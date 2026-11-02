using System.Threading.Channels;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Learning;

public sealed class LearningQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    public void Enqueue(Guid jobId) => _channel.Writer.TryWrite(jobId);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Extracts and chunks documents outside the request. Jobs live in SQL, so a restart (Render sleep, deploy) resumes
/// them; a semaphore keeps heavy extraction to <see cref="LearningOptions.MaxConcurrentJobs"/> at a time.
/// </summary>
public sealed class LearningProcessingService(
    LearningQueue queue, IServiceScopeFactory scopeFactory, IOptions<LearningOptions> options, ILogger<LearningProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);
        using var slots = new SemaphoreSlim(Math.Max(1, options.Value.MaxConcurrentJobs));
        try
        {
            await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
            {
                await slots.WaitAsync(stoppingToken);
                _ = Task.Run(async () =>
                {
                    try { await RunAsync(jobId, stoppingToken); }
                    finally { slots.Release(); }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RequeueUnfinishedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jobs = await db.Set<LearningProcessingJob>()
                .Where(j => j.Status == LearningStatus.Pending || j.Status == LearningStatus.Processing)
                .OrderBy(j => j.CreatedAt).ToListAsync(ct);
            foreach (var job in jobs) job.Status = LearningStatus.Pending; // a job cut off mid-run starts over
            await db.SaveChangesAsync(ct);
            jobs.ForEach(j => queue.Enqueue(j.Id));
            if (jobs.Count > 0) logger.LogInformation("Re-queued {Count} learning processing jobs", jobs.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not re-queue learning processing jobs");
        }
    }

    private async Task RunAsync(Guid jobId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LearningProcessor>().ProcessAsync(jobId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Learning processing job {JobId} crashed", jobId);
        }
    }
}

/// <summary>Turns one stored file into paragraphs and chunks; never touches the exam question data.</summary>
public sealed class LearningProcessor(AppDbContext db, IFileStorage storage, IOptions<LearningOptions> options, TimeProvider clock, ILogger<LearningProcessor> logger)
{
    public async Task ProcessAsync(Guid jobId, CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var job = await db.Set<LearningProcessingJob>().FirstOrDefaultAsync(j => j.Id == jobId, stoppingToken);
        if (job is null || job.Status is LearningStatus.Completed or LearningStatus.Failed) return;
        var learning = await db.Set<LearningDocument>().Include(d => d.Document).FirstOrDefaultAsync(d => d.Id == job.LearningDocumentId, stoppingToken);
        if (learning is null) return;

        job.Status = learning.Status = LearningStatus.Processing;
        job.Attempt++;
        job.StartedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(stoppingToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.JobTimeoutSeconds));
        string? failure;
        IReadOnlyList<TextChunk> chunks = [];
        ExtractedText? text = null;
        try
        {
            // Copy to a temp file first: extraction then reads from disk instead of holding a second copy in memory.
            var tempPath = Path.GetTempFileName();
            try
            {
                await using (var source = storage.OpenRead(learning.Document.StoredFileName))
                await using (var target = File.Create(tempPath))
                    await source.CopyToAsync(target, timeout.Token);
                await using var file = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
                text = await Task.Run(() => LearningTextExtractor.Extract(file, "." + learning.Format, settings.Limits, timeout.Token), timeout.Token);
            }
            finally
            {
                try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
            }

            failure = text.Failure switch
            {
                TextExtractionFailure.None => null,
                TextExtractionFailure.Encrypted => "Şifrə ilə qorunan sənəd dəstəklənmir.",
                TextExtractionFailure.TooLarge => $"Sənəd emal limitlərini aşır (ən çox {settings.MaxPages} səhifə və {settings.MaxCharacters / 1_000_000} milyon simvol).",
                TextExtractionFailure.NoText when text.LooksScanned => "Sənəd skan edilmiş şəkillərdən ibarətdir. Mətn tanıma (OCR) hələ dəstəklənmir — mətnli PDF, DOCX və ya TXT yükləyin.",
                TextExtractionFailure.NoText => "Sənəddə oxunan mətn tapılmadı.",
                _ => "Sənəd oxuna bilmədi. Fayl zədələnmiş ola bilər.",
            };
            if (failure is null) chunks = LearningChunker.Chunk(text.Blocks, settings.Chunking);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            failure = "Emal vaxt limitini aşdı. Daha kiçik sənəd yükləyin.";
        }
        catch (FileNotFoundException)
        {
            failure = "Sənəd faylı tapılmadı.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never leave a job stuck in Processing; the reason shown to the user stays generic.
            logger.LogError(ex, "Learning extraction failed for document {DocumentId}", learning.DocumentId);
            failure = "Sənəd emal oluna bilmədi.";
        }

        if (failure is not null)
        {
            Fail(job, learning, failure);
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("Learning document {DocumentId} failed: {Reason}", learning.DocumentId, failure);
            return;
        }

        // Old chunks (retry after a failure) and new ones swap atomically; the retrying SQL strategy needs the
        // transaction inside ExecuteAsync.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(CancellationToken.None);
            await db.Set<LearningDocumentChunk>().Where(c => c.LearningDocumentId == learning.Id).ExecuteDeleteAsync(CancellationToken.None);
            db.Set<LearningDocumentChunk>().AddRange(chunks.Select(c => new LearningDocumentChunk
            {
                Id = Guid.NewGuid(), LearningDocumentId = learning.Id, Ordinal = c.Ordinal, Text = c.Text, TokenEstimate = c.Tokens,
                PageFrom = c.PageFrom, PageTo = c.PageTo, Section = c.Section is { Length: > 200 } s ? s[..200] : c.Section,
                ParagraphFrom = c.ParagraphFrom, ParagraphTo = c.ParagraphTo,
            }));
            learning.Status = job.Status = LearningStatus.Completed;
            learning.PageCount = text!.PageCount;
            learning.ParagraphCount = text.Blocks.Count;
            learning.CharacterCount = text.Characters;
            learning.ChunkCount = chunks.Count;
            learning.LooksScanned = text.LooksScanned;
            learning.Error = null;
            learning.ProcessedAt = job.FinishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken.None);
            await tx.CommitAsync(CancellationToken.None);
        });
        logger.LogInformation("Learning document {DocumentId} indexed: {Chunks} chunks", learning.DocumentId, chunks.Count);
    }

    private void Fail(LearningProcessingJob job, LearningDocument learning, string reason)
    {
        job.Status = learning.Status = LearningStatus.Failed;
        job.Error = learning.Error = reason;
        job.FinishedAt = clock.GetUtcNow();
    }
}

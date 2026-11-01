using System.Threading.Channels;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Documents;

/// <summary>In-process queue of document ids waiting to be parsed.</summary>
public sealed class ProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid documentId) => _channel.Writer.TryWrite(documentId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Consumes the queue; on startup re-queues documents left unfinished by a restart.</summary>
public sealed class DocumentProcessingService(
    ProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<DocumentOptions> options,
    ILogger<DocumentProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);

        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxConcurrentJobs, CancellationToken = stoppingToken };
        try
        {
            await Parallel.ForEachAsync(queue.ReadAllAsync(stoppingToken), parallel, async (documentId, ct) =>
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DocumentProcessor>().ProcessAsync(documentId, ct);
            });
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Documents still in Processing are picked up again on the next start.
        }
    }

    private async Task RequeueUnfinishedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = await db.Documents
                .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Processing)
                .OrderBy(d => d.CreatedAt)
                .Select(d => d.Id)
                .ToListAsync(ct);

            ids.ForEach(queue.Enqueue);
            if (ids.Count > 0)
            {
                logger.LogInformation("Re-queued {Count} unfinished documents", ids.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not re-queue unfinished documents");
        }
    }
}

/// <summary>Parses one document and stores its questions.</summary>
public sealed class DocumentProcessor(
    AppDbContext db,
    IFileStorage storage,
    PdfQuestionParser parser,
    TimeProvider clock,
    IOptions<DocumentOptions> options,
    ILogger<DocumentProcessor> logger)
{
    private const int MaxReportWarnings = 100;

    public async Task ProcessAsync(Guid documentId, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null || document.Status is DocumentStatus.Completed or DocumentStatus.Failed)
        {
            return; // Deleted meanwhile, or already handled.
        }

        document.Status = DocumentStatus.Processing;
        await db.SaveChangesAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ParseTimeoutSeconds));

        ParseResult result;
        try
        {
            await using var stream = storage.OpenRead(document.StoredFileName);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, timeout.Token);
            buffer.Position = 0;

            // CPU-bound; keep it off the request/IO threads.
            result = await Task.Run(() => parser.Parse(buffer, timeout.Token), timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Shutdown: leave as Processing so it is re-queued on restart.
        }
        catch (OperationCanceledException)
        {
            await FailAsync(document, "Emal çox uzun çəkdi.", ct);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Parsing failed for document {DocumentId}", documentId);
            await FailAsync(document, "PDF faylı oxuna bilmədi.", ct);
            return;
        }

        if (!result.IsSuccess)
        {
            document.PageCount = result.PageCount > 0 ? result.PageCount : document.PageCount;
            await FailAsync(document, FailureMessage(result.Failure), ct);
            return;
        }

        // Reprocessing replaces earlier questions; one SaveChanges keeps it atomic.
        var existing = await db.Questions.Where(q => q.DocumentId == documentId).ToListAsync(ct);
        db.Questions.RemoveRange(existing);
        db.Questions.AddRange(result.Questions.Select(q => ToEntity(documentId, q)));

        document.Status = DocumentStatus.Completed;
        document.PageCount = result.PageCount;
        document.ErrorMessage = null;
        document.ProcessedAt = clock.GetUtcNow();
        document.ProcessingReport = new ProcessingReport
        {
            QuestionsFound = result.Questions.Count,
            AutoDetected = result.Questions.Count(q => q.CorrectIndex is not null),
            Unverified = result.Questions.Count(q => q.CorrectIndex is null),
            DuplicatesSkipped = result.DuplicatesSkipped,
            Warnings = result.Warnings.Take(MaxReportWarnings).ToList(),
        };

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Document {DocumentId} processed: {Count} questions", documentId, result.Questions.Count);
    }

    private static Question ToEntity(Guid documentId, ParsedQuestion parsed) => new()
    {
        DocumentId = documentId,
        SourceNumber = parsed.Number,
        Text = parsed.Text,
        ContentHash = parsed.ContentHash,
        VerificationStatus = parsed.CorrectIndex is null ? VerificationStatus.Unverified : VerificationStatus.AutoDetected,
        ParseWarning = parsed.Warning is { Length: > 500 } w ? w[..500] : parsed.Warning,
        Options = parsed.Options.Select((o, i) => new AnswerOption
        {
            Position = (byte)i,
            Label = o.Label,
            Text = o.Text,
            IsCorrect = parsed.CorrectIndex == i,
        }).ToList(),
    };

    private async Task FailAsync(UploadedDocument document, string message, CancellationToken ct)
    {
        document.Status = DocumentStatus.Failed;
        document.ErrorMessage = message;
        document.ProcessedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private static string FailureMessage(ParseFailure failure) => failure switch
    {
        ParseFailure.Encrypted => "Şifrə ilə qorunan PDF dəstəklənmir.",
        ParseFailure.Scanned => "Skan edilmiş PDF — OCR hələ dəstəklənmir.",
        ParseFailure.NoQuestions => "Sual formatı tanınmadı. Gözlənilən format: \"1. Sual mətni\" və \"A) variant\".",
        _ => "PDF faylı oxuna bilmədi.",
    };
}

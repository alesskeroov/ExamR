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
                .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Processing
                    || (d.Status == DocumentStatus.Completed && d.StoredFileName.EndsWith(".docx")
                        && d.Questions.Any(q => q.Explanation == null || q.Explanation == "")))
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
    DocxQuestionParser docxParser,
    TimeProvider clock,
    IOptions<DocumentOptions> options,
    ILogger<DocumentProcessor> logger)
{
    private const int MaxReportWarnings = 100;

    public async Task ProcessAsync(Guid documentId, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null || document.Status == DocumentStatus.Failed)
        {
            return; // Deleted meanwhile, or already handled.
        }

        // Enrich legacy Word answers without replacing question IDs or deleting study history.
        var repairAnswers = document.Status == DocumentStatus.Completed;
        if (!repairAnswers)
        {
            document.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync(ct);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ParseTimeoutSeconds));

        var isDocx = string.Equals(Path.GetExtension(document.StoredFileName), ".docx", StringComparison.OrdinalIgnoreCase);
        ParseResult result;
        using var buffer = new MemoryStream();
        var length = 0;
        try
        {
            await using var stream = storage.OpenRead(document.StoredFileName);
            await stream.CopyToAsync(buffer, timeout.Token);
            buffer.Position = 0;
            length = (int)buffer.Length;

            // CPU-bound; keep it off the request/IO threads.
            result = await Task.Run(() => isDocx ? docxParser.Parse(buffer, timeout.Token) : parser.Parse(buffer, timeout.Token), timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Shutdown: leave as Processing so it is re-queued on restart.
        }
        catch (OperationCanceledException)
        {
            if (repairAnswers) return;
            await FailAsync(document, "Emal çox uzun çəkdi.", ct);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Parsing failed for document {DocumentId}", documentId);
            if (repairAnswers) return;
            await FailAsync(document, isDocx ? "DOCX faylı oxuna bilmədi." : "PDF faylı oxuna bilmədi.", ct);
            return;
        }

        if (!result.IsSuccess)
        {
            if (repairAnswers) return;
            document.PageCount = result.PageCount > 0 ? result.PageCount : document.PageCount;
            await FailAsync(document, isDocx
                ? result.Failure == ParseFailure.NoQuestions
                    ? "Sual formatı tanınmadı. Nömrəli suallar, A) variantlar və ya Cavab:/İzah: ilə ayrılmış açıq cavablar istifadə edin."
                    : "DOCX faylı oxuna bilmədi."
                : FailureMessage(result.Failure), ct);
            return;
        }

        if (repairAnswers)
        {
            var legacy = await db.Questions.Where(q => q.DocumentId == documentId
                && (q.Explanation == null || q.Explanation == "")).ToListAsync(ct);
            foreach (var question in legacy)
            {
                var parsed = result.Questions.FirstOrDefault(q => q.Number == question.SourceNumber
                    && q.Options.Count == 0 && !string.IsNullOrWhiteSpace(q.Explanation));
                if (parsed is null) continue;
                question.Text = parsed.Text;
                question.Explanation = parsed.Explanation;
                question.ContentHash = parsed.ContentHash;
            }
            await db.SaveChangesAsync(ct);
            return;
        }

        // Reprocessing replaces earlier questions and their visuals; one SaveChanges keeps it atomic.
        // Old visual files stay in storage: same images get the same keys, so they are reused, not duplicated.
        var existingVisuals = await db.QuestionVisuals.Where(v => v.DocumentId == documentId).ToListAsync(ct);
        var existing = await db.Questions.Where(q => q.DocumentId == documentId).ToListAsync(ct);
        db.QuestionVisuals.RemoveRange(existingVisuals);
        db.Questions.RemoveRange(existing);
        var questions = result.Questions.Select(q => ToEntity(documentId, q)).ToList();
        db.Questions.AddRange(questions);

        var visuals = VisualImport.None;
        if (!isDocx && options.Value.ExtractVisuals)
        {
            var storedKeys = existingVisuals.Where(v => v.StorageKey is not null).Select(v => v.StorageKey!).ToHashSet();
            // A fresh view over the same bytes: no copy, and independent of whatever the parser did with its stream.
            using var pdf = new MemoryStream(buffer.GetBuffer(), 0, length, writable: false);
            visuals = await DocumentVisuals.ImportAsync(documentId, pdf, result, questions, storedKeys, storage, clock.GetUtcNow(),
                logger, timeout.Token, ct);
            db.QuestionVisuals.AddRange(visuals.Visuals);
        }

        document.Status = DocumentStatus.Completed;
        document.PageCount = isDocx ? null : result.PageCount;
        document.ErrorMessage = null;
        document.ProcessedAt = clock.GetUtcNow();
        document.ProcessingReport = new ProcessingReport
        {
            QuestionsFound = result.Questions.Count,
            AutoDetected = result.Questions.Count(q => q.CorrectIndex is not null),
            Unverified = result.Questions.Count(q => q.CorrectIndex is null),
            DuplicatesSkipped = result.DuplicatesSkipped,
            VisualsFound = visuals.Visuals.Count,
            VisualsNeedingReview = visuals.Visuals.Count(v => v.MappingStatus == QuestionVisualStatus.NeedsReview),
            Warnings = visuals.Warnings.Concat(result.Warnings).Take(MaxReportWarnings).ToList(),
        };

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Document {DocumentId} processed: {Count} questions", documentId, result.Questions.Count);
    }

    private static Question ToEntity(Guid documentId, ParsedQuestion parsed) => new()
    {
        DocumentId = documentId,
        SourceNumber = parsed.Number,
        Text = parsed.Text,
        Explanation = parsed.Explanation,
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

using ExamFlow.Api.Features.Usage;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Learning;

public sealed record LearningDocumentDto(Guid DocumentId, string FileName, string Format, string Status, int? PageCount, int ParagraphCount,
    long CharacterCount, int ChunkCount, bool LooksScanned, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? ProcessedAt);
public sealed record LearningChunkDto(int Ordinal, string Text, int TokenEstimate, int? PageFrom, int? PageTo, string? Section, int ParagraphFrom, int ParagraphTo);

/// <summary>Document intake for the AI tutor (extraction and chunking only, no AI calls). Open to every signed-in user; document access rules apply.</summary>
public static class LearningEndpoints
{
    // AI Tutor works only with open-question PDF and Word (DOCX) files. TXT support stays in the text extractor; it is only
    // switched off here, and already indexed TXT rows are kept untouched.
    private static readonly HashSet<string> Formats = [".pdf", ".docx"];

    /// <summary>Readable documents for the study guide: files with closed (multiple-choice) test questions are left out.</summary>
    internal static IQueryable<UploadedDocument> GuideDocuments(AppDbContext db, Guid userId) =>
        DocumentAccess.Readable(db, userId).Where(d => !db.Questions.Any(q => q.DocumentId == d.Id && q.Options.Count >= 2));

    public static void MapLearningEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/learning").RequireAuthorization();
        group.MapPost("/documents", Upload).DisableAntiforgery()
            .RequireRateLimiting(AuthenticationSetup.UploadRateLimit)
            // Raises the body limit for this endpoint only; every other route keeps the global Documents limit.
            .WithMetadata(new RequestSizeLimitAttribute(LearningOptionsLimit + 1024 * 1024))
            .WithFormOptions(multipartBodyLengthLimit: LearningOptionsLimit + 1024 * 1024);
        group.MapGet("/documents", List);
        group.MapPost("/documents/{documentId:guid}/process", Process);
        group.MapGet("/documents/{documentId:guid}", Get);
        group.MapGet("/documents/{documentId:guid}/chunks", Chunks);
        group.MapGet("/documents/{documentId:guid}/search", Search);
    }

    private static async Task<IResult> Search(Guid documentId, string? q, int? top, ClaimsPrincipal user, AppDbContext db, IChunkRetriever retriever,
        IOptions<LearningOptions> options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 500) return Invalid("Sorğu 1–500 simvol olmalıdır.");
        if (!await DocumentAccess.Readable(db, user.GetUserId()).AnyAsync(d => d.Id == documentId, ct)) return NotFound();
        var learning = await db.Set<LearningDocument>().AsNoTracking().Where(l => l.DocumentId == documentId)
            .Select(l => new { l.Id, l.Status }).FirstOrDefaultAsync(ct);
        if (learning is null) return TypedResults.Problem("Sənəd hələ emal üçün göndərilməyib.", statusCode: 404);
        if (learning.Status != LearningStatus.Completed) return TypedResults.Problem("Sənəd hələ emal olunur və ya emal alınmayıb.", statusCode: StatusCodes.Status409Conflict);
        return TypedResults.Ok(await retriever.SearchAsync(learning.Id, q.Trim(), top ?? options.Value.Retrieval.TopK, ct));
    }

    // Upper bound for the HTTP layer; the configured Learning:MaxFileSizeBytes is enforced inside the handler.
    private const long LearningOptionsLimit = 50L * 1024 * 1024;

    private static async Task<IResult> Upload(IFormFile? file, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, LearningQueue queue,
        IOptions<LearningOptions> options, TimeProvider clock, UsageLimiter usage, CancellationToken ct)
    {
        var limit = Math.Min(options.Value.MaxFileSizeBytes, LearningOptionsLimit);
        if (file is null || file.Length == 0) return Invalid("PDF və ya Word (.docx) faylı seçilməyib.");
        if (file.Length > limit) return TypedResults.Problem($"Fayl {limit / 1024 / 1024} MB-dan böyük ola bilməz.", statusCode: StatusCodes.Status413PayloadTooLarge);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Formats.Contains(extension)) return Invalid(extension == ".doc" ? "Köhnə .doc formatı dəstəklənmir. Faylı Word-də .docx kimi saxlayıb yenidən yükləyin." : "AI Tutor yalnız açıq suallı PDF və Word (.docx) faylları qəbul edir.");

        // Stream to a temp file (not memory): hash and signature are checked there before anything is stored.
        var tempPath = Path.GetTempFileName();
        try
        {
            byte[] sha256;
            await using (var temp = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await file.CopyToAsync(temp, ct);
                temp.Position = 0;
                if (!await HasValidSignature(temp, extension, ct)) return Invalid("Faylın məzmunu uzantısına uyğun deyil.");
                temp.Position = 0;
                sha256 = await SHA256.HashDataAsync(temp, ct);
            }

            var userId = user.GetUserId();
            var existing = await db.Documents.AsNoTracking().Where(d => d.UploadedByUserId == userId && d.Sha256 == sha256)
                .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (existing is { } existingId)
                return await StartOrReuse(existingId, userId, db, queue, clock, ct); // same file: index the existing document once

            Guid queuedJob = Guid.Empty;
            var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await usage.LockAsync(userId, UsageKind.Upload, ct);
                var duplicate = await db.Documents.AsNoTracking().Where(d => d.UploadedByUserId == userId && d.Sha256 == sha256)
                    .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
                if (duplicate is not null) return (IResult)TypedResults.Problem("Bu sənəd artıq yüklənib.", statusCode: 409,
                    extensions: new Dictionary<string, object?> { ["documentId"] = duplicate });
                var document = new UploadedDocument
                {
                    OriginalFileName = SafeName(file.FileName), FileSizeBytes = file.Length, Sha256 = sha256,
                    // Tutor extraction has its own state; keep it outside the exam parser queue.
                    Status = DocumentStatus.Completed, ProcessedAt = clock.GetUtcNow(), UploadedByUserId = userId,
                };
                db.Documents.Add(document);
                document.StoredFileName = $"{document.Id:N}{extension}";
                var (learning, job) = NewJob(document.Id, extension, clock);
                // Use the learning resource id so indexing an existing exam document cannot refund its upload.
                if (await usage.TryConsumeAsync(user, UsageKind.Upload, learning.Id, ct) is { } denied) return denied;
                await using (var stored = File.OpenRead(tempPath)) await storage.SaveAsync(document.StoredFileName, stored, ct);
                db.Add(learning);
                db.Add(job);
                try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
                catch (DbUpdateException) { storage.Delete(document.StoredFileName); throw; }
                queuedJob = job.Id;
                return (IResult)TypedResults.Accepted($"/api/learning/documents/{document.Id}", await Dto(db, document.Id, ct));
            });
            if (queuedJob != Guid.Empty) queue.Enqueue(queuedJob);
            return result;
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
        }
    }

    public sealed record LearningDocumentListItem(Guid DocumentId, string FileName, string Format, string? Status, int? PageCount, int ChunkCount, DateTimeOffset UploadedAt);

    /// <summary>Readable PDF and Word documents with their index state (null status = not indexed yet), for the tutor's document picker.</summary>
    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var items = await GuideDocuments(db, user.GetUserId())
            .Where(d => d.StoredFileName.EndsWith(".pdf") || d.StoredFileName.EndsWith(".docx"))
            .OrderByDescending(d => d.CreatedAt).Take(200)
            .Select(d => new
            {
                d.Id, d.OriginalFileName, d.StoredFileName, d.CreatedAt,
                Learning = db.Set<LearningDocument>().Where(l => l.DocumentId == d.Id).Select(l => new { l.Status, l.PageCount, l.ChunkCount }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return TypedResults.Ok(items.Select(d => new LearningDocumentListItem(d.Id, d.OriginalFileName, Path.GetExtension(d.StoredFileName).TrimStart('.').ToLowerInvariant(),
            d.Learning?.Status.ToString(), d.Learning?.PageCount, d.Learning?.ChunkCount ?? 0, d.CreatedAt)));
    }

    private static async Task<IResult> Process(Guid documentId, ClaimsPrincipal user, AppDbContext db, LearningQueue queue, TimeProvider clock, CancellationToken ct) =>
        await StartOrReuse(documentId, user.GetUserId(), db, queue, clock, ct);

    /// <summary>Indexes a readable document once: completed and running indexes are returned as they are; only a failed one is retried.</summary>
    private static async Task<IResult> StartOrReuse(Guid documentId, Guid userId, AppDbContext db, LearningQueue queue, TimeProvider clock, CancellationToken ct)
    {
        var document = await GuideDocuments(db, userId).Where(d => d.Id == documentId)
            .Select(d => new { d.Id, d.StoredFileName }).FirstOrDefaultAsync(ct);
        if (document is null)
            return await DocumentAccess.Readable(db, userId).AnyAsync(d => d.Id == documentId, ct)
                ? Invalid("Qapalı test sualları olan fayllar öyrənmə bələdçisində istifadə olunmur.")
                : NotFound();
        var extension = Path.GetExtension(document.StoredFileName).ToLowerInvariant();
        if (!Formats.Contains(extension)) return Invalid("Öyrənmə bələdçisi yalnız PDF və Word (.docx) faylları ilə işləyir.");

        var learning = await db.Set<LearningDocument>().FirstOrDefaultAsync(l => l.DocumentId == documentId, ct);
        if (learning is { Status: LearningStatus.Completed }) return TypedResults.Ok(await Dto(db, documentId, ct));
        if (learning is { Status: LearningStatus.Pending or LearningStatus.Processing }) return TypedResults.Accepted($"/api/learning/documents/{documentId}", await Dto(db, documentId, ct));

        LearningProcessingJob job;
        if (learning is null)
        {
            (learning, job) = NewJob(documentId, extension, clock);
            db.Add(learning);
        }
        else
        {
            learning.Status = LearningStatus.Pending;
            learning.Error = null;
            job = new LearningProcessingJob { Id = Guid.NewGuid(), LearningDocumentId = learning.Id, Status = LearningStatus.Pending, CreatedAt = clock.GetUtcNow() };
        }
        db.Add(job);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return TypedResults.Accepted($"/api/learning/documents/{documentId}", await Dto(db, documentId, ct)); } // parallel request won the unique index
        queue.Enqueue(job.Id);
        return TypedResults.Accepted($"/api/learning/documents/{documentId}", await Dto(db, documentId, ct));
    }

    private static async Task<IResult> Get(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!await DocumentAccess.Readable(db, user.GetUserId()).AnyAsync(d => d.Id == documentId, ct)) return NotFound();
        return await Dto(db, documentId, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.Problem("Sənəd hələ emal üçün göndərilməyib.", statusCode: 404);
    }

    private static async Task<IResult> Chunks(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct, int? page = null, int? pageSize = null)
    {
        if (!await DocumentAccess.Readable(db, user.GetUserId()).AnyAsync(d => d.Id == documentId, ct)) return NotFound();
        var (number, size) = Paging.Normalize(page, pageSize);
        var query = db.Set<LearningDocumentChunk>().AsNoTracking()
            .Where(c => db.Set<LearningDocument>().Any(l => l.Id == c.LearningDocumentId && l.DocumentId == documentId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Ordinal).Skip((number - 1) * size).Take(size)
            .Select(c => new LearningChunkDto(c.Ordinal, c.Text, c.TokenEstimate, c.PageFrom, c.PageTo, c.Section, c.ParagraphFrom, c.ParagraphTo))
            .ToListAsync(ct);
        return TypedResults.Ok(new PagedResult<LearningChunkDto>(items, number, size, total));
    }

    private static (LearningDocument, LearningProcessingJob) NewJob(Guid documentId, string extension, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var learning = new LearningDocument { Id = Guid.NewGuid(), DocumentId = documentId, Status = LearningStatus.Pending, Format = extension.TrimStart('.'), CreatedAt = now };
        return (learning, new LearningProcessingJob { Id = Guid.NewGuid(), LearningDocumentId = learning.Id, Status = LearningStatus.Pending, CreatedAt = now });
    }

    private static Task<LearningDocumentDto?> Dto(AppDbContext db, Guid documentId, CancellationToken ct) =>
        db.Set<LearningDocument>().AsNoTracking().Where(l => l.DocumentId == documentId)
            .Select(l => new LearningDocumentDto(l.DocumentId, l.Document.OriginalFileName, l.Format, l.Status.ToString(), l.PageCount, l.ParagraphCount,
                l.CharacterCount, l.ChunkCount, l.LooksScanned, l.Error, l.CreatedAt, l.ProcessedAt))
            .FirstOrDefaultAsync(ct);

    /// <summary>Magic bytes, not the client's content type: %PDF- for PDF, a ZIP header for DOCX (its structure is checked when parsed).</summary>
    private static async Task<bool> HasValidSignature(Stream file, string extension, CancellationToken ct)
    {
        var head = new byte[8192];
        var read = await file.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        var bytes = head.AsSpan(0, read);
        return extension switch
        {
            ".pdf" => bytes.StartsWith("%PDF-"u8),
            ".docx" => bytes.StartsWith("PK\u0003\u0004"u8),
            ".txt" => !bytes.Contains((byte)0) && IsUtf8Prefix(bytes),
            _ => false,
        };
    }

    // The 8 KB sample may end inside a multi-byte character, so up to 3 trailing bytes are allowed to be incomplete.
    private static bool IsUtf8Prefix(ReadOnlySpan<byte> bytes)
    {
        var decoder = new UTF8Encoding(false, true).GetDecoder();
        try { decoder.GetCharCount(bytes, flush: false); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    private static string SafeName(string fileName)
    {
        var name = new string(Path.GetFileName(fileName).Trim().Where(c => !char.IsControl(c)).ToArray());
        return name.Length > 255 ? name[^255..] : name;
    }

    private static IResult Invalid(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
    private static IResult NotFound() => TypedResults.Problem("Sənəd tapılmadı.", statusCode: StatusCodes.Status404NotFound);
}

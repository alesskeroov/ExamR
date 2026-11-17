using ExamFlow.Api.Features.Usage;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private static readonly HashSet<string> Formats = [".pdf", ".docx", ".pptx"];

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
        group.MapGet("/documents/{documentId:guid}/slides/{number:int}", Slide);
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
        if (await GuideNotIncluded(user, usage, ct) is { } notIncluded) return notIncluded;
        var limit = Math.Min(options.Value.MaxFileSizeBytes, LearningOptionsLimit);
        if (file is null || file.Length == 0) return Invalid("PDF, Word (.docx) və ya PowerPoint (.pptx) faylı seçilməyib.");
        if (file.Length > limit) return TypedResults.Problem($"Fayl {limit / 1024 / 1024} MB-dan böyük ola bilməz.", statusCode: StatusCodes.Status413PayloadTooLarge);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Formats.Contains(extension)) return Invalid(extension switch
        {
            ".doc" => "Köhnə .doc formatı dəstəklənmir. Faylı Word-də .docx kimi saxlayıb yenidən yükləyin.",
            ".ppt" => "Köhnə .ppt formatı dəstəklənmir. Faylı PowerPoint-də .pptx kimi saxlayıb yenidən yükləyin.",
            _ => "Öyrənmə bələdçisi yalnız PDF, Word (.docx) və PowerPoint (.pptx) faylları qəbul edir.",
        });

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

    public sealed record LearningDocumentListItem(Guid DocumentId, string FileName, string Format, string? Status, int? PageCount, int ChunkCount, DateTimeOffset UploadedAt, bool IsOwner);

    /// <summary>Readable PDF and Word documents with their index state (null status = not indexed yet), for the tutor's document picker.</summary>
    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var items = await GuideDocuments(db, userId)
            .Where(d => d.StoredFileName.EndsWith(".pdf") || d.StoredFileName.EndsWith(".docx") || d.StoredFileName.EndsWith(".pptx"))
            .OrderByDescending(d => d.CreatedAt).Take(200)
            .Select(d => new
            {
                d.Id, d.OriginalFileName, d.StoredFileName, d.CreatedAt, IsOwner = d.UploadedByUserId == userId,
                Learning = db.Set<LearningDocument>().Where(l => l.DocumentId == d.Id).Select(l => new { l.Status, l.PageCount, l.ChunkCount }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return TypedResults.Ok(items.Select(d => new LearningDocumentListItem(d.Id, d.OriginalFileName, Path.GetExtension(d.StoredFileName).TrimStart('.').ToLowerInvariant(),
            d.Learning?.Status.ToString(), d.Learning?.PageCount, d.Learning?.ChunkCount ?? 0, d.CreatedAt, d.IsOwner)));
    }

    private static async Task<IResult> Process(Guid documentId, ClaimsPrincipal user, AppDbContext db, LearningQueue queue, UsageLimiter usage, TimeProvider clock, CancellationToken ct) =>
        await GuideNotIncluded(user, usage, ct) ?? await StartOrReuse(documentId, user.GetUserId(), db, queue, clock, ct);

    /// <summary>Packages without the study guide (limit 0) cannot prepare documents for it; the upload quota is not spent.</summary>
    private static async Task<IResult?> GuideNotIncluded(ClaimsPrincipal user, UsageLimiter usage, CancellationToken ct)
    {
        var check = await usage.CheckAsync(user.GetUserId(), user.IsAdmin(), UsageKind.Guide, ct);
        return check.Limit == 0 ? check.Problem() : null;
    }

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
        if (!Formats.Contains(extension)) return Invalid("Öyrənmə bələdçisi yalnız PDF, Word (.docx) və PowerPoint (.pptx) faylları ilə işləyir.");

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
            ".docx" or ".pptx" => bytes.StartsWith("PK\u0003\u0004"u8),
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

    /// <summary>
    /// The picture of one slide of a presentation the user can read, taken from the stored file on request (nothing
    /// extra is stored). The file is copied to a temp file once and reused for a few minutes while the slides load.
    /// </summary>
    private static async Task<IResult> Slide(Guid documentId, int number, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, IMemoryCache cache,
        IOptions<LearningOptions> options, HttpResponse response, CancellationToken ct)
    {
        var document = await DocumentAccess.Readable(db, user.GetUserId()).Where(d => d.Id == documentId)
            .Select(d => new { d.StoredFileName }).FirstOrDefaultAsync(ct);
        if (document is null || !document.StoredFileName.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)) return NotFound();
        if (number < 1 || number > options.Value.MaxPages) return SlideNotFound();

        string path;
        try { path = await SlideFileAsync(cache, storage, document.StoredFileName, ct); }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return NotFound(); }

        SlidePicture? picture;
        await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            picture = LearningTextExtractor.SlidePicture(file, number, options.Value.Limits);
        if (picture is null) return SlideNotFound();
        // Private and short-lived, like question images: access can be withdrawn.
        response.Headers.CacheControl = "private, max-age=3600";
        return TypedResults.File(picture.Content, picture.ContentType);
    }

    private static async Task<string> SlideFileAsync(IMemoryCache cache, IFileStorage storage, string storedFileName, CancellationToken ct)
    {
        var key = ("learning-slide-file", storedFileName);
        if (cache.TryGetValue(key, out string? cached) && cached is not null && File.Exists(cached)) return cached;
        var path = Path.GetTempFileName();
        try
        {
            await using var source = storage.OpenRead(storedFileName);
            await using var target = File.Create(path);
            await source.CopyToAsync(target, ct);
        }
        catch
        {
            try { File.Delete(path); } catch (IOException) { /* best effort */ }
            throw;
        }
        var options = new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10), Size = 1 };
        options.RegisterPostEvictionCallback((_, value, _, _) => { try { File.Delete((string)value!); } catch (IOException) { /* in use: next start cleans temp */ } });
        cache.Set(key, path, options);
        return path;
    }

    private static IResult SlideNotFound() => TypedResults.Problem("Bu slaydın şəkli tapılmadı.", statusCode: StatusCodes.Status404NotFound);
    private static IResult Invalid(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
    private static IResult NotFound() => TypedResults.Problem("Sənəd tapılmadı.", statusCode: StatusCodes.Status404NotFound);
}

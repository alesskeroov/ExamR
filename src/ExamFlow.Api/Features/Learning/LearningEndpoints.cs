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

/// <summary>Document intake for the AI tutor (Phase 1: extraction and chunking only, no AI calls). Admin-only for now.</summary>
public static class LearningEndpoints
{
    private static readonly HashSet<string> Formats = [".pdf", ".docx", ".txt"];

    public static void MapLearningEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/learning").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
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
        IOptions<LearningOptions> options, TimeProvider clock, CancellationToken ct)
    {
        var limit = Math.Min(options.Value.MaxFileSizeBytes, LearningOptionsLimit);
        if (file is null || file.Length == 0) return Invalid("PDF, DOCX və ya TXT faylı seçilməyib.");
        if (file.Length > limit) return TypedResults.Problem($"Fayl {limit / 1024 / 1024} MB-dan böyük ola bilməz.", statusCode: StatusCodes.Status413PayloadTooLarge);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Formats.Contains(extension)) return Invalid("Yalnız PDF, DOCX və TXT faylları qəbul edilir.");

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

            var document = new UploadedDocument
            {
                OriginalFileName = SafeName(file.FileName),
                FileSizeBytes = file.Length,
                Sha256 = sha256,
                // Not part of the exam pipeline: marked as processed with no questions, so the exam processor never
                // picks it up and exam/study/battle lists (which require questions) do not show it.
                Status = DocumentStatus.Completed,
                ProcessedAt = clock.GetUtcNow(),
                UploadedByUserId = userId,
            };
            db.Documents.Add(document);
            document.StoredFileName = $"{document.Id:N}{extension}";
            await using (var stored = File.OpenRead(tempPath))
                await storage.SaveAsync(document.StoredFileName, stored, ct);

            var (learning, job) = NewJob(document.Id, extension, clock);
            db.Add(learning);
            db.Add(job);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { storage.Delete(document.StoredFileName); throw; }
            queue.Enqueue(job.Id);
            return TypedResults.Accepted($"/api/learning/documents/{document.Id}", await Dto(db, document.Id, ct));
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
        }
    }

    public sealed record LearningDocumentListItem(Guid DocumentId, string FileName, string Format, string? Status, int? PageCount, int ChunkCount, DateTimeOffset UploadedAt);

    /// <summary>Readable PDF/DOCX/TXT documents with their index state (null status = not indexed yet), for the tutor's document picker.</summary>
    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var items = await DocumentAccess.Readable(db, user.GetUserId())
            .Where(d => d.StoredFileName.EndsWith(".pdf") || d.StoredFileName.EndsWith(".docx") || d.StoredFileName.EndsWith(".txt"))
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
        var document = await DocumentAccess.Readable(db, userId).Where(d => d.Id == documentId)
            .Select(d => new { d.Id, d.StoredFileName }).FirstOrDefaultAsync(ct);
        if (document is null) return NotFound();
        var extension = Path.GetExtension(document.StoredFileName).ToLowerInvariant();
        if (!Formats.Contains(extension)) return Invalid("Bu sənəd növü dəstəklənmir.");

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

    /// <summary>Magic bytes, not the client's content type: %PDF-, a ZIP header for DOCX, valid UTF-8 without NUL for TXT.</summary>
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

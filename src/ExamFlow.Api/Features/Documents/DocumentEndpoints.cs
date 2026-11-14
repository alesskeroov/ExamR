using ExamFlow.Api.Features.Usage;
using System.Security.Claims;
using System.Security.Cryptography;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Documents;

public static class DocumentEndpoints
{
    private static readonly string[] DocxContentTypes = [DocxQuestionParser.ContentType, "application/octet-stream", "application/zip"];
    private static readonly string[] PdfContentTypes = ["application/pdf", "application/x-pdf"];

    public static void MapDocumentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/documents").RequireAuthorization();

        // Auth is a bearer token that browsers never attach automatically, so CSRF does not apply.
        group.MapPost("/", Upload).DisableAntiforgery().RequireRateLimiting(AuthenticationSetup.UploadRateLimit);
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapGet("/{id:guid}/download", Download);
        group.MapGet("/{id:guid}/visuals/{visualId:guid}", Visual);
        group.MapPost("/{id:guid}/reprocess", Reprocess);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Upload(
        IFormFile? file,
        ClaimsPrincipal user,
        AppDbContext db,
        UsageLimiter usage,
        IFileStorage storage,
        ProcessingQueue queue,
        IOptions<DocumentOptions> options,
        CancellationToken ct,
        bool? dim = null)
    {
        var limits = options.Value;
        // Uploaded from the DİM admin page: kept out of exams, battles and every other user-facing list.
        var isDimSource = dim == true && user.IsAdmin();
        if (file is null || file.Length == 0)
        {
            return Invalid("PDF və ya DOCX faylı seçilməyib.");
        }

        if (file.Length > limits.MaxFileSizeBytes)
        {
            return TypedResults.Problem($"Fayl {limits.MaxFileSizeBytes / 1024 / 1024} MB-dan böyük ola bilməz.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isDocx = extension == ".docx";
        if (!(extension == ".pdf" && PdfContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            && !(isDocx && DocxContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)))
        {
            return Invalid("Yalnız PDF və DOCX faylları qəbul edilir.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        if (!isDocx && !HasPdfSignature(buffer))
        {
            return Invalid("Fayl düzgün PDF deyil.");
        }

        var sha256 = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        var userId = user.GetUserId();
        var duplicate = await db.Documents.AsNoTracking()
            .Where(d => d.UploadedByUserId == userId && d.Sha256 == sha256)
            .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (duplicate is not null)
        {
            if (isDimSource)
            {
                await db.Documents.Where(d => d.Id == duplicate).ExecuteUpdateAsync(s => s.SetProperty(d => d.IsDimSource, true), ct);
            }

            return TypedResults.Problem(isDocx ? "Bu DOCX artıq yüklənib." : "Bu PDF artıq yüklənib.", statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["documentId"] = duplicate });
        }

        int? pageCount = null;
        buffer.Position = 0;
        if (isDocx)
        {
            if (!DocxQuestionParser.Inspect(buffer))
                return Invalid("Fayl düzgün DOCX deyil, zədələnib və ya açılmış məzmunu icazə verilən həddi aşır.");
        }
        else
        {
            var inspection = PdfQuestionParser.Inspect(buffer);
            if (inspection.IsEncrypted)
            {
                return Invalid("Şifrə ilə qorunan PDF dəstəklənmir.");
            }

            if (!inspection.IsReadable)
            {
                return Invalid("PDF faylı oxuna bilmədi.");
            }

            if (inspection.PageCount > limits.MaxPages)
            {
                return Invalid($"PDF {limits.MaxPages} səhifədən çox ola bilməz.");
            }
            pageCount = inspection.PageCount;
        }

        Guid uploadedId = Guid.Empty;
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var document = new UploadedDocument
            {
                OriginalFileName = SafeDisplayName(file.FileName),
                FileSizeBytes = file.Length,
                Sha256 = sha256,
                PageCount = pageCount,
                Status = DocumentStatus.Pending,
                UploadedByUserId = user.GetUserId(),
                IsDimSource = isDimSource,
            };
            db.Documents.Add(document); // Assigns the sequential Guid key client-side.
            document.StoredFileName = $"{document.Id:N}{extension}";

            await usage.LockAsync(userId, UsageKind.Upload, ct);
            var concurrentDuplicate = await db.Documents.AsNoTracking().Where(d => d.UploadedByUserId == userId && d.Sha256 == sha256)
                .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            if (concurrentDuplicate is not null)
                return (IResult)TypedResults.Problem("Bu sənəd artıq yüklənib.", statusCode: 409,
                    extensions: new Dictionary<string, object?> { ["documentId"] = concurrentDuplicate });
            if (await usage.TryConsumeAsync(user, UsageKind.Upload, document.Id, ct) is { } denied) return denied;
            buffer.Position = 0;
            await storage.SaveAsync(document.StoredFileName, buffer, ct);

            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                storage.Delete(document.StoredFileName);
                throw;
            }

            uploadedId = document.Id;
            return (IResult)TypedResults.Accepted($"/api/documents/{document.Id}", await FindAsync(db, user, document.Id, ct));
        });
        if (uploadedId != Guid.Empty) queue.Enqueue(uploadedId);
        return result;
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct,
        bool? mine = null, DocumentStatus? status = null, int? page = null, int? pageSize = null, bool? forExam = null, bool? includeShared = null, bool? shared = null, bool? forBattle = null)
    {
        var (pageNumber, size) = Paging.Normalize(page, pageSize);
        var userId = user.GetUserId();
        var query = forBattle == true ? DocumentAccess.ForBattle(db, userId)
            : includeShared == true || shared == true ? DocumentAccess.Readable(db, userId)
            : db.Documents.AsNoTracking().Where(d => d.UploadedByUserId == userId && !d.IsDimSource);
        if (shared == true) query = query.Where(d => d.UploadedByUserId != userId);
        if (mine == true) query = query.Where(d => d.UploadedByUserId == userId);
        if (forExam == true)
            query = query.Where(d => d.Questions.Any(q => q.Options.Count >= 2)
                && !d.Questions.Any(q => q.Explanation != null && q.Explanation != ""));

        if (status is not null)
        {
            query = query.Where(d => d.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(d => d.CreatedAt), user, db)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResult<DocumentDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var dto = await FindAsync(db, user, id, ct);
        return dto is null ? NotFound() : TypedResults.Ok(dto);
    }

    private static async Task<IResult> Download(Guid id, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var document = await DocumentAccess.Readable(db, user.GetUserId()).Where(d => d.Id == id)
            .Select(d => new { d.StoredFileName, d.OriginalFileName }).FirstOrDefaultAsync(ct);
        if (document is null) return NotFound();
        try
        {
            var contentType = Path.GetExtension(document.StoredFileName).ToLowerInvariant() switch
            {
                ".docx" => DocxQuestionParser.ContentType,
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/pdf",
            };
            return TypedResults.File(storage.OpenRead(document.StoredFileName), contentType, document.OriginalFileName, enableRangeProcessing: true);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return NotFound(); }
    }

    /// <summary>An image of the document's questions; same permissions as the PDF itself.</summary>
    private static async Task<IResult> Visual(
        Guid id, Guid visualId, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, HttpResponse response, CancellationToken ct)
    {
        var readable = DocumentAccess.Readable(db, user.GetUserId());
        var visual = await db.QuestionVisuals.AsNoTracking()
            .Where(v => v.Id == visualId && v.DocumentId == id && readable.Any(d => d.Id == id))
            .Select(v => new { v.StorageKey, v.ContentType })
            .FirstOrDefaultAsync(ct);
        return visual is null
            ? TypedResults.Problem("Şəkil tapılmadı.", statusCode: StatusCodes.Status404NotFound)
            : Features.Questions.QuestionVisuals.File(storage, response, visual.StorageKey, visual.ContentType);
    }

    private static async Task<IResult> Reprocess(
        Guid id, ClaimsPrincipal user, AppDbContext db, ProcessingQueue queue, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UploadedByUserId == user.GetUserId(), ct);
        if (document is null)
        {
            return NotFound();
        }

        if (document.Status is DocumentStatus.Pending or DocumentStatus.Processing)
        {
            return TypedResults.Problem("Sənəd hazırda emal olunur.", statusCode: StatusCodes.Status409Conflict);
        }

        document.Status = DocumentStatus.Pending;
        document.ErrorMessage = null;
        await db.SaveChangesAsync(ct);
        queue.Enqueue(document.Id);

        return TypedResults.Accepted($"/api/documents/{id}", await FindAsync(db, user, id, ct));
    }

    private static async Task<IResult> Delete(
        Guid id, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UploadedByUserId == user.GetUserId(), ct);
        if (document is null)
        {
            return NotFound();
        }

        if (document.Status == DocumentStatus.Processing)
        {
            return TypedResults.Problem("Sənəd hazırda emal olunur, sonra yenidən cəhd edin.", statusCode: StatusCodes.Status409Conflict);
        }

        // Visuals are removed explicitly (deleted before the questions they reference); questions and options cascade.
        var visuals = await db.QuestionVisuals.Where(v => v.DocumentId == id).ToListAsync(ct);
        db.QuestionVisuals.RemoveRange(visuals);
        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);
        storage.Delete(document.StoredFileName);

        foreach (var key in visuals.Where(v => v.StorageKey is not null).Select(v => v.StorageKey!).Distinct())
        {
            try
            {
                storage.Delete(key);
            }
            catch (Exception)
            {
                // Best effort: the document is gone; an orphaned image file is harmless and unreachable.
            }
        }

        return TypedResults.NoContent();
    }

    // Filter before projecting: EF cannot translate predicates on the constructed DTO.
    // DİM sources are included: the DİM admin page polls its upload's parsing status here.
    private static Task<DocumentDto?> FindAsync(AppDbContext db, ClaimsPrincipal user, Guid id, CancellationToken ct) =>
        Project(DocumentAccess.ReadableWithDimSources(db, user.GetUserId()).Where(d => d.Id == id), user, db).FirstOrDefaultAsync(ct);

    private static IQueryable<DocumentDto> Project(IQueryable<UploadedDocument> query, ClaimsPrincipal user, AppDbContext db)
    {
        var userId = user.GetUserId();
        return query.Select(d => new DocumentDto(
            d.Id,
            d.OriginalFileName,
            d.FileSizeBytes,
            d.PageCount,
            d.Status,
            new UserSummary(d.UploadedByUserId, d.UploadedBy.DisplayName),
            d.UploadedByUserId == userId,
            d.Questions.Count,
            d.ProcessingReport,
            d.ErrorMessage,
            d.CreatedAt,
            d.ProcessedAt,
            d.ShareWithEveryone ? "Everyone" : db.DocumentUserShares.Any(s => s.DocumentId == d.Id)
                ? (db.DocumentGroupShares.Any(s => s.DocumentId == d.Id) ? "UsersAndGroups" : "Users")
                : db.DocumentGroupShares.Any(s => s.DocumentId == d.Id) ? "Groups" : "Private"));
    }

    private static bool HasPdfSignature(MemoryStream buffer) =>
        buffer.Length >= 5 && buffer.GetBuffer().AsSpan(0, 5).SequenceEqual("%PDF-"u8);

    private static string SafeDisplayName(string fileName)
    {
        var name = Path.GetFileName(fileName).Trim();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        return name.Length > 255 ? name[^255..] : name;
    }

    private static IResult Invalid(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);

    private static IResult NotFound() => TypedResults.Problem("Sənəd tapılmadı.", statusCode: StatusCodes.Status404NotFound);
}

public sealed record UserSummary(Guid Id, string DisplayName);

public sealed record DocumentDto(
    Guid Id,
    string OriginalFileName,
    long FileSizeBytes,
    int? PageCount,
    DocumentStatus Status,
    UserSummary UploadedBy,
    bool CanEdit,
    int QuestionCount,
    ProcessingReport? ProcessingReport,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt, string SharingStatus);

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
    private static readonly string[] PdfContentTypes = ["application/pdf", "application/x-pdf"];

    public static void MapDocumentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/documents").RequireAuthorization();

        // Auth cookie is SameSite=Strict, which blocks cross-site form posts; no antiforgery token needed.
        group.MapPost("/", Upload).DisableAntiforgery();
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/{id:guid}/reprocess", Reprocess);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Upload(
        IFormFile? file,
        ClaimsPrincipal user,
        AppDbContext db,
        IFileStorage storage,
        ProcessingQueue queue,
        IOptions<DocumentOptions> options,
        CancellationToken ct)
    {
        var limits = options.Value;
        if (file is null || file.Length == 0)
        {
            return Invalid("PDF faylı seçilməyib.");
        }

        if (file.Length > limits.MaxFileSizeBytes)
        {
            return TypedResults.Problem($"Fayl {limits.MaxFileSizeBytes / 1024 / 1024} MB-dan böyük ola bilməz.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !PdfContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Invalid("Yalnız PDF faylları qəbul edilir.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        if (!HasPdfSignature(buffer))
        {
            return Invalid("Fayl düzgün PDF deyil.");
        }

        var sha256 = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        var duplicate = await db.Documents.Where(d => d.Sha256 == sha256).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (duplicate is not null)
        {
            return TypedResults.Problem("Bu PDF artıq yüklənib.", statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["documentId"] = duplicate });
        }

        buffer.Position = 0;
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

        var document = new UploadedDocument
        {
            OriginalFileName = SafeDisplayName(file.FileName),
            FileSizeBytes = file.Length,
            Sha256 = sha256,
            PageCount = inspection.PageCount,
            Status = DocumentStatus.Pending,
            UploadedByUserId = user.GetUserId(),
        };
        db.Documents.Add(document); // Assigns the sequential Guid key client-side.
        document.StoredFileName = $"{document.Id:N}.pdf";

        buffer.Position = 0;
        await storage.SaveAsync(document.StoredFileName, buffer, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            storage.Delete(document.StoredFileName);
            throw;
        }

        queue.Enqueue(document.Id);

        var dto = await FindAsync(db, user, document.Id, ct);
        return TypedResults.Accepted($"/api/documents/{document.Id}", dto);
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct,
        bool? mine = null, DocumentStatus? status = null, int? page = null, int? pageSize = null)
    {
        var (pageNumber, size) = Paging.Normalize(page, pageSize);
        var query = db.Documents.AsNoTracking();

        if (mine == true)
        {
            var userId = user.GetUserId();
            query = query.Where(d => d.UploadedByUserId == userId);
        }

        if (status is not null)
        {
            query = query.Where(d => d.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(d => d.CreatedAt), user)
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

    private static async Task<IResult> Reprocess(
        Guid id, ClaimsPrincipal user, AppDbContext db, ProcessingQueue queue, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document is null)
        {
            return NotFound();
        }

        if (!user.CanEdit(document.UploadedByUserId))
        {
            return Forbidden();
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
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document is null)
        {
            return NotFound();
        }

        if (!user.CanEdit(document.UploadedByUserId))
        {
            return Forbidden();
        }

        if (document.Status == DocumentStatus.Processing)
        {
            return TypedResults.Problem("Sənəd hazırda emal olunur, sonra yenidən cəhd edin.", statusCode: StatusCodes.Status409Conflict);
        }

        db.Documents.Remove(document); // Questions and options cascade in the database.
        await db.SaveChangesAsync(ct);
        storage.Delete(document.StoredFileName);

        return TypedResults.NoContent();
    }

    // Filter before projecting: EF cannot translate predicates on the constructed DTO.
    private static Task<DocumentDto?> FindAsync(AppDbContext db, ClaimsPrincipal user, Guid id, CancellationToken ct) =>
        Project(db.Documents.AsNoTracking().Where(d => d.Id == id), user).FirstOrDefaultAsync(ct);

    private static IQueryable<DocumentDto> Project(IQueryable<UploadedDocument> query, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var isAdmin = user.IsAdmin();
        return query.Select(d => new DocumentDto(
            d.Id,
            d.OriginalFileName,
            d.FileSizeBytes,
            d.PageCount,
            d.Status,
            new UserSummary(d.UploadedByUserId, d.UploadedBy.DisplayName),
            isAdmin || d.UploadedByUserId == userId,
            d.Questions.Count,
            d.ProcessingReport,
            d.ErrorMessage,
            d.CreatedAt,
            d.ProcessedAt));
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

    private static IResult Forbidden() =>
        TypedResults.Problem("Bu sənədi yalnız onu yükləyən istifadəçi və ya Admin dəyişə bilər.", statusCode: StatusCodes.Status403Forbidden);
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
    DateTimeOffset? ProcessedAt);

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Dim;

public sealed record DimAudioDto(Guid Id, string Title, int QuestionFrom, int QuestionTo, string ContentType, long SizeBytes, DateTimeOffset CreatedAt);

public sealed class DimAudioSettingsRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Range(1, 10_000)]
    public int QuestionFrom { get; init; }

    [Range(1, 10_000)]
    public int QuestionTo { get; init; }
}

/// <summary>
/// Listening recordings of DİM documents (admin). A recording covers a range of question numbers of its document;
/// ranges of one document may not overlap, so every question has at most one recording.
/// </summary>
public static class DimAudioEndpoints
{
    public static void MapDimAudioEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/dim").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapGet("/documents/{documentId:guid}/audio", List);
        group.MapPost("/documents/{documentId:guid}/audio", Upload).DisableAntiforgery()
            .RequireRateLimiting(AuthenticationSetup.UploadRateLimit)
            // Raises the body limit for this endpoint only; every other route keeps the global Documents limit.
            .WithMetadata(new RequestSizeLimitAttribute(DimAudioFormat.MaxSizeBytes + 1024 * 1024))
            .WithFormOptions(multipartBodyLengthLimit: DimAudioFormat.MaxSizeBytes + 1024 * 1024);
        group.MapPut("/audio/{id:guid}", Update).Validate<DimAudioSettingsRequest>();
        group.MapDelete("/audio/{id:guid}", Delete);
        group.MapGet("/audio/{id:guid}/file", File);
    }

    private static async Task<IResult> List(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");
        var audios = await db.DimAudios.AsNoTracking().Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.QuestionFrom).ToListAsync(ct);
        return TypedResults.Ok(audios.Select(ToDto).ToList());
    }

    private static async Task<IResult> Upload(
        Guid documentId, IFormFile? file, [FromForm] string? title, [FromForm] int questionFrom, [FromForm] int questionTo,
        ClaimsPrincipal user, AppDbContext db, IFileStorage storage, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");
        if (file is null || file.Length == 0) return Problem(StatusCodes.Status400BadRequest, "Səs faylı seçilməyib.");
        if (file.Length > DimAudioFormat.MaxSizeBytes)
            return Problem(StatusCodes.Status413PayloadTooLarge, $"Səs faylı {DimAudioFormat.MaxSizeBytes / 1024 / 1024} MB-dan böyük ola bilməz.");

        var name = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim();
        if (name.Length > 200) name = name[..200];
        if (await RangeError(db, documentId, null, questionFrom, questionTo, ct) is { } rangeError)
            return Problem(StatusCodes.Status400BadRequest, rangeError);

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        if (DimAudioFormat.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16))) is not { } format)
            return Problem(StatusCodes.Status400BadRequest, "Yalnız MP3, M4A, OGG və WAV səs faylları qəbul edilir.");

        var audio = new DimAudio
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Title = name.Length > 0 ? name : "Dinləmə",
            QuestionFrom = questionFrom,
            QuestionTo = questionTo,
            ContentType = format.ContentType,
            SizeBytes = file.Length,
            UploadedByUserId = userId,
            CreatedAt = clock.GetUtcNow(),
        };
        audio.StorageKey = $"dimaudio_{audio.Id:N}{format.Extension}";
        buffer.Position = 0;
        await storage.SaveAsync(audio.StorageKey, buffer, ct);
        db.DimAudios.Add(audio);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(audio.StorageKey);
            throw;
        }

        return TypedResults.Created($"/api/admin/dim/audio/{audio.Id}", ToDto(audio));
    }

    private static async Task<IResult> Update(Guid id, DimAudioSettingsRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var audio = await db.DimAudios.FirstOrDefaultAsync(a => a.Id == id && a.Document.UploadedByUserId == userId, ct);
        if (audio is null) return Problem(StatusCodes.Status404NotFound, "Səs faylı tapılmadı.");
        if (await RangeError(db, audio.DocumentId, id, request.QuestionFrom, request.QuestionTo, ct) is { } rangeError)
            return Problem(StatusCodes.Status400BadRequest, rangeError);

        audio.Title = request.Title.Trim();
        audio.QuestionFrom = request.QuestionFrom;
        audio.QuestionTo = request.QuestionTo;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(audio));
    }

    /// <summary>Removes the recording; exams already started keep their link but can no longer play it.</summary>
    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var audio = await db.DimAudios.FirstOrDefaultAsync(a => a.Id == id && a.Document.UploadedByUserId == userId, ct);
        if (audio is null) return Problem(StatusCodes.Status404NotFound, "Səs faylı tapılmadı.");
        db.DimAudios.Remove(audio);
        await db.SaveChangesAsync(ct);
        DeleteFile(storage, audio.StorageKey);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> File(Guid id, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, HttpResponse response, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var audio = await db.DimAudios.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.Document.UploadedByUserId == userId, ct);
        return Stream(storage, response, audio);
    }

    /// <summary>Streams a recording with range support, so the player can seek and Safari can play it at all.</summary>
    internal static IResult Stream(IFileStorage storage, HttpResponse response, DimAudio? audio)
    {
        if (audio is null) return Problem(StatusCodes.Status404NotFound, "Səs faylı tapılmadı.");
        try
        {
            var stream = storage.OpenRead(audio.StorageKey);
            response.Headers.CacheControl = "private, max-age=3600";
            return TypedResults.Stream(stream, audio.ContentType, enableRangeProcessing: true);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Problem(StatusCodes.Status404NotFound, "Səs faylı tapılmadı.");
        }
    }

    /// <summary>Best effort: the row is gone, an orphaned file is harmless and unreachable.</summary>
    internal static void DeleteFile(IFileStorage storage, string key)
    {
        try { storage.Delete(key); }
        catch (Exception) { }
    }

    private static async Task<string?> RangeError(AppDbContext db, Guid documentId, Guid? exceptId, int from, int to, CancellationToken ct)
    {
        if (from < 1 || to < from || to > 10_000) return "Sual aralığını düzgün yazın (məs. 1–5).";
        var overlap = await db.DimAudios.AsNoTracking()
            .Where(a => a.DocumentId == documentId && a.Id != exceptId && a.QuestionFrom <= to && a.QuestionTo >= from)
            .Select(a => a.Title).FirstOrDefaultAsync(ct);
        return overlap is null ? null : $"Bu aralıq \"{overlap}\" səs faylının aralığı ilə üst-üstə düşür.";
    }

    private static DimAudioDto ToDto(DimAudio a) => new(a.Id, a.Title, a.QuestionFrom, a.QuestionTo, a.ContentType, a.SizeBytes, a.CreatedAt);

    private static IResult Problem(int status, string detail) => TypedResults.Problem(detail, statusCode: status);
}

using System.Security.Claims;
using System.Text.Json;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Study;

public static partial class StudyEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void MapStudyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/study").RequireAuthorization();
        group.MapGet("/documents", StudyDocuments);
        group.MapGet("/{documentId:guid}/progress", Progress);
        MapPracticeEndpoints(group);
    }

    private static async Task<List<StudySource>> SourcesAsync(AppDbContext db, Guid documentId, CancellationToken ct)
    {
        var sources = await db.Questions.AsNoTracking()
            .Where(q => q.DocumentId == documentId && q.Explanation != null && q.Explanation != "")
            .OrderBy(q => q.SourceNumber).Select(q => new StudySource(q.Id, q.SourceNumber, q.Text, q.Explanation!)).ToListAsync(ct);
        return sources.Where(s => !string.IsNullOrWhiteSpace(s.Text) && !string.IsNullOrWhiteSpace(s.Answer)).ToList();
    }

    private static async Task<IResult> StudyDocuments(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await DocumentAccess.Readable(db, user.GetUserId()).Where(d => d.Status == DocumentStatus.Completed
                && d.Questions.Any(q => q.Explanation != null && q.Explanation != ""))
            .OrderByDescending(d => d.CreatedAt).Select(d => new StudyDocumentDto(d.Id, d.OriginalFileName,
                d.Questions.Count(q => q.Explanation != null && q.Explanation != ""))).ToListAsync(ct));

    private static IQueryable<StudyProgress> ProgressQuery(AppDbContext db, Guid userId, Guid documentId) =>
        db.Questions.AsNoTracking().Where(q => q.DocumentId == documentId && q.Explanation != null && q.Explanation != "")
            .OrderBy(q => q.SourceNumber).Select(q => new StudyProgress(q.Id, q.SourceNumber, q.Text,
                db.StudyAttempts.Count(a => a.UserId == userId && a.QuestionId == q.Id && a.Score != null),
                db.StudyAttempts.Where(a => a.UserId == userId && a.QuestionId == q.Id && a.Score != null)
                    .OrderByDescending(a => a.CompletedAt).Select(a => a.Score).FirstOrDefault(),
                db.StudyAttempts.Where(a => a.UserId == userId && a.QuestionId == q.Id && a.Score != null)
                    .OrderByDescending(a => a.CompletedAt).Select(a => a.DueAt).FirstOrDefault()));

    private static Task<bool> DocumentExists(AppDbContext db, Guid id, Guid userId, CancellationToken ct) =>
        DocumentAccess.Readable(db, userId).AnyAsync(d => d.Id == id && d.Status == DocumentStatus.Completed
            && d.Questions.Any(q => q.Explanation != null && q.Explanation != ""), ct);

    private static async Task<bool> InActiveExam(AppDbContext db, Guid userId, Guid documentId, CancellationToken ct)
    {
        var documents = await db.Exams.AsNoTracking().Where(e => e.UserId == userId && e.Status == ExamStatus.InProgress)
            .Select(e => e.SourceDocumentIds).ToListAsync(ct);
        return documents.Any(ids => ids.Count == 0 || ids.Contains(documentId));
    }

    private static async Task<IResult> Progress(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!await DocumentExists(db, documentId, user.GetUserId(), ct)) return TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
        return TypedResults.Ok(await ProgressQuery(db, user.GetUserId(), documentId).ToListAsync(ct));
    }

}

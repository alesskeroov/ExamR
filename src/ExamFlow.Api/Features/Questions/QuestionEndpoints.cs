using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Features.Documents;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Questions;

/// <summary>Read-only question bank listing. Editing endpoints come in Phase 4.</summary>
public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/questions").RequireAuthorization();
        group.MapGet("/", List);
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct,
        Guid? documentId = null, VerificationStatus? status = null, bool? hasWarning = null,
        int? page = null, int? pageSize = null)
    {
        var (pageNumber, size) = Paging.Normalize(page, pageSize);
        var userId = user.GetUserId();
        if (documentId is not null && !await DocumentAccess.Readable(db, userId)
            .AnyAsync(d => d.Id == documentId, ct))
            return TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
        var readable = DocumentAccess.Readable(db, userId);
        var query = db.Questions.AsNoTracking().Where(q => readable.Any(d => d.Id == q.DocumentId));

        if (documentId is not null)
        {
            query = query.Where(q => q.DocumentId == documentId);
        }

        if (status is not null)
        {
            query = query.Where(q => q.VerificationStatus == status);
        }

        if (hasWarning is not null)
        {
            query = hasWarning.Value ? query.Where(q => q.ParseWarning != null) : query.Where(q => q.ParseWarning == null);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(q => q.DocumentId).ThenBy(q => q.SourceNumber)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(q => new QuestionDto(
                q.Id,
                q.DocumentId,
                q.SourceNumber,
                q.Text,
                q.VerificationStatus,
                q.ParseWarning,
                q.Options.OrderBy(o => o.Position).Select(o => new AnswerOptionDto(o.Id, o.Label, o.Text, o.IsCorrect)).ToList()))
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResult<QuestionDto>(items, pageNumber, size, total));
    }
}

public sealed record AnswerOptionDto(Guid Id, string Label, string Text, bool IsCorrect);

public sealed record QuestionDto(
    Guid Id,
    Guid DocumentId,
    int SourceNumber,
    string Text,
    VerificationStatus VerificationStatus,
    string? ParseWarning,
    IReadOnlyList<AnswerOptionDto> Options);

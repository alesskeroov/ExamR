using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Learning;

public sealed class CardReviewRequest
{
    [Required] public CardGrade? Grade { get; init; }
}

/// <summary>One flashcard: the document's own question and answer. IsNew: never reviewed by this student.</summary>
public sealed record CardDto(int Number, string Question, string Answer, int? Page, bool SharedAnswer, int Box, bool IsNew, DateTimeOffset? DueAt);
/// <summary>Counts over the whole document; Cards = the next study batch (due first, then new ones). NextDueAt: when the next card becomes due.</summary>
public sealed record CardDeckDto(int Total, int New, int Due, int Learning, int Learned, DateTimeOffset? NextDueAt, string? Warning, IReadOnlyList<CardDto> Cards);
public sealed record CardStateDto(int Number, int Box, DateTimeOffset DueAt);

/// <summary>"Kartlar": spaced repetition over the document's own open questions. No AI call and no AI quota.</summary>
public static partial class LearningStudyEndpoints
{
    private static void MapCards(RouteGroupBuilder group)
    {
        group.MapGet("/chats/{sessionId:guid}/cards", Cards);
        group.MapPost("/chats/{sessionId:guid}/cards/{number:int}/review", ReviewCard).Validate<CardReviewRequest>();
    }

    /// <param name="ahead">"Yenə də məşq et": cards that are not due yet, soonest first (when nothing is due today).</param>
    private static async Task<IResult> Cards(Guid sessionId, ClaimsPrincipal user, AppDbContext db, OpenQuestionIndexer indexer, TimeProvider clock,
        [FromQuery] bool? ahead, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;

        var states = await db.Set<LearningCardState>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId)
            .Select(c => new { c.QuestionHash, c.Box, c.DueAt }).ToListAsync(ct);
        var byHash = states.GroupBy(s => s.QuestionHash).ToDictionary(g => g.Key, g => g.First());
        var now = clock.GetUtcNow();

        var cards = index!.Questions.Select(q =>
        {
            var state = byHash.GetValueOrDefault(CardScheduler.Hash(q.Text));
            return new CardDto(q.Number, q.Text, q.Answer, q.Page, q.SharedAnswer, state?.Box ?? 0, state is null, state?.DueAt);
        }).ToList();
        var seen = cards.Where(c => !c.IsNew).ToList();
        var due = seen.Where(c => c.DueAt <= now).OrderBy(c => c.DueAt).ToList();
        var fresh = cards.Where(c => c.IsNew).ToList();

        var batch = ahead == true
            ? seen.Where(c => c.DueAt > now).OrderBy(c => c.DueAt).Take(CardScheduler.BatchSize).ToList()
            : due.Take(CardScheduler.BatchSize).ToList();
        if (ahead != true)
            batch.AddRange(fresh.Take(Math.Min(CardScheduler.NewPerBatch, CardScheduler.BatchSize - batch.Count)));

        return TypedResults.Ok(new CardDeckDto(cards.Count, fresh.Count, due.Count,
            seen.Count(c => c.Box < CardScheduler.LearnedBox), seen.Count(c => c.Box >= CardScheduler.LearnedBox),
            seen.Where(c => c.DueAt > now).Min(c => c.DueAt), index.Warning, batch));
    }

    private static async Task<IResult> ReviewCard(Guid sessionId, int number, CardReviewRequest request, ClaimsPrincipal user, AppDbContext db,
        OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;
        if (index!.Questions.FirstOrDefault(q => q.Number == number) is not { } question)
            return TypedResults.Problem("Bu sual sənəddə tapılmadı.", statusCode: StatusCodes.Status404NotFound);

        var hash = CardScheduler.Hash(question.Text);
        var now = clock.GetUtcNow();
        var grade = request.Grade!.Value;
        for (var attempt = 0; ; attempt++)
        {
            var state = await db.Set<LearningCardState>()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId && c.QuestionHash == hash, ct);
            if (state is null)
            {
                state = new LearningCardState { Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc!.LearningId, QuestionHash = hash, CreatedAt = now };
                db.Add(state);
            }
            (state.Box, state.DueAt) = CardScheduler.Next(state.Box, grade, now);
            state.Reviews++;
            if (grade == CardGrade.Again && state.Reviews > 1) state.Lapses++;
            state.LastGrade = grade;
            state.LastReviewedAt = now;
            try
            {
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CardStateDto(number, state.Box, state.DueAt));
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A parallel first review created the row: apply this grade on top of it.
                db.ChangeTracker.Clear();
            }
        }
    }
}

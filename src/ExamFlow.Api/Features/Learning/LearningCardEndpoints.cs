using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Learning;

public sealed class CardReviewRequest
{
    [Required] public CardGrade? Grade { get; init; }
}

/// <summary>One flashcard: the document's own question and answer with the student's state. Weak: last answered "Bilmirdim" or "Qismən".</summary>
public sealed record CardDto(int Number, string Question, string Answer, int? Page, int ChunkOrdinal, bool SharedAnswer, string TopicKey,
    bool Seen, int Streak, bool Mastered, bool Weak);
public sealed record CardTopicDto(string Key, string Title, IReadOnlyList<int> Numbers);
/// <summary>The whole private deck of one document: topics in document order and every card. Required = "Bilirdim" in a row to master a card.</summary>
public sealed record CardDeckDto(string? Warning, int Required, IReadOnlyList<CardTopicDto> Topics, IReadOnlyList<CardDto> Cards);
public sealed record CardStateDto(int Number, int Streak, bool Mastered, bool Weak);

/// <summary>Öyrənmə bələdçisi: cards from the document's own open questions. No AI call, no AI quota; each student sees only their own progress.</summary>
public static partial class LearningStudyEndpoints
{
    private static void MapCards(RouteGroupBuilder group)
    {
        group.MapGet("/chats/{sessionId:guid}/deck", Deck);
        group.MapPost("/chats/{sessionId:guid}/cards/{number:int}/review", ReviewCard).Validate<CardReviewRequest>();
    }

    private static async Task<IResult> Deck(Guid sessionId, ClaimsPrincipal user, AppDbContext db, OpenQuestionIndexer indexer, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;

        var sections = await db.Set<LearningDocumentChunk>().AsNoTracking().Where(c => c.LearningDocumentId == doc!.LearningId && c.Section != null)
            .Select(c => new { c.Ordinal, c.Section }).ToDictionaryAsync(c => c.Ordinal, c => c.Section, ct);
        var topics = CardTopics.Build(index!.Questions, q => sections.GetValueOrDefault(q.ChunkOrdinal));
        var topicOf = topics.SelectMany(t => t.Numbers.Select(n => (n, t.Key))).ToDictionary(x => x.n, x => x.Key);

        var states = await db.Set<LearningCardState>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId)
            .Select(c => new { c.QuestionHash, c.Streak, c.Mastered, c.LastGrade }).ToListAsync(ct);
        var byHash = states.GroupBy(s => s.QuestionHash).ToDictionary(g => g.Key, g => g.First());

        var cards = index.Questions.Select(q =>
        {
            var s = byHash.GetValueOrDefault(CardMastery.Hash(q.Text));
            return new CardDto(q.Number, q.Text, q.Answer, q.Page, q.ChunkOrdinal, q.SharedAnswer, topicOf[q.Number],
                s is not null, s?.Streak ?? 0, s?.Mastered ?? false, s is not null && s.LastGrade != CardGrade.Good);
        }).ToList();
        return TypedResults.Ok(new CardDeckDto(index.Warning, CardMastery.Required,
            topics.Select(t => new CardTopicDto(t.Key, t.Title, t.Numbers)).ToList(), cards));
    }

    private static async Task<IResult> ReviewCard(Guid sessionId, int number, CardReviewRequest request, ClaimsPrincipal user, AppDbContext db,
        OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;
        if (index!.Questions.FirstOrDefault(q => q.Number == number) is not { } question)
            return TypedResults.Problem("Bu sual sənəddə tapılmadı.", statusCode: StatusCodes.Status404NotFound);

        var hash = CardMastery.Hash(question.Text);
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
            (state.Streak, state.Mastered) = CardMastery.Next(state.Streak, grade);
            state.Reviews++;
            if (grade == CardGrade.Again) state.Lapses++;
            state.LastGrade = grade;
            state.LastReviewedAt = now;
            try
            {
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CardStateDto(number, state.Streak, state.Mastered, grade != CardGrade.Good));
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A parallel first review created the row: apply this grade on top of it.
                db.ChangeTracker.Clear();
            }
        }
    }
}

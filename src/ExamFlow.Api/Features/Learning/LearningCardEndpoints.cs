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

/// <summary>
/// One flashcard with the student's state. Kind: Question, Term, List or Test. Items: list items (List), terms explained in the
/// answer (Question) or the explanation (Test); Others: wrong test options. Weak: last answered "Bilmirdim" or "Qismən".
/// </summary>
public sealed record CardDto(string Id, string Kind, string TopicKey, string Front, string Back, IReadOnlyList<string> Items, IReadOnlyList<string> Others,
    int Source, int? Page, int ChunkOrdinal, bool Seen, int Streak, bool Mastered, bool Weak, bool IsSection, string? Section);
public sealed record CardTopicDto(string Key, string Title, string? Notes);
/// <summary>The whole private deck of one document. Required = "Bilirdim" in a row to master a card.</summary>
public sealed record CardDeckDto(string? Warning, int Required, IReadOnlyList<CardTopicDto> Topics, IReadOnlyList<CardDto> Cards);
public sealed record CardStateDto(string Id, int Streak, bool Mastered, bool Weak);

/// <summary>Öyrənmə bələdçisi: cards built from the document itself. No AI call, no AI quota; each student sees only their own progress.</summary>
public static partial class LearningStudyEndpoints
{
    private static void MapCards(RouteGroupBuilder group)
    {
        group.MapGet("/chats/{sessionId:guid}/deck", Deck);
        group.MapPost("/chats/{sessionId:guid}/cards/{cardId}/review", ReviewCard).Validate<CardReviewRequest>();
    }

    private static async Task<(StudyDoc? Doc, StudyDeck? Deck, IResult? Error)> DeckAsync(AppDbContext db, CardDeckService decks, Guid userId, Guid sessionId,
        CancellationToken ct)
    {
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return (null, null, NotFound());
        var (deck, error) = await decks.GetAsync(doc.LearningId, ct);
        return deck is null ? (null, null, TypedResults.Problem(error, statusCode: StatusCodes.Status422UnprocessableEntity)) : (doc, deck, null);
    }

    private static async Task<IResult> Deck(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CardDeckService decks, OpenQuestionIndexer indexer,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;

        var states = await db.Set<LearningCardState>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId)
            .Select(c => new { c.QuestionHash, c.Streak, c.Mastered, c.LastGrade }).ToListAsync(ct);
        var byId = states.GroupBy(s => s.QuestionHash).ToDictionary(g => g.Key, g => g.First());
        var cards = deck!.Cards.Select(c =>
        {
            var s = byId.GetValueOrDefault(c.Id);
            return new CardDto(c.Id, c.Kind.ToString(), c.TopicKey, c.Front, c.Back, c.Items, c.Others, c.Source, c.Page, c.ChunkOrdinal,
                s is not null, s?.Streak ?? 0, s?.Mastered ?? false, s is not null && s.LastGrade != CardGrade.Good, c.IsSection, c.Section);
        }).ToList();
        var warning = (await indexer.GetAsync(doc!.LearningId, ct)) is { Supported: true } index ? index.Warning : null;
        return TypedResults.Ok(new CardDeckDto(warning, CardMastery.Required, deck.Topics.Select(t => new CardTopicDto(t.Key, t.Title, t.Notes)).ToList(), cards));
    }

    private static async Task<IResult> ReviewCard(Guid sessionId, string cardId, CardReviewRequest request, ClaimsPrincipal user, AppDbContext db,
        CardDeckService decks, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;
        if (!deck!.Cards.Any(c => c.Id == cardId))
            return TypedResults.Problem("Bu kart sənəddə tapılmadı.", statusCode: StatusCodes.Status404NotFound);

        var now = clock.GetUtcNow();
        var grade = request.Grade!.Value;
        for (var attempt = 0; ; attempt++)
        {
            var state = await db.Set<LearningCardState>()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId && c.QuestionHash == cardId, ct);
            if (state is null)
            {
                state = new LearningCardState { Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc!.LearningId, QuestionHash = cardId, CreatedAt = now };
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
                return TypedResults.Ok(new CardStateDto(cardId, state.Streak, state.Mastered, grade != CardGrade.Good));
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A parallel first review created the row: apply this grade on top of it.
                db.ChangeTracker.Clear();
            }
        }
    }
}

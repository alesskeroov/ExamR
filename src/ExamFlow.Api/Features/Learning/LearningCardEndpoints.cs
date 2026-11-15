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

/// <summary>A card the student writes or rewrites: question in front, answer behind. TopicKey only when creating.</summary>
public sealed class CardEditRequest
{
    [Required(ErrorMessage = "Kartın ön üzünü yazın.")]
    [StringLength(LearningUserCard.MaxFront, MinimumLength = 2, ErrorMessage = "Ön üz 2–500 simvol olmalıdır.")]
    public string? Front { get; init; }

    [Required(ErrorMessage = "Kartın arxa üzünü yazın.")]
    [StringLength(LearningUserCard.MaxBack, MinimumLength = 1, ErrorMessage = "Arxa üz ən çox 4000 simvol ola bilər.")]
    public string? Back { get; init; }

    [StringLength(64)] public string? TopicKey { get; init; }
}

public sealed class CardHideRequest
{
    [Required] public bool? Hidden { get; init; }
}

/// <summary>
/// One flashcard with the student's state. Kind: Question, Term, List or Test. Items: list items (List), terms explained in the
/// answer (Question) or the explanation (Test); Others: wrong test options. Weak: last answered "Bilmirdim" or "Qismən".
/// </summary>
public sealed record CardDto(string Id, string Kind, string TopicKey, string Front, string Back, IReadOnlyList<string> Items, IReadOnlyList<string> Others,
    int Source, int? Page, int ChunkOrdinal, bool Seen, int Streak, bool Mastered, bool Weak, bool IsSection, string? Section,
    bool NeedsReview, bool Own, bool Edited, bool Hidden, bool Due, DateTimeOffset? DueAt);
public sealed record CardTopicDto(string Key, string Title, string? Notes);
/// <summary>The whole private deck of one document. Required = "Bilirdim" in a row to master a card.</summary>
public sealed record CardDeckDto(string? Warning, int Required, IReadOnlyList<CardTopicDto> Topics, IReadOnlyList<CardDto> Cards);
/// <summary>Due: the card is waiting for review now (spaced repetition); DueAt: when it comes back.</summary>
public sealed record CardStateDto(string Id, int Streak, bool Mastered, bool Weak, bool Due, DateTimeOffset? DueAt);

/// <summary>A student's state of one card as the DTOs need it.</summary>
/// <summary>Cards of one document waiting for review; ChatId = the student's guide entry for it, when one exists.</summary>
public sealed record DueDocumentDto(Guid DocumentId, Guid? ChatId, string Name, int Due);
public sealed record ReviewTodayDto(int Goal, int ReviewedToday, int Due, IReadOnlyList<DueDocumentDto> Documents);

internal sealed record CardStateView(int Streak, bool Mastered, CardGrade LastGrade, DateTimeOffset? DueAt, DateTimeOffset LastReviewedAt);

/// <summary>Öyrənmə bələdçisi: cards built from the document itself. No AI call, no AI quota; each student sees only their own progress.</summary>
public static partial class LearningStudyEndpoints
{
    private static void MapCards(RouteGroupBuilder group)
    {
        group.MapGet("/chats/{sessionId:guid}/deck", Deck);
        group.MapPost("/chats/{sessionId:guid}/cards/{cardId}/review", ReviewCard).Validate<CardReviewRequest>();
        group.MapPost("/chats/{sessionId:guid}/cards", CreateCard).Validate<CardEditRequest>();
        group.MapPut("/chats/{sessionId:guid}/cards/{cardId}", EditCard).Validate<CardEditRequest>();
        group.MapPut("/chats/{sessionId:guid}/cards/{cardId}/hidden", HideCard).Validate<CardHideRequest>();
        group.MapDelete("/chats/{sessionId:guid}/cards/{cardId}", DeleteCard);
        group.MapGet("/review/today", ReviewToday);
    }

    private static async Task<(StudyDoc? Doc, StudyDeck? Deck, IResult? Error)> DeckAsync(AppDbContext db, CardDeckService decks, Guid userId, Guid sessionId,
        CancellationToken ct)
    {
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return (null, null, NotFound());
        var (deck, error) = await decks.GetAsync(doc.LearningId, ct);
        return deck is null ? (null, null, TypedResults.Problem(error, statusCode: StatusCodes.Status422UnprocessableEntity)) : (doc, deck, null);
    }

    private static async Task<IResult> Deck(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CardDeckService decks, OpenQuestionIndexer indexer, TimeProvider clock,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;

        var states = await db.Set<LearningCardState>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId)
            .Select(c => new { c.QuestionHash, View = new CardStateView(c.Streak, c.Mastered, c.LastGrade, c.DueAt, c.LastReviewedAt) }).ToListAsync(ct);
        var byId = states.GroupBy(s => s.QuestionHash).ToDictionary(g => g.Key, g => g.First().View);
        var now = clock.GetUtcNow();
        var personal = await PersonalAsync(db, userId, doc!.LearningId, deck!, ct);
        var cards = personal.Select(p => ToDto(p, byId.GetValueOrDefault(p.Card.Id), now)).ToList();
        var topics = deck!.Topics.Select(t => new CardTopicDto(t.Key, t.Title, t.Notes)).ToList();
        if (cards.Any(c => c.TopicKey == PersonalDeck.OwnTopicKey)) topics.Add(new(PersonalDeck.OwnTopicKey, PersonalDeck.OwnTopicTitle, null));
        var warning = (await indexer.GetAsync(doc.LearningId, ct)) is { Supported: true } index ? index.Warning : null;
        return TypedResults.Ok(new CardDeckDto(warning, CardMastery.Required, topics, cards));
    }

    private static CardDto ToDto(PersonalCard p, CardStateView? s, DateTimeOffset now)
    {
        var c = p.Card;
        return new CardDto(c.Id, c.Kind.ToString(), c.TopicKey, c.Front, c.Back, c.Items, c.Others, c.Source, c.Page, c.ChunkOrdinal,
            s is not null, s?.Streak ?? 0, s?.Mastered ?? false, s is not null && s.LastGrade != CardGrade.Good, c.IsSection, c.Section,
            c.NeedsReview, p.Own, p.Edited, p.Hidden, s is not null && !p.Hidden && CardSchedule.IsDue(s.DueAt, s.LastReviewedAt, now), s?.DueAt);
    }

    /// <summary>The document's deck as this student sees it (their edits, hidden and deleted cards, own cards).</summary>
    private static async Task<List<PersonalCard>> PersonalAsync(AppDbContext db, Guid userId, Guid learningId, StudyDeck deck, CancellationToken ct)
    {
        var overrides = await db.Set<LearningCardOverride>().AsNoTracking().Where(o => o.UserId == userId && o.LearningDocumentId == learningId).ToListAsync(ct);
        var own = await db.Set<LearningUserCard>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == learningId).ToListAsync(ct);
        return PersonalDeck.Merge(deck, overrides, own);
    }

    private static IResult CardNotFound() => TypedResults.Problem("Bu kart sənəddə tapılmadı.", statusCode: StatusCodes.Status404NotFound);

    private static Guid? OwnId(string cardId) =>
        cardId.StartsWith(LearningUserCard.IdPrefix, StringComparison.Ordinal) && Guid.TryParseExact(cardId[LearningUserCard.IdPrefix.Length..], "N", out var id) ? id : null;

    private static async Task<IResult> CreateCard(Guid sessionId, CardEditRequest request, ClaimsPrincipal user, AppDbContext db, CardDeckService decks,
        TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;
        var count = await db.Set<LearningUserCard>().CountAsync(c => c.UserId == userId && c.LearningDocumentId == doc!.LearningId, ct);
        if (count >= LearningUserCard.MaxPerDocument)
            return TypedResults.Problem($"Bir sənəd üçün ən çox {LearningUserCard.MaxPerDocument} öz kartınız ola bilər.", statusCode: StatusCodes.Status409Conflict);
        var topic = request.TopicKey is { } key && deck!.Topics.Any(t => t.Key == key) ? key : null;
        var now = clock.GetUtcNow();
        var card = new LearningUserCard
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc!.LearningId, TopicKey = topic,
            Front = request.Front!.Trim(), Back = request.Back!.Trim(), CreatedAt = now, UpdatedAt = now,
        };
        db.Add(card);
        await db.SaveChangesAsync(ct);
        var created = PersonalDeck.Merge(new StudyDeck(deck!.Topics, []), [], [card])[0];
        return TypedResults.Ok(ToDto(created, null, now));
    }

    private static async Task<IResult> EditCard(Guid sessionId, string cardId, CardEditRequest request, ClaimsPrincipal user, AppDbContext db,
        CardDeckService decks, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;
        var now = clock.GetUtcNow();
        var front = request.Front!.Trim();
        var back = request.Back!.Trim();
        if (OwnId(cardId) is { } ownId)
        {
            var own = await db.Set<LearningUserCard>().FirstOrDefaultAsync(c => c.Id == ownId && c.UserId == userId && c.LearningDocumentId == doc!.LearningId, ct);
            if (own is null) return CardNotFound();
            (own.Front, own.Back, own.UpdatedAt) = (front, back, now);
        }
        else
        {
            if (!deck!.Cards.Any(c => c.Id == cardId)) return CardNotFound();
            var change = await OverrideAsync(db, userId, doc!.LearningId, cardId, ct);
            if (change.Deleted) return CardNotFound();
            (change.Front, change.Back, change.UpdatedAt) = (front, back, now);
        }
        await db.SaveChangesAsync(ct);
        return await CardResult(db, userId, doc!.LearningId, deck!, cardId, clock.GetUtcNow(), ct);
    }

    private static async Task<IResult> HideCard(Guid sessionId, string cardId, CardHideRequest request, ClaimsPrincipal user, AppDbContext db,
        CardDeckService decks, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;
        // Own cards are deleted, not hidden: hiding is for document cards the student does not want to study.
        if (OwnId(cardId) is not null || !deck!.Cards.Any(c => c.Id == cardId)) return CardNotFound();
        var change = await OverrideAsync(db, userId, doc!.LearningId, cardId, ct);
        if (change.Deleted) return CardNotFound();
        (change.Hidden, change.UpdatedAt) = (request.Hidden!.Value, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await CardResult(db, userId, doc.LearningId, deck, cardId, clock.GetUtcNow(), ct);
    }

    private static async Task<IResult> DeleteCard(Guid sessionId, string cardId, ClaimsPrincipal user, AppDbContext db, CardDeckService decks,
        TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;
        if (OwnId(cardId) is { } ownId)
        {
            var own = await db.Set<LearningUserCard>().FirstOrDefaultAsync(c => c.Id == ownId && c.UserId == userId && c.LearningDocumentId == doc!.LearningId, ct);
            if (own is null) return CardNotFound();
            db.Remove(own);
        }
        else
        {
            if (!deck!.Cards.Any(c => c.Id == cardId)) return CardNotFound();
            // A document card cannot be removed from the document: the student's mark keeps it out of their deck for good.
            var change = await OverrideAsync(db, userId, doc!.LearningId, cardId, ct);
            (change.Deleted, change.Front, change.Back, change.UpdatedAt) = (true, null, null, clock.GetUtcNow());
        }
        await db.SaveChangesAsync(ct);
        await db.Set<LearningCardState>().Where(s => s.UserId == userId && s.LearningDocumentId == doc!.LearningId && s.QuestionHash == cardId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>The student's change row for a document card, created (tracked, unsaved) when there is none yet.</summary>
    private static async Task<LearningCardOverride> OverrideAsync(AppDbContext db, Guid userId, Guid learningId, string cardId, CancellationToken ct)
    {
        var change = await db.Set<LearningCardOverride>().FirstOrDefaultAsync(o => o.UserId == userId && o.LearningDocumentId == learningId && o.CardId == cardId, ct);
        if (change is not null) return change;
        change = new LearningCardOverride { Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = learningId, CardId = cardId };
        db.Add(change);
        return change;
    }

    private static async Task<IResult> CardResult(AppDbContext db, Guid userId, Guid learningId, StudyDeck deck, string cardId, DateTimeOffset now, CancellationToken ct)
    {
        var card = (await PersonalAsync(db, userId, learningId, deck, ct)).FirstOrDefault(p => p.Card.Id == cardId);
        if (card is null) return CardNotFound();
        var s = await db.Set<LearningCardState>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == learningId && c.QuestionHash == cardId)
            .Select(c => new CardStateView(c.Streak, c.Mastered, c.LastGrade, c.DueAt, c.LastReviewedAt)).FirstOrDefaultAsync(ct);
        return TypedResults.Ok(ToDto(card, s, now));
    }

    private static async Task<IResult> ReviewCard(Guid sessionId, string cardId, CardReviewRequest request, ClaimsPrincipal user, AppDbContext db,
        CardDeckService decks, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, deck, error) = await DeckAsync(db, decks, userId, sessionId, ct);
        if (error is not null) return error;
        if (!(await PersonalAsync(db, userId, doc!.LearningId, deck!, ct)).Any(p => p.Card.Id == cardId)) return CardNotFound();

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
            (state.IntervalDays, var due) = CardSchedule.Next(state.IntervalDays, grade, now);
            state.DueAt = due;
            try
            {
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CardStateDto(cardId, state.Streak, state.Mastered, grade != CardGrade.Good, due <= now, due));
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A parallel first review created the row: apply this grade on top of it.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>
    /// "Bu gün təkrar" across all the student's guide documents: cards due now per document, and how many cards were
    /// reviewed today against the daily goal. Hidden and deleted cards are left out.
    /// </summary>
    private static async Task<IResult> ReviewToday(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var now = clock.GetUtcNow();
        var dayStart = CardSchedule.DayStart(now);
        var states = db.Set<LearningCardState>().AsNoTracking().Where(s => s.UserId == userId
            && !db.Set<LearningCardOverride>().Any(o => o.UserId == userId && o.LearningDocumentId == s.LearningDocumentId && o.CardId == s.QuestionHash
                && (o.Hidden || o.Deleted)));
        var due = await states.Where(s => (s.DueAt ?? s.LastReviewedAt) <= now).GroupBy(s => s.LearningDocumentId)
            .Select(g => new { LearningId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var reviewed = await states.CountAsync(s => s.LastReviewedAt >= dayStart, ct);

        var ids = due.Select(d => d.LearningId).ToList();
        // Only documents the student can still read.
        var docs = await db.Set<LearningDocument>().AsNoTracking().Where(l => ids.Contains(l.Id))
            .Join(LearningEndpoints.GuideDocuments(db, userId), l => l.DocumentId, d => d.Id, (l, d) => new { LearningId = l.Id, DocumentId = d.Id, Name = d.OriginalFileName })
            .ToListAsync(ct);
        var chats = await db.Set<LearningChatSession>().AsNoTracking().Where(s => s.UserId == userId && ids.Contains(s.LearningDocumentId))
            .Select(s => new { s.Id, s.LearningDocumentId, s.UpdatedAt }).ToListAsync(ct);
        var documents = docs
            .Select(d => new DueDocumentDto(d.DocumentId, chats.Where(c => c.LearningDocumentId == d.LearningId).MaxBy(c => c.UpdatedAt)?.Id, d.Name,
                due.First(x => x.LearningId == d.LearningId).Count))
            .OrderByDescending(d => d.Due).ToList();
        return TypedResults.Ok(new ReviewTodayDto(CardSchedule.DailyGoal, reviewed, documents.Sum(d => d.Due), documents));
    }
}

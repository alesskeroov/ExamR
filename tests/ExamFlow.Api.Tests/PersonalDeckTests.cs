using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class PersonalDeckTests
{
    private static readonly StudyDeck Deck = new(
        [new CardTopic("t1", "Mövzu 1")],
        [
            new StudyCardItem("a", CardKind.Term, "t1", "Pod nədir?", "Ən kiçik vahiddir.", [], [], 1, null, 0, NeedsReview: true),
            new StudyCardItem("b", CardKind.List, "t1", "Növlər: hansılardır?", "", ["A", "B"], [], 1, null, 0),
            new StudyCardItem("c", CardKind.Term, "t1", "Node nədir?", "Maşındır.", [], [], 1, null, 0),
        ]);

    private static LearningCardOverride Change(string id, string? front = null, string? back = null, bool hidden = false, bool deleted = false) =>
        new() { CardId = id, Front = front, Back = back, Hidden = hidden, Deleted = deleted };

    [Fact]
    public void Edits_hidden_marks_deletions_and_own_cards_are_applied()
    {
        var own = new LearningUserCard { Id = Guid.NewGuid(), Front = "Mənim sualım", Back = "Cavab", TopicKey = "unknown" };
        var cards = PersonalDeck.Merge(Deck,
            [Change("a", "Pod nədir? (düzəldilib)", "Konteyner qrupudur."), Change("b", hidden: true), Change("c", deleted: true)],
            [own]);

        Assert.Equal(["a", "b", own.CardId], cards.Select(c => c.Card.Id));
        var edited = cards[0];
        Assert.True(edited.Edited);
        Assert.False(edited.Card.NeedsReview); // the student checked it
        Assert.Equal("Konteyner qrupudur.", edited.Card.Back);
        Assert.True(cards[1].Hidden);
        Assert.Equal(["A", "B"], cards[1].Card.Items);
        var mine = cards[2];
        Assert.True(mine.Own);
        Assert.Equal(CardKind.Custom, mine.Card.Kind);
        Assert.Equal(PersonalDeck.OwnTopicKey, mine.Card.TopicKey); // unknown topic falls back to "Mənim kartlarım"
    }
}

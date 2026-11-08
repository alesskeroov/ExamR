using System.Text.Json;
using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class RevisionGeneratorTests
{
    private const string NetA = "Protokol cihazların məlumat mübadiləsi qaydalarıdır.";
    private const string NetB = "TCP etibarlı, UDP isə sürətli amma etibarsız ötürmədir.";
    private const string DbA = "Normallaşdırma verilənlərin təkrarlanmasını azaldır.";

    private static readonly IndexedQuestion[] Net = [new(1, "Protokol nədir?", NetA, 0, 2), new(2, "TCP və UDP fərqi nədir?", NetB, 0, 3)];
    private static readonly IndexedQuestion[] Db = [new(3, "Normallaşdırma nədir?", DbA, 0, 7)];

    private static IReadOnlyList<RevisionTopic> Topics(IReadOnlyDictionary<string, QuizAttempt>? latest = null) =>
        RevisionGenerator.Order([("net", "Şəbəkə", Net), ("db", "Baza", Db)], latest ?? new Dictionary<string, QuizAttempt>());

    private static QuizAttempt Wrong(IndexedQuestion q) => new(QuizLogic.Key(q.Text), QuizStatus.Incorrect, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Without_history_the_analysed_order_stays()
    {
        Assert.Equal(["net", "db"], Topics().Select(t => t.Key));
        Assert.All(Topics(), t => Assert.Equal(0, t.Answered));
    }

    [Fact]
    public void Topics_with_wrong_answers_come_first()
    {
        var latest = QuizLogic.Latest([Wrong(Db[0]), new(QuizLogic.Key(Net[0].Text), QuizStatus.Correct, DateTimeOffset.UnixEpoch)]);
        var topics = Topics(latest);
        Assert.Equal(["db", "net"], topics.Select(t => t.Key));
        Assert.Equal([3], topics[0].WrongNumbers);
        Assert.Equal(1, topics[1].Answered);
        Assert.Equal(0, topics[1].Wrong);
    }

    private static object Item(string text, int source) => new { text, source, quote = text };

    private static JsonElement Cards(params object[] cards) => JsonSerializer.SerializeToElement(new { cards });

    private static object NetCard(int topicId = 1) => new
    {
        topicId,
        facts = new[] { Item(NetA, 1), Item(NetB, 2), Item(DbA, 1), Item("Uydurulmuş fakt sənəddə yoxdur heç.", 1) },
        concepts = new[] { new { term = "Protokol", meaning = "Məlumat mübadiləsi qaydaları.", source = 1, quote = NetA } },
        comparisons = new[]
        {
            new { first = "TCP", second = "UDP", difference = "TCP etibarlıdır, UDP sürətlidir.", source = 2, quote = NetB },
            new { first = "TCP", second = "tcp", difference = "Eyni anlayışdır.", source = 2, quote = NetB },
        },
        tips = new[] { Item("Uydurma yadda saxlama qeydi.", 1) },
    };

    [Fact]
    public void Card_keeps_only_items_quoted_from_its_own_topic()
    {
        var cards = RevisionGenerator.Validate(Cards(NetCard()), Topics());
        var card = cards["net"];
        Assert.Equal([NetA, NetB], card.Facts);              // fact quoted from the other topic and the invented one are dropped
        Assert.Equal("Protokol", Assert.Single(card.Concepts).Term);
        Assert.Equal("UDP", Assert.Single(card.Comparisons).Second); // a concept compared with itself is dropped
        Assert.Empty(card.Tips);
        Assert.Equal([2, 3], card.SourcePages);
        // The same sentence is never shown twice in one card (fact and concept).
        var repeated = RevisionGenerator.Validate(Cards(new { topicId = 1, facts = new[] { Item(NetA, 1) },
            concepts = new[] { new { term = "Protokol", meaning = NetA, source = 1, quote = NetA } }, comparisons = Array.Empty<object>(), tips = Array.Empty<object>() }), Topics());
        Assert.Empty(repeated["net"].Concepts);
        Assert.False(cards.ContainsKey("db"));               // no card returned → not filled in
    }

    [Fact]
    public void Card_without_a_supported_fact_is_missing()
    {
        var empty = new { topicId = 2, facts = new[] { Item("Bu fakt sənəddə yoxdur, uydurulub.", 3) }, concepts = Array.Empty<object>(), comparisons = Array.Empty<object>(), tips = Array.Empty<object>() };
        Assert.Empty(RevisionGenerator.Validate(Cards(empty), Topics()));
    }

    [Fact]
    public async Task Several_topics_are_prepared_in_one_call()
    {
        var calls = 0;
        var db = new { topicId = 2, facts = new[] { Item(DbA, 3) }, concepts = Array.Empty<object>(), comparisons = Array.Empty<object>(), tips = Array.Empty<object>() };
        var (cards, status, _) = await RevisionGenerator.GenerateAsync(Topics(), (m, name, schema, tokens, ct) =>
        {
            calls++;
            Assert.Contains("<topic id=\"2\"", m[1].Content);
            Assert.Contains("never claim which questions will appear", m[1].Content);
            return Task.FromResult(new StudyAiResult(Cards(NetCard(), db), 200, null));
        }, default);
        Assert.Equal(1, calls);
        Assert.Equal(200, status);
        Assert.Equal(2, cards!.Count);
    }

    [Fact]
    public async Task Unusable_output_gets_one_repair_attempt_and_provider_errors_pass_through()
    {
        var calls = 0;
        var (cards, status, _) = await RevisionGenerator.GenerateAsync(Topics(), (m, n, s, t, ct) =>
        {
            calls++;
            return Task.FromResult(new StudyAiResult(Cards(), 200, null));
        }, default);
        Assert.Equal(2, calls);
        Assert.Null(cards);
        Assert.Equal(502, status);

        var failed = await RevisionGenerator.GenerateAsync(Topics(), (m, n, s, t, ct) => Task.FromResult(new StudyAiResult(null, 503, "Aktiv deyil")), default);
        Assert.Equal((503, "Aktiv deyil"), (failed.Status, failed.Error));
    }

    [Fact]
    public void Long_batches_stay_within_the_source_budget()
    {
        var big = Enumerable.Range(1, 40).Select(n => new IndexedQuestion(n, $"Sual {n}?", new string('a', 4000), 0, 1)).ToList();
        var topics = RevisionGenerator.Order([("a", "A", big)], new Dictionary<string, QuizAttempt>());
        Assert.True(RevisionGenerator.Sources(topics).Length < RevisionGenerator.SourceCharacters + 40 * 120);
        Assert.Equal("object", RevisionGenerator.Schema()["type"]!.GetValue<string>());
    }
}

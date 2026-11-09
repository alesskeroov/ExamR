using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamFlow.Api.Features.Learning;

public sealed record ConceptComparison(string First, string Second, string Difference);

/// <summary>"Son təkrar" card of one topic: short, and every item checked against an exact quote from the topic's own questions.</summary>
public sealed record RevisionCard(IReadOnlyList<string> Facts, IReadOnlyList<TopicConcept> Concepts, IReadOnlyList<ConceptComparison> Comparisons,
    IReadOnlyList<string> Tips, IReadOnlyList<int> SourcePages);

/// <summary>A topic in revision order, with the student's own result on it (by each question's latest answer).</summary>
public sealed record RevisionTopic(string Key, string Title, IReadOnlyList<IndexedQuestion> Questions, int Wrong, int Answered, IReadOnlyList<int> WrongNumbers);

/// <summary>
/// Son təkrar: one compact card per topic, generated for several topics in ONE model call and stored per topic and
/// document version, so it is never generated again on the next click or by another reader.
/// </summary>
public static class RevisionGenerator
{
    public const string Version = "rev-v1";
    /// <summary>Topics per model call (also the number of cards shown before "Daha çox").</summary>
    public const int BatchTopics = 5;
    public const int SourceCharacters = 24000;
    private const string Invalid = "Təkrar kartları yoxlana bilmədi. Yenidən cəhd edin.";

    /// <summary>Weak topics first (most wrong, then highest share wrong); without history the analysed order stays (biggest topics first).</summary>
    public static IReadOnlyList<RevisionTopic> Order(IEnumerable<(string Key, string Title, IReadOnlyList<IndexedQuestion> Questions)> topics,
        IReadOnlyDictionary<string, QuizAttempt> latest)
    {
        return topics.Select((t, position) =>
            {
                var answered = t.Questions.Select(q => (q.Number, Attempt: latest.GetValueOrDefault(QuizLogic.Key(q.Text)))).Where(x => x.Attempt is not null).ToList();
                var wrong = answered.Where(x => QuizLogic.IsWrong(x.Attempt!.Status)).Select(x => x.Number).ToList();
                return (Topic: new RevisionTopic(t.Key, t.Title, t.Questions, wrong.Count, answered.Count, wrong), Position: position);
            })
            .OrderByDescending(x => x.Topic.Wrong > 0)
            .ThenByDescending(x => x.Topic.Wrong)
            .ThenByDescending(x => x.Topic.Answered == 0 ? 0 : x.Topic.Wrong / (double)x.Topic.Answered)
            .ThenBy(x => x.Position)
            .Select(x => x.Topic).ToList();
    }

    public static string Task => """
        Each <topic> holds exam questions of one topic with the answers the document gives. For EVERY topic write a short
        last-minute revision card (topicId = the topic's id) that lets a student refresh the topic in about one minute:
        - facts: the 3-5 most important facts, each one short sentence.
        - concepts: up to 4 key terms with a one-sentence definition.
        - comparisons: up to 2 pairs of concepts of this topic that students easily confuse, with one sentence on the difference.
          Only when the sources really describe both; otherwise [].
        - tips: up to 2 very short memory notes (a key word chain, an order, a contrast) built only from source statements; otherwise [].
        Every item gives source = the id of the <source> block it comes from and quote = an exact, continuous quote (at least 15 characters)
        copied from that block. Use only sources of the same topic. Write nothing the sources do not say, no introductions,
        and never claim which questions will appear in the exam.
        """;

    private const string Evidence = "\"source\":{\"type\":\"integer\"},\"quote\":{\"type\":\"string\"}";
    public static JsonObject Schema() => (JsonObject)JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["cards"],"properties":{"cards":{"type":"array","items":{
          "type":"object","additionalProperties":false,"required":["topicId","facts","concepts","comparisons","tips"],"properties":{
          "topicId":{"type":"integer"},
          "facts":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["text","source","quote"],"properties":{"text":{"type":"string"},EVIDENCE}}},
          "concepts":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["term","meaning","source","quote"],"properties":{"term":{"type":"string"},"meaning":{"type":"string"},EVIDENCE}}},
          "comparisons":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["first","second","difference","source","quote"],"properties":{"first":{"type":"string"},"second":{"type":"string"},"difference":{"type":"string"},EVIDENCE}}},
          "tips":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["text","source","quote"],"properties":{"text":{"type":"string"},EVIDENCE}}}}}}}}
        """.Replace("EVIDENCE", Evidence))!;

    /// <summary>Topic blocks with their question blocks; long answers are shortened so the whole batch fits the budget.</summary>
    public static string Sources(IReadOnlyList<RevisionTopic> topics)
    {
        var total = Math.Max(1, topics.Sum(t => t.Questions.Count));
        var perQuestion = Math.Max(400, SourceCharacters / total);
        var text = new StringBuilder();
        for (var i = 0; i < topics.Count; i++)
        {
            text.Append("<topic id=\"").Append(i + 1).Append("\" title=\"").Append(TutorPrompt.Neutralise(topics[i].Title).Replace("\"", "'")).Append("\">\n");
            text.Append(PrepareGenerator.Sources(Units(topics[i], perQuestion)));
            text.Append("</topic>\n");
        }
        return text.ToString();
    }

    private static IReadOnlyList<LearningDocumentChunk> Units(RevisionTopic topic, int perQuestion) =>
        SharedAnswerGenerator.QuestionUnits(topic.Questions.Select(q => q.Answer.Length > perQuestion ? q with { Answer = q.Answer[..perQuestion] } : q).ToList());

    /// <summary>Cards by topic key. A topic whose card has no supported fact is missing (shown as not ready), never filled in.</summary>
    public static async Task<(IReadOnlyDictionary<string, RevisionCard>? Cards, int Status, string? Error)> GenerateAsync(IReadOnlyList<RevisionTopic> topics,
        PrepareGenerator.ModelCall call, CancellationToken ct)
    {
        if (topics.Count == 0) return (new Dictionary<string, RevisionCard>(), 200, null);
        var first = topics[0].Questions[0];
        var language = TutorPrompt.DetectLanguage(first.Text + " " + first.Answer);
        var input = Sources(topics) + Task;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var retry = attempt == 0 ? "" : "\nThe previous output failed validation: return one card per topic with exact quotes from that topic's sources.";
            var result = await call([new("system", StudyPrompts.System(language)), new("user", input + retry)], "revision_cards", Schema(),
                Math.Min(4000, 600 + 700 * topics.Count), ct);
            if (!result.Ok) return (null, result.Status, result.Error);
            try
            {
                var cards = Validate(result.Json!.Value, topics);
                if (cards.Count > 0) return (cards, 200, null);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { /* one repair attempt */ }
        }
        return (null, 502, Invalid);
    }

    public static Dictionary<string, RevisionCard> Validate(JsonElement json, IReadOnlyList<RevisionTopic> topics)
    {
        var cards = new Dictionary<string, RevisionCard>();
        foreach (var card in json.GetProperty("cards").EnumerateArray())
        {
            var id = card.GetProperty("topicId").GetInt32();
            if (id < 1 || id > topics.Count || cards.ContainsKey(topics[id - 1].Key)) continue;
            var topic = topics[id - 1];
            var units = SharedAnswerGenerator.QuestionUnits(topic.Questions);
            var pages = new SortedSet<int>();
            var seen = new HashSet<string>();

            bool Supported(JsonElement item, string text)
            {
                var unit = units.FirstOrDefault(u => u.Ordinal == item.GetProperty("source").GetInt32());
                if (unit is null || !SharedAnswerGenerator.Supports(unit, item.GetProperty("quote").GetString())
                    || !seen.Add(SharedAnswerGenerator.NormalizedKey(text))) return false;
                if (unit.PageFrom is { } page) pages.Add(page);
                return true;
            }
            static string? Text(JsonElement item, string field, int max) => item.GetProperty(field).GetString()?.Trim() is { Length: > 0 } t && t.Length <= max ? t : null;

            var facts = card.GetProperty("facts").EnumerateArray()
                .Select(f => Text(f, "text", 240) is { } t && Supported(f, t) ? t : null).OfType<string>().Take(5).ToList();
            if (facts.Count == 0) continue;
            var concepts = card.GetProperty("concepts").EnumerateArray()
                .Select(c => Text(c, "term", 80) is { } term && Text(c, "meaning", 240) is { } meaning && Supported(c, meaning) ? new TopicConcept(term, meaning) : null)
                .OfType<TopicConcept>().Take(4).ToList();
            var comparisons = card.GetProperty("comparisons").EnumerateArray()
                .Select(c => Text(c, "first", 80) is { } a && Text(c, "second", 80) is { } b && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
                    && Text(c, "difference", 300) is { } d && Supported(c, d) ? new ConceptComparison(a, b, d) : null)
                .OfType<ConceptComparison>().Take(2).ToList();
            var tips = card.GetProperty("tips").EnumerateArray()
                .Select(t => Text(t, "text", 160) is { } text && Supported(t, text) ? text : null).OfType<string>().Take(2).ToList();
            cards[topic.Key] = new RevisionCard(facts, concepts, comparisons, tips, pages.ToList());
        }
        return cards;
    }
}

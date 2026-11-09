using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamFlow.Api.Features.Learning;

public sealed record TopicConcept(string Term, string Meaning);

/// <summary>"Mövzunu anla": every item was checked against an exact quote from one of the topic's questions.</summary>
public sealed record TopicExplanation(string Title, IReadOnlyList<string> Explanation, IReadOnlyList<TopicConcept> Concepts, string? Example,
    IReadOnlyList<string> Facts, IReadOnlyList<int> QuestionNumbers, IReadOnlyList<int> SourcePages);

/// <summary>A question of the group: covered by the shared answer (with its own addition) or shown with the document's answer.</summary>
public sealed record CoveredQuestion(int Number, string Text, int? Page, bool Covered, string Addition, string? DocumentAnswer);

/// <summary>"İmtahan cavabı": one shared answer per topic group. Sufficient=false keeps the questions with their document answers only.</summary>
public sealed record GroupAnswer(string Title, bool Sufficient, string Answer, IReadOnlyList<CoveredQuestion> Questions, IReadOnlyList<int> SourcePages);

public sealed record PrepareResult<T>(T? Payload, int Status, string? Error) where T : class;

/// <summary>
/// Hazırlaş generators. Sources are the topic's open questions with the document's answers; nothing else is sent.
/// Each output item carries a source id and an exact quote, and items the quote does not support are dropped (explanations)
/// or make the output invalid (shared answers), so nothing outside the PDF reaches the student.
/// </summary>
public static class PrepareGenerator
{
    /// <summary>Bump when prompts or payloads change, so cached results are not reused.</summary>
    public const string Version = "prep-v1";
    /// <summary>Larger topic groups are split, so one answer stays readable and the output fits the token limit.</summary>
    public const int MaxGroupQuestions = 12;
    public const int SourceCharacters = 18000;
    private const string Invalid = "AI cavabı yoxlana bilmədi. Yenidən cəhd edin.";

    public delegate Task<StudyAiResult> ModelCall(IReadOnlyList<ChatModelMessage> messages, string schemaName, JsonObject schema, int maxOutputTokens, CancellationToken ct);

    /// <summary>One block per question (id = question number); long answers are shortened so the topic fits the budget.</summary>
    public static IReadOnlyList<LearningDocumentChunk> Units(IReadOnlyList<IndexedQuestion> questions)
    {
        var perQuestion = Math.Max(600, SourceCharacters / Math.Max(1, questions.Count));
        return SharedAnswerGenerator.QuestionUnits(questions.Select(q => q.Answer.Length > perQuestion ? q with { Answer = q.Answer[..perQuestion] } : q).ToList());
    }

    public static string Sources(IReadOnlyList<LearningDocumentChunk> units) => string.Concat(units.Select(u =>
        $"<source id=\"{u.Ordinal}\" pages=\"{StudyPrompts.PageText(u.PageFrom, u.PageTo)}\">\n{TutorPrompt.Neutralise(u.Text)}\n</source>\n"));

    private const string QuoteRule = """
        Every item gives source = the id of the <source> block it comes from and quote = an exact, continuous quote (at least 15 characters)
        copied from that block that supports the whole item. Write nothing the sources do not say.
        """;

    public static string ExplainTask(string title) => $"""
        The <source> blocks are exam questions of ONE topic ("{TutorPrompt.Neutralise(title)}") with the answers the document gives.
        Help a student understand this topic before the exam, in simple, clear language.
        - explanation: 2-5 short sentences that explain the topic as a whole (what it is, why it matters, how it works).
        - concepts: up to 6 key terms of the topic, each with a one-sentence meaning.
        - example: one concrete example ONLY if the sources contain one (a named case, a number, a scenario); otherwise text and quote are "" and source is 0.
        - facts: up to 6 short facts the student must remember for the exam.
        {QuoteRule}
        If the sources are too short to explain the topic, set sufficient=false.
        """;

    public static string AnswerTask(string title) => $"""
        The <source> blocks are exam questions grouped as one topic ("{TutorPrompt.Neutralise(title)}"), each with the answer the document gives.
        1. sentences: ONE shared exam answer of 8-13 complete, simple sentences that covers what these questions have in common.
           No introductions, conclusions or filler; never mechanically join the separate answers.
        2. questions: one entry for EVERY question id. covered = true only if the shared answer plus the additions really answer that question.
           additions = 1-2 sentences specific to that question; their quote must come from THAT question's own block.
           If the question does not belong to this topic, set covered=false and additions=[].
        {QuoteRule}
        If the sources cannot support 8 sentences, set sufficient=false and sentences=[], and still fill questions.
        """;

    private static JsonObject Schema(string json) => (JsonObject)JsonNode.Parse(json.Replace("EVIDENCE", Evidence))!;
    private const string Evidence = "\"source\":{\"type\":\"integer\"},\"quote\":{\"type\":\"string\"}";

    public static JsonObject ExplainSchema() => Schema("""
        {"type":"object","additionalProperties":false,"required":["sufficient","explanation","concepts","example","facts"],"properties":{
          "sufficient":{"type":"boolean"},
          "explanation":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["text","source","quote"],"properties":{"text":{"type":"string"},EVIDENCE}}},
          "concepts":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["term","meaning","source","quote"],"properties":{"term":{"type":"string"},"meaning":{"type":"string"},EVIDENCE}}},
          "example":{"type":"object","additionalProperties":false,"required":["text","source","quote"],"properties":{"text":{"type":"string"},EVIDENCE}},
          "facts":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["text","source","quote"],"properties":{"text":{"type":"string"},EVIDENCE}}}}}
        """);

    public static JsonObject AnswerSchema() => Schema("""
        {"type":"object","additionalProperties":false,"required":["sufficient","sentences","questions"],"properties":{
          "sufficient":{"type":"boolean"},
          "sentences":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["text","source","quote"],"properties":{"text":{"type":"string"},EVIDENCE}}},
          "questions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["number","covered","additions"],"properties":{
            "number":{"type":"integer"},"covered":{"type":"boolean"},
            "additions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["text","quote"],"properties":{"text":{"type":"string"},"quote":{"type":"string"}}}}}}}}}
        """);

    public static Task<PrepareResult<TopicExplanation>> ExplainAsync(string title, IReadOnlyList<IndexedQuestion> questions, ModelCall call, CancellationToken ct)
    {
        var units = Units(questions);
        return RunAsync(call, units, ExplainTask(title), "topic_explanation", ExplainSchema(), 1400,
            json => ValidateExplanation(json, title, questions, units), ct);
    }

    public static Task<PrepareResult<GroupAnswer>> AnswerAsync(string title, IReadOnlyList<IndexedQuestion> questions, ModelCall call, CancellationToken ct)
    {
        var units = Units(questions);
        return RunAsync(call, units, AnswerTask(title), "topic_exam_answer", AnswerSchema(), Math.Min(4000, 1600 + 180 * questions.Count),
            json => new PrepareResult<GroupAnswer>(ValidateAnswer(json, title, questions, units), 200, null), ct);
    }

    /// <summary>One call plus at most one repair attempt when the output fails validation; provider errors are passed through.</summary>
    private static async Task<PrepareResult<T>> RunAsync<T>(ModelCall call, IReadOnlyList<LearningDocumentChunk> units, string task, string schemaName,
        JsonObject schema, int maxTokens, Func<JsonElement, PrepareResult<T>> validate, CancellationToken ct) where T : class
    {
        if (units.Count == 0) return new(null, 409, "Bu mövzu üçün sənəddə sual tapılmadı.");
        var language = TutorPrompt.DetectLanguage(units[0].Text);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var retry = attempt == 0 ? "" : "\nThe previous output failed validation: every item needs an existing source id and an exact quote from that block.";
            var result = await call([new("system", StudyPrompts.System(language)), new("user", Sources(units) + task + retry)], schemaName, schema, maxTokens, ct);
            if (!result.Ok) return new(null, result.Status, result.Error);
            try { return validate(result.Json!.Value); }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { /* one repair attempt */ }
        }
        return new(null, 502, Invalid);
    }

    /// <summary>Unsupported items are dropped; when most of the explanation is unsupported the whole output is rejected.</summary>
    public static PrepareResult<TopicExplanation> ValidateExplanation(JsonElement json, string title, IReadOnlyList<IndexedQuestion> questions,
        IReadOnlyList<LearningDocumentChunk> units)
    {
        if (!json.GetProperty("sufficient").GetBoolean())
            return new(null, 409, "Bu mövzunu izah etmək üçün sənəddə kifayət qədər məlumat yoxdur.");
        var pages = new SortedSet<int>();
        var seen = new HashSet<string>();

        string? Supported(JsonElement item, string field, int max)
        {
            var text = item.GetProperty(field).GetString()?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > max || !seen.Add(SharedAnswerGenerator.NormalizedKey(text))) return null;
            var unit = units.FirstOrDefault(u => u.Ordinal == item.GetProperty("source").GetInt32());
            if (unit is null || !SharedAnswerGenerator.Supports(unit, item.GetProperty("quote").GetString())) return null;
            if (unit.PageFrom is { } page) pages.Add(page);
            return text;
        }

        var raw = json.GetProperty("explanation").EnumerateArray().ToList();
        var explanation = raw.Select(e => Supported(e, "text", 600)).OfType<string>().Take(5).ToList();
        if (explanation.Count == 0 || explanation.Count * 2 < raw.Count) throw new JsonException("Explanation is not supported by the sources");
        var concepts = json.GetProperty("concepts").EnumerateArray()
            .Select(c => Supported(c, "meaning", 300) is { } meaning && c.GetProperty("term").GetString()?.Trim() is { Length: > 0 and <= 80 } term
                ? new TopicConcept(term, meaning) : null)
            .OfType<TopicConcept>().Take(6).ToList();
        var example = json.GetProperty("example") is var e && e.GetProperty("text").GetString() is { Length: > 0 } ? Supported(e, "text", 500) : null;
        var facts = json.GetProperty("facts").EnumerateArray().Select(f => Supported(f, "text", 240)).OfType<string>().Take(6).ToList();
        return new(new TopicExplanation(title, explanation, concepts, example, facts, questions.Select(q => q.Number).ToList(), pages.ToList()), 200, null);
    }

    /// <summary>
    /// The shared answer must be 8–13 supported single sentences (else invalid). A question counts as covered only when the model says so
    /// AND it has 1–2 supported sentences quoted from its own answer; every other question is shown with the document's answer instead.
    /// </summary>
    public static GroupAnswer ValidateAnswer(JsonElement json, string title, IReadOnlyList<IndexedQuestion> questions, IReadOnlyList<LearningDocumentChunk> units)
    {
        var pages = new SortedSet<int>();
        void Page(LearningDocumentChunk unit) { if (unit.PageFrom is { } p) pages.Add(p); }
        CoveredQuestion Uncovered(IndexedQuestion q) => new(q.Number, q.Text, q.Page, false, "", q.Answer);

        if (!json.GetProperty("sufficient").GetBoolean())
            return new(title, false, "", questions.Select(Uncovered).ToList(), []);

        var sentences = json.GetProperty("sentences").EnumerateArray().ToList();
        if (sentences.Count is < 8 or > 13) throw new JsonException("Sentence count");
        var seen = new HashSet<string>();
        var answer = new List<string>();
        foreach (var sentence in sentences)
        {
            var text = sentence.GetProperty("text").GetString()?.Trim() ?? "";
            var unit = units.FirstOrDefault(u => u.Ordinal == sentence.GetProperty("source").GetInt32());
            if (text.Length is 0 or > 800 || !SharedAnswerGenerator.IsOneSentence(text) || !seen.Add(SharedAnswerGenerator.NormalizedKey(text))
                || unit is null || !SharedAnswerGenerator.Supports(unit, sentence.GetProperty("quote").GetString()))
                throw new JsonException("Unsupported, repeated or incomplete sentence");
            Page(unit);
            answer.Add(text);
        }

        var entries = json.GetProperty("questions").EnumerateArray()
            .GroupBy(q => q.GetProperty("number").GetInt32()).ToDictionary(g => g.Key, g => g.First());
        var covered = new List<CoveredQuestion>();
        foreach (var q in questions)
        {
            var own = units.First(u => u.Ordinal == q.Number);
            var additions = entries.TryGetValue(q.Number, out var entry) && entry.GetProperty("covered").GetBoolean()
                ? entry.GetProperty("additions").EnumerateArray().ToList() : [];
            var valid = additions.Count is 1 or 2 && additions.All(a => a.GetProperty("text").GetString()?.Trim() is { Length: > 0 and <= 400 } t
                && SharedAnswerGenerator.IsOneSentence(t) && SharedAnswerGenerator.Supports(own, a.GetProperty("quote").GetString()));
            if (!valid) { covered.Add(Uncovered(q)); continue; }
            Page(own);
            covered.Add(new(q.Number, q.Text, q.Page, true, string.Join(' ', additions.Select(a => a.GetProperty("text").GetString()!.Trim())), null));
        }
        // A shared answer that fits none of its questions is not an answer to them.
        if (!covered.Any(c => c.Covered)) return new(title, false, "", questions.Select(Uncovered).ToList(), []);
        return new(title, true, string.Join(' ', answer), covered, pages.ToList());
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExamFlow.Api.Features.Explanations;

public sealed record ExplanationOption(string Label, string Text);

/// <summary>A bank question that still needs an explanation, with its answer key.</summary>
public sealed record ExplanationCandidate(Guid QuestionId, byte[] Fingerprint, string Text, IReadOnlyList<ExplanationOption> Options,
    string CorrectLabel, ExplanationQuestionType Type);

public sealed record ExplanationEstimate(int Questions, int TheoryQuestions, int MathQuestions, int Requests, int InputTokens, int OutputTokens, decimal EstimatedUsd);

/// <summary>Outcome for one question of a model answer. Not <see cref="Ok"/> = unusable (missing, empty, too long).</summary>
public sealed record ExplanationItemResult(Guid QuestionId, bool Ok, ExplanationQuestionType Type, ExplanationStatus Status, string? Explanation, string? ReviewReason);

/// <summary>Pure explanation-bank logic (no database, no network), so it can be unit-tested.</summary>
public static partial class ExplanationEngine
{
    /// <summary>Rough size of the fixed instructions sent with every request.</summary>
    public const int SystemPromptTokens = 450;
    public const int MinExplanationLength = 20;

    /// <summary>
    /// Identifies a question by what the student sees plus its answer key: normalised text, the options in any order
    /// and the correct option's text. The same question in another PDF or a shuffled exam snapshot gets the same value;
    /// an edit of the text, an option or the answer key gets a new one, so an old explanation never matches it.
    /// Returns null when the correct answer is unknown (nothing safe to explain).
    /// </summary>
    public static byte[]? Fingerprint(string text, IEnumerable<string> options, string? correctOption)
    {
        if (correctOption is null) return null;
        var builder = new StringBuilder("v1\n").Append(Normalize(text)).Append('\n');
        foreach (var option in options.Select(Normalize).Order(StringComparer.Ordinal))
            builder.Append("o:").Append(option).Append('\n');
        builder.Append("c:").Append(Normalize(correctOption));
        return SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    internal static string Normalize(string value) =>
        Whitespace().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim().ToLowerInvariant();

    /// <summary>
    /// Free pre-classification that picks the (dearer) math model: formulas, arithmetic, calculation verbs, or mostly
    /// numeric options. The model reports its own type too; this only decides batching and price.
    /// </summary>
    public static bool IsMath(string text, IReadOnlyCollection<string> options)
    {
        if (Formula().IsMatch(text) || MathWords().IsMatch(text)) return true;
        return options.Count > 0 && options.Count(o => NumericOption().IsMatch(o.Trim())) * 2 > options.Count;
    }

    public static int InputTokens(ExplanationCandidate c, int maxQuestionCharacters) =>
        (int)Math.Ceiling((Math.Min(c.Text.Length, maxQuestionCharacters) + c.Options.Sum(o => o.Text.Length + 8) + 60) / 3.0);

    /// <summary>Requests of one type each (theory and math use different models), in the candidates' order.</summary>
    public static IEnumerable<IReadOnlyList<ExplanationCandidate>> Batches(IEnumerable<ExplanationCandidate> candidates, ExplanationAiOptions o) =>
        candidates.GroupBy(c => c.Type).SelectMany(g => g.Chunk(o.BatchSizeFor(g.Key)).Select(chunk => (IReadOnlyList<ExplanationCandidate>)chunk));

    public static (int Input, int Output) Tokens(IReadOnlyList<ExplanationCandidate> batch, ExplanationAiOptions o) =>
        (SystemPromptTokens + batch.Sum(c => InputTokens(c, o.MaxQuestionCharacters)), batch.Count * o.OutputTokensFor(batch[0].Type));

    /// <summary>Most a request may cost: every input token plus every allowed output token at the full price.</summary>
    public static decimal WorstCaseCost(IReadOnlyList<ExplanationCandidate> batch, ExplanationAiOptions o)
    {
        var (input, output) = Tokens(batch, o);
        return o.CostOf(batch[0].Type, input, output);
    }

    public static ExplanationEstimate Estimate(IReadOnlyList<ExplanationCandidate> candidates, ExplanationAiOptions o)
    {
        int requests = 0, input = 0, output = 0;
        decimal usd = 0;
        foreach (var batch in Batches(candidates, o))
        {
            var tokens = Tokens(batch, o);
            requests++;
            input += tokens.Input;
            output += tokens.Output;
            usd += o.CostOf(batch[0].Type, tokens.Input, tokens.Output);
        }
        var math = candidates.Count(c => c.Type == ExplanationQuestionType.Math);
        return new ExplanationEstimate(candidates.Count, candidates.Count - math, math, requests, input, output, Math.Round(usd, 6));
    }

    /// <summary>Short id of a batch item in the prompt ("q1", "q2", …): cheaper than GUIDs and free of real ids.</summary>
    public static string ItemId(int index) => $"q{index + 1}";

    /// <summary>
    /// Validates a model answer against the batch. The model's own result (<c>computedOption</c>) is compared with the
    /// PDF's key: a disagreement, or a math answer the model could not verify, goes to Review instead of Draft.
    /// </summary>
    public static IReadOnlyList<ExplanationItemResult> Parse(JsonElement json, IReadOnlyList<ExplanationCandidate> batch, int maxLength)
    {
        var answers = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    answers.TryAdd(id.GetString()!, item); // a repeated id keeps its first answer

        return batch.Select((candidate, index) =>
        {
            if (!answers.TryGetValue(ItemId(index), out var item)) return Failed(candidate);
            var text = item.TryGetProperty("explanation", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : "";
            if (text.Length < MinExplanationLength || text.Length > maxLength) return Failed(candidate);

            var type = item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && Enum.TryParse<ExplanationQuestionType>(t.GetString(), out var parsed)
                ? parsed : candidate.Type;
            var computed = item.TryGetProperty("computedOption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()!.Trim() : null;
            var labels = candidate.Options.Select(o => o.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (computed is not null && !labels.Contains(computed)) computed = null; // not one of the options

            string? reason = null;
            if (computed is not null && !computed.Equals(candidate.CorrectLabel, StringComparison.OrdinalIgnoreCase))
                reason = $"AI nəticəsi ({computed.ToUpperInvariant()}) cavab açarı ilə ({candidate.CorrectLabel}) uyğun gəlmir.";
            else if (computed is null && (type == ExplanationQuestionType.Math || candidate.Type == ExplanationQuestionType.Math))
                reason = "Riyazi nəticə avtomatik yoxlanıla bilmədi.";
            else if (computed is null)
                reason = "AI düzgün variantı təsdiqləmədi.";

            return new ExplanationItemResult(candidate.QuestionId, true, type, reason is null ? ExplanationStatus.Draft : ExplanationStatus.Review, text, reason);
        }).ToList();

        static ExplanationItemResult Failed(ExplanationCandidate c) => new(c.QuestionId, false, c.Type, ExplanationStatus.Draft, null, null);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Arithmetic between numbers, powers/roots, LaTeX, percentages, equations with a variable.
    [GeneratedRegex(@"\d\s*[+\-*/×÷^=]\s*\d|\\(frac|sqrt|sum|int)|\$[^$]+\$|√|\d\s*%|\b[xyz]\s*[=+\-*/^²³]|[²³]", RegexOptions.IgnoreCase)]
    private static partial Regex Formula();

    [GeneratedRegex(@"\b(hesablay|hesabla|tapın|tapın:|neçədir|neçə\s|faizi|tənliy|tənlik|kökü|sahəsi|həcmi|perimetr|ehtimal|ədədi\s+orta|törəmə|inteqral|limit)", RegexOptions.IgnoreCase)]
    private static partial Regex MathWords();

    [GeneratedRegex(@"^[-+−]?\d+([.,]\d+)?\s*(%|sm|m|kq|km|san|dəq|azn|\$)?$", RegexOptions.IgnoreCase)]
    private static partial Regex NumericOption();
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExamFlow.Api.Features.Study;

public static partial class StudyEngine
{
    private static readonly CultureInfo Azeri = CultureInfo.GetCultureInfo("az-Latn-AZ");
    private static readonly HashSet<string> StopWords = ["üçün", "kimi", "olan", "olur", "daha", "hansı", "nədir", "necə", "haqqında", "mənə", "izah", "edir", "onun", "bunu", "qədər", "sonra", "əvvəl", "həmçinin", "yalnız", "deyil", "sual", "cavab", "zəhmət", "olmasa"];
    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
    public static string Normalize(string text) => text.Normalize(NormalizationForm.FormKC).ToLower(Azeri).Trim();
    private static HashSet<string> Concepts(string text) => Words().Matches(Normalize(text))
        .Select(m => m.Value).Where(w => w.Length >= 3 && !StopWords.Contains(w)).ToHashSet();

    public static string Topic(StudySource source) => string.Join(" ", source.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(10));

    public static string Hint(string answer) => "Əsas anlayışlar: " + string.Join(", ", Words().Matches(answer)
        .Select(m => m.Value).Where(w => Concepts(w).Count > 0)
        .Distinct(StringComparer.Create(Azeri, true)).Take(4));

    public static string Explain(string answer) => string.Join(" ", Regex.Split(answer.Trim(), @"(?<=[.!?])\s+|[\r\n]+")
        .Where(s => !string.IsNullOrWhiteSpace(s)).Take(2));

    public static string? ShortAnswer(string answer)
    {
        var sentence = Regex.Split(answer.Trim(), @"(?<=[.!?])\s+|[\r\n]+")
            .Select(s => s.Trim()).FirstOrDefault(s => s.Length is > 0 and <= 220 && Words().Matches(s).Count >= 2);
        return sentence is { Length: > 0 and <= 220 } ? sentence : null;
    }

    public static PracticeEvaluation Evaluate(string expected, string answer)
    {
        if (AnswerKey(expected) == AnswerKey(answer)) return new(100, "Doğru cavab", []);
        var required = Words().Matches(Normalize(expected)).Select(m => m.Value)
            .Where(w => w.Length >= 2 && !StopWords.Contains(w)).ToHashSet();
        var given = Words().Matches(Normalize(answer)).Select(m => m.Value).ToHashSet();
        var matched = required.Count(given.Contains);
        var score = required.Count == 0 ? 0 : (int)Math.Round(100d * matched / required.Count);
        // A changed negation can reverse an otherwise identical answer.
        var negatives = new[] { "deyil", "olmaz", "yoxdur", "etmir" };
        if (negatives.Any(w => given.Contains(w) != Words().Matches(Normalize(expected)).Any(m => m.Value == w)))
            score = Math.Min(score, 40);
        if (score == 100) score = 95;
        return new(score, score >= 80 ? "Əsas anlayışlar uyğundur" : score >= 40 ? "Qismən uyğun cavab" : "Cavabı yenidən nəzərdən keçir",
            required.Where(w => !given.Contains(w)).Take(6).ToArray());
    }

    private static string AnswerKey(string text) => string.Join(" ", Words().Matches(Normalize(text)).Select(m => m.Value));

    // Reject uncertain alternatives rather than guessing their semantic correctness.
    private static bool DistinctAnswer(string first, string second)
    {
        var a = AnswerKey(first);
        var b = AnswerKey(second);
        if (a.Length == 0 || b.Length == 0 || a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal)) return false;
        var left = Concepts(first);
        var right = Concepts(second);
        if (left.Count < 2 || right.Count < 2) return false;
        return (double)left.Intersect(right).Count() / Math.Min(left.Count, right.Count) < 0.5;
    }

    public static string[]? Options(StudySource source, IReadOnlyList<StudySource> sources)
    {
        var shortAnswer = ShortAnswer(source.Answer);
        if (source.Text.Length > 240 || shortAnswer is null) return null;
        var terms = Concepts(source.Text);
        if (terms.Count == 0) return null;
        // Shared document vocabulary is not evidence that two questions have the same answer.
        var subject = terms.Where(t => sources.Count(s => Concepts(s.Text).Contains(t)) < Math.Max(3, sources.Count / 3)).ToHashSet();
        if (subject.Count == 0) subject = terms;
        var answers = new List<string> { shortAnswer };
        foreach (var other in sources.Where(s => s.Id != source.Id))
        {
            // Answers discussing the question's subject may also be correct: exclude them.
            var otherTerms = Concepts(other.Text + " " + other.Answer);
            if ((double)subject.Intersect(otherTerms).Count() / subject.Count >= 0.6) continue;
            var alternative = ShortAnswer(other.Answer);
            if (alternative is null || !DistinctAnswer(source.Answer, other.Answer)) continue;
            var ratio = (double)alternative.Length / Math.Max(1, shortAnswer.Length);
            if (ratio < 0.2 || ratio > 5 || !answers.All(a => DistinctAnswer(a, alternative))) continue;
            answers.Add(alternative);
            if (answers.Count == 4) return answers.OrderBy(_ => Random.Shared.Next()).ToArray();
        }
        return null;
    }

    public static StudyChunk[] Retrieve(IReadOnlyList<StudySource> sources, string query, Guid? focus, StudyAiOptions options, bool preferFocus = false)
    {
        var terms = Concepts(query);
        var normalized = Normalize(query);
        if (focus is null && normalized.Contains("sənəd", StringComparison.Ordinal) &&
            (normalized.Contains("mövzu", StringComparison.Ordinal) || normalized.Contains("xülasə", StringComparison.Ordinal)))
        {
            // A document overview retrieves a bounded, representative selection rather than the whole file.
            var sampled = sources.Where((_, index) => index % Math.Max(1, sources.Count / options.MaxChunks) == 0)
                .Take(options.MaxChunks).Select(s => Chunk(s).First()).ToArray();
            var budget = options.MaxContextCharacters;
            var overview = new List<StudyChunk>();
            foreach (var chunk in sampled)
            {
                if (budget < 100) break;
                var text = chunk.Text[..Math.Min(chunk.Text.Length, budget)];
                overview.Add(chunk with { Text = text });
                budget -= text.Length;
            }
            return overview.ToArray();
        }
        var candidates = sources.SelectMany(s => Chunk(s)).Select(c =>
        {
            var words = Concepts(c.Text);
            var matches = terms.Count(t => words.Contains(t) || t.Length >= 4 && words.Any(w => w.Length >= 4 && (w.StartsWith(t, StringComparison.Ordinal) || t.StartsWith(w, StringComparison.Ordinal))));
            return (Chunk: c, Rank: terms.Count == 0 ? 0 : (double)matches / terms.Count);
        }).ToList();
        var hasMatch = candidates.Any(c => c.Rank > 0);
        var ranked = candidates.Select(c => (c.Chunk, Rank: c.Rank + (c.Chunk.QuestionId == focus && (preferFocus || !hasMatch) ? 2 : 0)))
            .Where(c => c.Rank > 0).OrderByDescending(c => c.Rank).ThenBy(c => c.Chunk.Number).ToList();
        var result = new List<StudyChunk>();
        var remaining = options.MaxContextCharacters;
        foreach (var candidate in ranked)
        {
            if (result.Count == options.MaxChunks || remaining < 100) break;
            var text = candidate.Chunk.Text;
            if (text.Length > remaining) text = text[..remaining];
            result.Add(candidate.Chunk with { Text = text });
            remaining -= text.Length;
        }
        return result.ToArray();
    }

    private static IEnumerable<StudyChunk> Chunk(StudySource source)
    {
        for (var start = 0; start < source.Answer.Length; start += 900)
        {
            var text = source.Answer.Substring(start, Math.Min(1000, source.Answer.Length - start));
            yield return new StudyChunk($"{source.Id:N}:{start}", source.Id, source.Number, $"Sual {source.Number}: {source.Text[..Math.Min(source.Text.Length, 1000)]}\nSənəddəki cavab: {text}");
            if (start + 1000 >= source.Answer.Length) break;
        }
    }

    public static StudyCitation[] Citations(StudyAiOutput output, StudyChunk[] chunks) => output.SourceIds
        .Distinct().Select(id => chunks.First(c => c.Id == id))
        .Select(c => new StudyCitation(c.QuestionId, c.Number, c.Text)).ToArray();
}

namespace ExamFlow.Parsing;

public readonly record struct AnswerDetection(int? CorrectIndex, string? Warning)
{
    public static AnswerDetection None => new(null, null);
}

/// <summary>Finds the correct option of a question. Must return null rather than guess.</summary>
public interface IAnswerDetector
{
    AnswerDetection Detect(RawQuestion question);
}

/// <summary>Detects the correct option from a check-mark bullet (√ ✓ ✔ ☑) used instead of the normal bullet.</summary>
public sealed class MarkerAnswerDetector : IAnswerDetector
{
    public AnswerDetection Detect(RawQuestion question)
    {
        var marked = Enumerable.Range(0, question.Options.Count)
            .Where(i => question.Options[i].LabelTokens is [{ Text: [var c] }] && QuestionSegmenter.CorrectMarkers.Contains(c))
            .ToList();

        return marked.Count switch
        {
            1 => new AnswerDetection(marked[0], null),
            > 1 => new AnswerDetection(null, "Bir neçə variant düzgün kimi işarələnib, düzgün cavab müəyyən edilmədi"),
            _ => AnswerDetection.None,
        };
    }
}

/// <summary>
/// Detects the correct option from visual emphasis: bold option text, a bold label only, or a distinct colour.
/// Exactly one option must stand out; anything else is reported as no answer.
/// </summary>
public sealed class StyleAnswerDetector : IAnswerDetector
{
    private const double StrongBold = 0.6;
    private const double WeakBold = 0.2;

    public AnswerDetection Detect(RawQuestion question)
    {
        var options = question.Options;
        if (options.Count < 2)
        {
            return AnswerDetection.None;
        }

        // 1. Bold option text.
        var boldRatios = options.Select(o => BoldRatio(o.TextTokens)).ToList();
        var strong = Indexes(boldRatios, r => r >= StrongBold);
        if (strong.Count == 1 && boldRatios.Where((_, i) => i != strong[0]).All(r => r <= WeakBold))
        {
            return new AnswerDetection(strong[0], null);
        }

        var ambiguous = strong.Count > 1 && strong.Count < options.Count;

        // 2. Only the label ("B)") is bold.
        var boldLabels = Indexes(options, o => o.LabelTokens.All(t => t.IsBold));
        if (boldLabels.Count == 1 && !ambiguous)
        {
            return new AnswerDetection(boldLabels[0], null);
        }

        ambiguous |= boldLabels.Count > 1 && boldLabels.Count < options.Count;

        // 3. One option in a different colour from all the others.
        var colors = options.Select(o => DominantColor(o.LabelTokens.Concat(o.TextTokens))).ToList();
        var colorGroups = colors.GroupBy(c => c).ToList();
        if (!ambiguous && colorGroups.Count == 2 && colorGroups.Any(g => g.Count() == 1) && options.Count > 2)
        {
            var odd = colorGroups.Single(g => g.Count() == 1).Key;
            return new AnswerDetection(colors.IndexOf(odd), null);
        }

        return ambiguous || colorGroups.Count > 2
            ? new AnswerDetection(null, "Bir neçə variant vurğulanıb, düzgün cavab müəyyən edilmədi")
            : AnswerDetection.None;
    }

    private static double BoldRatio(IReadOnlyCollection<TextToken> tokens)
    {
        var total = tokens.Sum(t => t.Text.Length);
        return total == 0 ? 0 : (double)tokens.Where(t => t.IsBold).Sum(t => t.Text.Length) / total;
    }

    private static string DominantColor(IEnumerable<TextToken> tokens) =>
        tokens.GroupBy(t => t.ColorKey)
            .OrderByDescending(g => g.Sum(t => t.Text.Length))
            .Select(g => g.Key)
            .FirstOrDefault() ?? TextToken.DefaultColor;

    private static List<int> Indexes<T>(IReadOnlyList<T> items, Func<T, bool> predicate) =>
        Enumerable.Range(0, items.Count).Where(i => predicate(items[i])).ToList();
}

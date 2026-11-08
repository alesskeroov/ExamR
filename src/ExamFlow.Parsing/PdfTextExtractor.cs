using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace ExamFlow.Parsing;

public sealed record PdfTextContent(IReadOnlyList<TextLine> Lines, int PageCount, int CharacterCount);

/// <summary>Turns PDF pages into reading-order lines of styled tokens (single-column layout).</summary>
public static partial class PdfTextExtractor
{
    [GeneratedRegex("bold|black|heavy|semibold|demi", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BoldFontName();

    public static PdfTextContent Extract(PdfDocument document, CancellationToken ct)
    {
        var lines = new List<TextLine>();
        var characterCount = 0;

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var allWords = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters)
                .Where(w => w.Letters.Count > 0 && !string.IsNullOrWhiteSpace(w.Text))
                .ToList();
            var scripts = AttachScripts(allWords);
            var words = allWords
                .Where(w => !scripts.Attached.Contains(w))
                .OrderByDescending(w => w.Letters[0].StartBaseLine.Y)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var lineWords = new List<Word>();
            var lineY = double.NaN;

            foreach (var word in words)
            {
                var y = word.Letters[0].StartBaseLine.Y;
                var tolerance = Math.Max(1, word.Letters[0].PointSize * 0.3);
                if (lineWords.Count > 0 && Math.Abs(y - lineY) > tolerance)
                {
                    AddLine(lines, page.Number, lineWords, lineY, scripts, ref characterCount);
                    lineWords.Clear();
                }

                if (lineWords.Count == 0)
                {
                    lineY = y;
                }

                lineWords.Add(word);
            }

            AddLine(lines, page.Number, lineWords, lineY, scripts, ref characterCount);
        }

        return new PdfTextContent(lines, document.NumberOfPages, characterCount);
    }

    /// <summary>Superscripts and subscripts found on a page, keyed by the word they belong to.</summary>
    private sealed class Scripts
    {
        public HashSet<Word> Attached { get; } = [];
        public Dictionary<Word, List<(Word Script, bool Raised)>> ByHost { get; } = [];
    }

    /// <summary>Largest gap between a word and its exponent/index, and between the index and the next word (points).</summary>
    private const double ScriptGap = 1.5;

    /// <summary>
    /// Formulas set exponents and indices ("2^(log₂5)", "log₃") in a smaller font a few points above or below the
    /// baseline, so line grouping put them on lines of their own and they ended up after the question text ("… tapın. 3 2").
    /// A small word right after a body word on a nearby baseline is attached to that word instead.
    /// </summary>
    private static Scripts AttachScripts(List<Word> words)
    {
        var result = new Scripts();
        if (words.Count == 0)
        {
            return result;
        }

        var body = words.SelectMany(w => w.Letters)
            .GroupBy(l => Math.Round(l.PointSize * 2) / 2)
            .OrderByDescending(g => g.Count())
            .First().Key;
        if (body <= 0)
        {
            return result;
        }

        var bodyWords = words.Where(w => w.Letters[0].PointSize > body * 0.9).ToList();
        var hostOf = new Dictionary<Word, Word>();
        // Left to right, so "log₃²x" works: the exponent touches the index, which already belongs to "log".
        foreach (var script in words.Where(w => w.Letters[0].PointSize <= body * 0.9).OrderBy(w => w.BoundingBox.Left))
        {
            var y = script.Letters[0].StartBaseLine.Y;
            var host = bodyWords
                .Select(w => (Word: w, Dy: y - w.Letters[0].StartBaseLine.Y))
                .Where(c => Math.Abs(c.Dy) >= 1 && Math.Abs(c.Dy) <= body * 0.75
                    && Math.Abs(script.BoundingBox.Left - c.Word.BoundingBox.Right) <= ScriptGap)
                .OrderBy(c => Math.Abs(c.Dy))
                .FirstOrDefault();
            if (host.Word is null)
            {
                var previous = hostOf.Keys.FirstOrDefault(s => Math.Abs(script.BoundingBox.Left - s.BoundingBox.Right) <= ScriptGap
                    && Math.Abs(y - s.Letters[0].StartBaseLine.Y) <= body * 1.5);
                if (previous is null)
                {
                    continue;
                }

                var owner = hostOf[previous];
                host = (owner, y - owner.Letters[0].StartBaseLine.Y);
            }

            hostOf[script] = host.Word;
            result.Attached.Add(script);
            if (!result.ByHost.TryGetValue(host.Word, out var list))
            {
                result.ByHost[host.Word] = list = [];
            }

            list.Add((script, host.Dy > 0));
        }

        return result;
    }

    private const string SuperscriptSource = "0123456789+-−=()n";
    private const string SuperscriptTarget = "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁻⁼⁽⁾ⁿ";
    private const string SubscriptSource = "0123456789+-−=()";
    private const string SubscriptTarget = "₀₁₂₃₄₅₆₇₈₉₊₋₋₌₍₎";

    /// <summary>"2" → "²", "log₂5" → "^(log₂5)"; "3" → "₃", "ab" → "_(ab)".</summary>
    private static string FormatScript(string text, bool raised)
    {
        var (source, target, mark) = raised ? (SuperscriptSource, SuperscriptTarget, '^') : (SubscriptSource, SubscriptTarget, '_');
        return text.All(c => source.Contains(c))
            ? string.Concat(text.Select(c => target[source.IndexOf(c)]))
            : $"{mark}({text})";
    }

    private static void AddLine(List<TextLine> lines, int pageNumber, List<Word> words, double y, Scripts scripts, ref int characterCount)
    {
        var tokens = new List<TextToken>();
        var glueNext = false;
        foreach (var word in words.OrderBy(w => w.BoundingBox.Left))
        {
            var text = TextNormalizer.Normalize(word.Text);
            if (scripts.ByHost.TryGetValue(word, out var attached))
            {
                text += string.Concat(attached.OrderBy(s => s.Script.BoundingBox.Left)
                    .Select(s => FormatScript(TextNormalizer.Normalize(s.Script.Text), s.Raised)));
            }

            if (text.Length == 0)
            {
                continue;
            }

            // "log₃" directly followed by "(log₂8)": the index sat in the gap, so the words belong together.
            if (glueNext && tokens.Count > 0)
            {
                tokens[^1] = tokens[^1] with { Text = tokens[^1].Text + text };
                characterCount += text.Length;
                glueNext = NextIsGlued(word, attached, words);
                continue;
            }

            glueNext = NextIsGlued(word, attached, words);

            var boldLetters = word.Letters.Count(IsBold);
            var color = word.Letters
                .GroupBy(ColorKey)
                .OrderByDescending(g => g.Count())
                .First().Key;

            tokens.Add(new TextToken(text, boldLetters * 2 > word.Letters.Count, color));
            characterCount += text.Length;
        }

        if (tokens.Count > 0)
        {
            lines.Add(new TextLine(pageNumber, tokens, y));
        }
    }

    private static bool NextIsGlued(Word word, List<(Word Script, bool Raised)>? attached, List<Word> line)
    {
        if (attached is null || attached.Count == 0)
        {
            return false;
        }

        var end = attached.Max(s => s.Script.BoundingBox.Right);
        return line.Any(w => w != word && Math.Abs(w.BoundingBox.Left - end) <= 0.5 && !attached.Any(s => s.Script == w));
    }

    private static bool IsBold(Letter letter) =>
        letter.FontDetails?.IsBold == true
        || (letter.FontName is { } name && BoldFontName().IsMatch(name))
        || letter.RenderingMode == TextRenderingMode.FillThenStroke;

    private static string ColorKey(Letter letter)
    {
        var color = letter.FillColor ?? letter.Color;
        if (color is null)
        {
            return TextToken.DefaultColor;
        }

        var (r, g, b) = color.ToRGBValues();
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        // Black and dark grey body text all count as the default colour.
        if (max - min < 0.12 && max < 0.55)
        {
            return TextToken.DefaultColor;
        }

        return $"{Bucket(r)}{Bucket(g)}{Bucket(b)}";

        static int Bucket(double channel) => (int)Math.Round(channel * 7);
    }
}

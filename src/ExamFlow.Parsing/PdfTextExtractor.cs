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

            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters)
                .Where(w => w.Letters.Count > 0 && !string.IsNullOrWhiteSpace(w.Text))
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
                    AddLine(lines, page.Number, lineWords, lineY, ref characterCount);
                    lineWords.Clear();
                }

                if (lineWords.Count == 0)
                {
                    lineY = y;
                }

                lineWords.Add(word);
            }

            AddLine(lines, page.Number, lineWords, lineY, ref characterCount);
        }

        return new PdfTextContent(lines, document.NumberOfPages, characterCount);
    }

    private static void AddLine(List<TextLine> lines, int pageNumber, List<Word> words, double y, ref int characterCount)
    {
        var tokens = new List<TextToken>();
        foreach (var word in words.OrderBy(w => w.BoundingBox.Left))
        {
            var text = TextNormalizer.Normalize(word.Text);
            if (text.Length == 0)
            {
                continue;
            }

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

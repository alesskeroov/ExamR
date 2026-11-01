using System.Text.RegularExpressions;

namespace ExamFlow.Parsing;

/// <summary>Removes running headers, footers and page numbers.</summary>
public static partial class LayoutCleaner
{
    private const int EdgeLines = 2;

    [GeneratedRegex(@"^(səhifə\s*|page\s*)?\d{1,4}(\s*/\s*\d{1,4})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageNumber();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    /// <summary>Question or option starts are content, even when they repeat at page edges.</summary>
    [GeneratedRegex(@"^(cavab\s*[:：]|[iİıI]zah\s*[:：]|answer\s*[:：]|(sual|№)\s*\d|\d{1,4}\s*[.):\-]|[A-Fa-f]\s*[).]|[•●○◦▪■□√✓✔☑])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Structural();

    public static IReadOnlyList<TextLine> Clean(IReadOnlyList<TextLine> lines)
    {
        var pages = lines.GroupBy(l => l.PageNumber).ToList();
        var edgeLines = new HashSet<TextLine>(ReferenceEqualityComparer.Instance);
        foreach (var page in pages)
        {
            var pageLines = page.ToList();
            foreach (var line in pageLines.Take(EdgeLines).Concat(pageLines.TakeLast(EdgeLines)))
            {
                if (Structural().IsMatch(line.Text) && !PageNumber().IsMatch(line.Text))
                {
                    continue;
                }

                edgeLines.Add(line);
            }
        }

        var repeatedKeys = new HashSet<string>();
        if (pages.Count >= 3)
        {
            repeatedKeys = edgeLines
                .GroupBy(Key)
                .Where(g => g.Select(l => l.PageNumber).Distinct().Count() * 2 >= pages.Count)
                .Select(g => g.Key)
                .ToHashSet();
        }

        return lines
            .Where(l => !edgeLines.Contains(l) || (!repeatedKeys.Contains(Key(l)) && !PageNumber().IsMatch(l.Text)))
            .ToList();
    }

    private static string Key(TextLine line) => Digits().Replace(line.Text.ToLowerInvariant(), "#");
}

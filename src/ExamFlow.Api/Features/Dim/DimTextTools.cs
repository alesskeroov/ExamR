using System.Text.RegularExpressions;
using ExamFlow.Parsing;

namespace ExamFlow.Api.Features.Dim;

/// <param name="From">First question number (inclusive).</param>
/// <param name="To">Last question number (inclusive).</param>
public sealed record DimTopicRange(int From, int To, string Topic);

/// <summary>Plain-text helpers for fast bank entry: topic ranges and pasted tests.</summary>
public static partial class DimTextTools
{
    public const int MaxTopicLength = 200;

    /// <summary>
    /// Reads one range per line: "1-5: Fonetika", "6–10 Leksika", "11. Morfologiya". Returns the ranges and the lines
    /// that could not be read (blank lines are skipped).
    /// </summary>
    public static (List<DimTopicRange> Ranges, List<string> Unread) ParseTopicRanges(string text)
    {
        var ranges = new List<DimTopicRange>();
        var unread = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var match = TopicLine().Match(line);
            if (!match.Success)
            {
                unread.Add(line);
                continue;
            }

            var from = int.Parse(match.Groups["from"].Value);
            var to = match.Groups["to"].Success ? int.Parse(match.Groups["to"].Value) : from;
            var topic = match.Groups["topic"].Value.Trim();
            if (to < from || topic.Length == 0 || topic.Length > MaxTopicLength)
            {
                unread.Add(line);
                continue;
            }

            ranges.Add(new DimTopicRange(from, to, topic));
        }

        return (ranges, unread);
    }

    /// <summary>
    /// The topic of a question number: the last range that covers it, so a later line can refine an earlier one
    /// ("1-20: Cəbr" then "5: Faiz").
    /// </summary>
    public static string? TopicFor(IReadOnlyList<DimTopicRange> ranges, int number) =>
        ranges.LastOrDefault(r => number >= r.From && number <= r.To)?.Topic;

    /// <summary>
    /// Splits a pasted test (copied from Word, a web page or a PDF viewer) into questions with the same segmenter,
    /// answer detection and limits as uploaded files. Every line is read as plain text, so bold or coloured answers
    /// are not detected; an answer key does that afterwards.
    /// </summary>
    public static ParseResult ParsePastedTest(PdfQuestionParser parser, string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .SelectMany(SplitInlineOptions)
            .Select(l => new TextLine(1, [TextToken.Plain(l)]))
            .ToList();
        return parser.ParseLines(lines, 0);
    }

    /// <summary>
    /// "A) 3   B) 4   C) 5" on one line (common in pasted tests) becomes one line per option, with any stem before
    /// "A)" on its own line. Only a run that starts with "A)" and continues in order is split, so a stem that
    /// mentions "B)" alone stays whole.
    /// </summary>
    public static IEnumerable<string> SplitInlineOptions(string line)
    {
        var matches = InlineOption().Matches(line);
        var starts = new List<int>();
        foreach (Match match in matches)
        {
            var expected = (char)('A' + starts.Count);
            if (char.ToUpperInvariant(match.Groups["l"].Value[0]) != expected) continue;
            starts.Add(match.Index);
        }

        if (starts.Count < 2)
        {
            yield return line;
            yield break;
        }

        if (starts[0] > 0) yield return line[..starts[0]].Trim();
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : line.Length;
            yield return line[starts[i]..end].Trim();
        }
    }

    [GeneratedRegex(@"^(?<from>\d{1,4})\s*(?:[-–—]\s*(?<to>\d{1,4}))?(?:\s*[:.)\-–—]\s*|\s+)(?<topic>\S.*)$")]
    private static partial Regex TopicLine();

    [GeneratedRegex(@"(?:^|(?<=\s))(?<l>[A-Ea-e])\s*[).]\s")]
    private static partial Regex InlineOption();
}

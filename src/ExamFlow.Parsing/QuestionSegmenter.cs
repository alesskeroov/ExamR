using System.Text.RegularExpressions;

namespace ExamFlow.Parsing;

/// <summary>
/// State machine that splits lines into questions and options.
/// Question numbers are accepted only in sequence, so numbers inside question text are not mistaken for new questions.
/// </summary>
public static partial class QuestionSegmenter
{
    public const int MaxOptions = 6;
    private const int MaxNumberJump = 10;

    /// <summary>Bullet-style option markers. Options get letters A, B, C… in order.</summary>
    public const string OptionMarkers = "•●○◦▪■□";

    /// <summary>Markers that both start an option and mark it as the correct answer.</summary>
    public const string CorrectMarkers = "√✓✔☑";

    /// <summary>
    /// Furthest a line may sit from a centred anchor and still belong to its cell (about one line of a
    /// multi-line cell). Also the assumed distance to an anchor on the previous page.
    /// </summary>
    private const double MaxCellLineDistance = 12;

    [GeneratedRegex(@"^(?:sual|№|\d{1,4}[.):\-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuestionNumberToken();

    [GeneratedRegex(@"^(?:cavab|[iİıI]zah|answer)\s*[:：]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnswerStart();

    public static bool IsMarker(char c) => OptionMarkers.Contains(c) || CorrectMarkers.Contains(c);

    [GeneratedRegex(@"^(?:(?<prefix>sual|№)\s*(?<n>\d{1,4})\s*[.):\-]?|(?<n>\d{1,4})\s*[.):\-])\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuestionStart();

    [GeneratedRegex(@"^(?<l>[A-Fa-f])(?<d>[).])(?<rest>.*)$")]
    private static partial Regex LabelToken();

    public static IReadOnlyList<RawQuestion> Segment(IEnumerable<TextLine> lines)
    {
        var questions = new List<RawQuestion>();
        RawQuestion? current = null;

        foreach (var line in AttachCenteredAnchors(lines.ToList()))
        {
            var match = QuestionStart().Match(line.Text);
            if (match.Success
                && int.TryParse(match.Groups["n"].Value, out var number)
                && AcceptsNumber(current, number, match.Groups["prefix"].Success))
            {
                var next = new RawQuestion(number);
                if (current is not null && number != current.Number + 1)
                {
                    next.Warnings.Add($"Sual nömrəsi {current.Number}-dən {number}-ə keçir");
                }

                current = next;
                questions.Add(current);
                Append(current.TextTokens, SkipChars(line.Tokens, match.Length));
                continue;
            }

            if (current is null)
            {
                continue; // Title, instructions and other text before the first question.
            }

            var answer = AnswerStart().Match(line.Text);
            if (current.Options.Count == 0 && answer.Success)
            {
                current.ReadingAnswer = true;
                Append(current.AnswerTokens, SkipChars(line.Tokens, answer.Length));
                continue;
            }
            if (current.ReadingAnswer)
            {
                Append(current.AnswerTokens, line.Tokens);
                continue;
            }

            if (TryReadLabel(line.Tokens, 0, ExpectedLetter(current), allowDot: true, out var consumed, out var rest))
            {
                ReadOptions(current, line.Tokens, consumed, rest);
                continue;
            }

            Append(current.Options.Count > 0 ? current.Options[^1].TextTokens : current.TextTokens, line.Tokens);
        }

        return questions;
    }

    /// <summary>
    /// Table layouts centre the question number or option marker vertically in its cell, so part of a
    /// multi-line cell is extracted *above* the marker line. Each line without a number/marker ("orphan")
    /// is assigned to the vertically nearest anchor; on a tie it stays with the anchor above.
    /// Orphans that belong to the anchor below are moved into that anchor's line, after its prefix.
    /// </summary>
    private static List<TextLine> AttachCenteredAnchors(List<TextLine> lines)
    {
        if (lines.Count == 0 || lines.Any(l => double.IsNaN(l.Y)))
        {
            return lines;
        }

        var prefixes = lines.Select(AnchorPrefixLength).ToArray();
        var result = new List<TextLine>(lines.Count);
        var pending = new List<TextToken>();
        var previousAnchor = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (prefixes[i] > 0)
            {
                result.Add(pending.Count == 0
                    ? line
                    : new TextLine(line.PageNumber, [.. line.Tokens.Take(prefixes[i]), .. pending, .. line.Tokens.Skip(prefixes[i])], line.Y));
                pending.Clear();
                previousAnchor = i;
                continue;
            }

            var nextAnchor = Array.FindIndex(prefixes, i + 1, p => p > 0);
            var toPrevious = previousAnchor < 0
                ? double.PositiveInfinity
                : lines[previousAnchor].PageNumber == line.PageNumber ? Math.Abs(lines[previousAnchor].Y - line.Y) : MaxCellLineDistance;
            var toNext = nextAnchor < 0 || lines[nextAnchor].PageNumber != line.PageNumber
                ? double.PositiveInfinity
                : Math.Abs(lines[nextAnchor].Y - line.Y);

            if (toNext < toPrevious && toNext <= MaxCellLineDistance)
            {
                pending.AddRange(line.Tokens);
            }
            else
            {
                result.Add(line);
            }
        }

        if (pending.Count > 0)
        {
            result.Add(new TextLine(lines[^1].PageNumber, pending, lines[^1].Y));
        }

        return result;
    }

    /// <summary>Number of leading tokens that make the line a question/option start, or 0.</summary>
    private static int AnchorPrefixLength(TextLine line)
    {
        var first = line.Tokens[0].Text;
        if (IsMarker(first[0]))
        {
            return 1;
        }

        if (!QuestionNumberToken().IsMatch(first))
        {
            return 0;
        }

        var match = QuestionStart().Match(line.Text);
        if (!match.Success)
        {
            return 0;
        }

        // The match ends with the whitespace after the number; that whitespace is not part of the prefix.
        var prefixLength = match.Value.TrimEnd().Length;
        var count = 0;
        var position = 0;
        foreach (var token in line.Tokens)
        {
            position += token.Text.Length;
            count++;
            if (position >= prefixLength)
            {
                break;
            }

            position++; // joining space
        }

        return count;
    }

    private static bool AcceptsNumber(RawQuestion? current, int number, bool hasPrefix) =>
        current is null
        || number == current.Number + 1
        || (hasPrefix && number > current.Number)
        || (current.Options.Count >= 2 && number > current.Number && number <= current.Number + MaxNumberJump);

    private static char? ExpectedLetter(RawQuestion question) =>
        question.Options.Count < MaxOptions ? (char)('A' + question.Options.Count) : null;

    /// <summary>Reads one option starting at the line, plus further inline options ("A) x  B) y").</summary>
    private static void ReadOptions(RawQuestion question, IReadOnlyList<TextToken> tokens, int consumed, TextToken[] labelAndRest)
    {
        var option = StartOption(question, labelAndRest);
        var lettered = !IsMarker(labelAndRest[0].Text[0]);
        for (var i = consumed; i < tokens.Count; i++)
        {
            // Inline options require ")" so that text like "Vitamin C." is not taken as a label.
            // Bullet-marker options are one per line, so "B)" inside their text stays text.
            if (lettered && TryReadLabel(tokens, i, ExpectedLetter(question), allowDot: false, out var used, out var rest))
            {
                option = StartOption(question, rest);
                i += used - 1;
                continue;
            }

            Append(option.TextTokens, [tokens[i]]);
        }
    }

    private static RawOption StartOption(RawQuestion question, TextToken[] labelAndRest)
    {
        var option = new RawOption((char)('A' + question.Options.Count));
        option.LabelTokens.Add(labelAndRest[0]);
        option.TextTokens.AddRange(labelAndRest.Skip(1));
        question.Options.Add(option);
        return option;
    }

    /// <summary>
    /// Recognises "B)", "B.", "B)Text" or "B" followed by ")" / "." / "-".
    /// <paramref name="labelAndRest"/> holds the label token, then any text glued to it.
    /// </summary>
    private static bool TryReadLabel(
        IReadOnlyList<TextToken> tokens, int index, char? expected, bool allowDot,
        out int consumed, out TextToken[] labelAndRest)
    {
        consumed = 0;
        labelAndRest = [];
        if (expected is null || index >= tokens.Count)
        {
            return false;
        }

        var token = tokens[index];

        // Bullet markers ("• text", "√ text", "•text") start an option at the beginning of a line only.
        if (allowDot && index == 0 && IsMarker(token.Text[0]))
        {
            consumed = 1;
            labelAndRest = token.Text.Length > 1
                ? [token with { Text = token.Text[..1] }, token with { Text = token.Text[1..] }]
                : [token];
            return true;
        }

        var match = LabelToken().Match(token.Text);
        if (match.Success
            && char.ToUpperInvariant(match.Groups["l"].Value[0]) == expected
            && (allowDot || match.Groups["d"].Value == ")"))
        {
            consumed = 1;
            var rest = match.Groups["rest"].Value;
            labelAndRest = rest.Length > 0
                ? [token with { Text = token.Text[..2] }, token with { Text = rest }]
                : [token];
            return true;
        }

        if (token.Text.Length == 1 && char.ToUpperInvariant(token.Text[0]) == expected && index + 1 < tokens.Count)
        {
            var delimiter = tokens[index + 1].Text;
            if (delimiter == ")" || (allowDot && delimiter is "." or "-" or "–"))
            {
                consumed = 2;
                labelAndRest = [token];
                return true;
            }
        }

        return false;
    }

    /// <summary>Drops the first <paramref name="count"/> characters of the space-joined token text.</summary>
    private static List<TextToken> SkipChars(IReadOnlyList<TextToken> tokens, int count)
    {
        var result = new List<TextToken>();
        var position = 0;
        foreach (var token in tokens)
        {
            var end = position + token.Text.Length;
            if (end <= count)
            {
                position = end + 1;
                continue;
            }

            result.Add(position < count ? token with { Text = token.Text[(count - position)..] } : token);
            position = end + 1;
        }

        return result;
    }

    /// <summary>Appends tokens, re-joining words hyphenated across a line break ("verilən-" + "lər").</summary>
    private static void Append(List<TextToken> target, IReadOnlyList<TextToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        if (target.Count > 0
            && target[^1].Text is { Length: > 1 } last && last.EndsWith('-')
            && char.IsLower(tokens[0].Text[0]))
        {
            target[^1] = target[^1] with { Text = last[..^1] + tokens[0].Text };
            target.AddRange(tokens.Skip(1));
            return;
        }

        target.AddRange(tokens);
    }
}

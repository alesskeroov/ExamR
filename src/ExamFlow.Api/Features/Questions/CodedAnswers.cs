using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace ExamFlow.Api.Features.Questions;

/// <summary>
/// Answers of open (kodlaşdırılan) tasks. A key may hold several accepted answers separated by ';' (the comma is the
/// decimal separator in Azerbaijan; ';' inside brackets belongs to a coordinate pair). Numeric answers are compared as
/// exact numbers, so "2,5", "2.5", "5/2" and "+2,50" match, "80°" matches "80", and "(3; -4)" matches "(3;−4)"; nothing
/// is rounded. Any other answer (a word, e.g. in Azerbaijani language tasks) is compared as text, ignoring letter case
/// (Azerbaijani rules), spaces and closing punctuation.
/// </summary>
public static partial class CodedAnswers
{
    public const int MaxKeyLength = 100;
    public const int MaxAnswerLength = 30;
    public const char Separator = ';';

    /// <summary>An exact rational number: <see cref="Numerator"/> / <see cref="Denominator"/>, denominator &gt; 0.</summary>
    public readonly record struct Number(BigInteger Numerator, BigInteger Denominator)
    {
        /// <summary>Equal as numbers: 5/2 equals 25/10.</summary>
        public bool SameValue(Number other) => Numerator * other.Denominator == other.Numerator * Denominator;
    }

    /// <summary>
    /// Parses "-3", "2,5", "0.25", "5/2", "−1/4", also with a unit after it ("80°", "40 sm²", "15%"): the unit is not
    /// compared, as on the DİM answer sheet. Null when the text is not a number.
    /// </summary>
    public static Number? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var (body, _) = SplitUnit(Compact(text));
        return ParseNumber(body);
    }

    /// <summary>
    /// A coded answer: one number, or several in brackets — coordinates "(3; −4)", "(2,5; 1)", or "(2,2)" when there is
    /// no ';'. Without brackets "3; -4" is read as a pair too (a student answer has no alternatives). Null when invalid.
    /// </summary>
    public static IReadOnlyList<Number>? ParseValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var (body, _) = SplitUnit(Compact(text));
        if (ParseNumber(body) is { } single) return [single];

        var bracketed = body.Length > 1 && body[0] is '(' or '[' && body[^1] is ')' or ']';
        var inner = bracketed ? body[1..^1] : body;
        var separator = inner.Contains(';') ? ';' : bracketed ? ',' : '\0';
        if (separator == '\0') return null;
        var parts = inner.Split(separator);
        if (parts.Length < 2) return null;
        var numbers = new List<Number>(parts.Length);
        foreach (var part in parts)
        {
            if (ParseNumber(part) is not { } number) return null;
            numbers.Add(number);
        }

        return numbers;
    }

    /// <summary>Whether a student's answer can be graded: anything but blank (word answers are compared as text).</summary>
    public static bool IsValidAnswer(string? text) => !string.IsNullOrWhiteSpace(text) && TextKey(text).Length > 0;

    private static Number? ParseNumber(string text)
    {
        var normalized = text.Replace(',', '.');
        var match = NumberPattern().Match(normalized);
        if (!match.Success) return null;

        var negative = match.Groups["sign"].Value == "-";
        if (Decimal(match.Groups["a"].Value) is not { } a) return null;
        var result = a;
        if (match.Groups["b"].Success)
        {
            if (Decimal(match.Groups["b"].Value) is not { } b || b.Numerator.IsZero) return null;
            result = new Number(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        }

        return negative ? result with { Numerator = -result.Numerator } : result;
    }

    /// <summary>"12.50" → 1250/100; up to 18 digits on each side of the point.</summary>
    private static Number? Decimal(string text)
    {
        var parts = text.Split('.');
        var digits = parts[0] + (parts.Length > 1 ? parts[1] : "");
        if (digits.Length == 0 || digits.Length > 36) return null;
        var scale = parts.Length > 1 ? parts[1].Length : 0;
        return new Number(BigInteger.Parse(digits, CultureInfo.InvariantCulture), BigInteger.Pow(10, scale));
    }

    /// <summary>
    /// The accepted answers of a key, in order; empty parts are skipped. ';' inside brackets belongs to a coordinate
    /// pair ("(3; -4); (-4; 3)" is two answers).
    /// </summary>
    public static IReadOnlyList<string> Split(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i <= key.Length; i++)
        {
            if (i < key.Length && key[i] is '(' or '[') depth++;
            else if (i < key.Length && key[i] is ')' or ']') depth = Math.Max(0, depth - 1);
            else if (i == key.Length || (key[i] == Separator && depth == 0))
            {
                var part = key[start..i].Trim();
                if (part.Length > 0) parts.Add(part);
                start = i + 1;
            }
        }

        return parts;
    }

    /// <summary>
    /// Cleans an admin's key ("2,5 ; 5/2", "(3;−4)", "40 sm²") into its stored form ("2,5; 5/2", "(3; -4)", "40 sm²").
    /// Returns an error message when a part is not a number or a bracketed group of numbers, or the key is empty or too long.
    /// </summary>
    public static (string? Key, string? Error) NormalizeKey(string? key)
    {
        var parts = Split(key);
        if (parts.Count == 0) return (null, "Açıq sualın düzgün cavabını yazın (məs. 2,5; (3; -4); 80°; mübtəda).");
        if (parts.FirstOrDefault(p => TextKey(p).Length == 0) is { } bad)
            return (null, $"\"{bad}\" cavab deyil: ədəd, mötərizədə ədədlər və ya söz yazın.");
        var stored = string.Join($"{Separator} ", parts.Select(Display));
        return stored.Length > MaxKeyLength ? (null, $"Cavab ən çox {MaxKeyLength} simvol ola bilər.") : (stored, null);
    }

    /// <summary>
    /// Whether the student's answer equals any accepted answer of the key: numeric answers by value (same count of equal
    /// numbers), word answers by <see cref="TextKey"/>.
    /// </summary>
    public static bool Matches(string? key, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var given = ParseValue(answer);
        var givenText = TextKey(answer);
        return Split(key).Any(part => ParseValue(part) is { } accepted
            ? given is not null && accepted.Count == given.Count && accepted.Zip(given).All(pair => pair.First.SameValue(pair.Second))
            : givenText.Length > 0 && TextKey(part) == givenText);
    }

    private static readonly CultureInfo Azerbaijani = CultureInfo.GetCultureInfo("az-Latn-AZ");

    /// <summary>A word answer as compared: lower case by Azerbaijani rules (İ→i, I→ı), no spaces, no closing punctuation or quotes.</summary>
    private static string TextKey(string text) =>
        WhiteSpace().Replace(text, "").ToLower(Azerbaijani).Trim('.', ',', '!', '?', ';', ':', '"', '\'', '«', '»', '“', '”');

    /// <summary>
    /// Stored spelling of one accepted answer. Numbers: no inner spaces, "; " inside brackets, a space before a word unit.
    /// Words: as typed, with runs of spaces collapsed.
    /// </summary>
    private static string Display(string part)
    {
        if (ParseValue(part) is null) return WhiteSpace().Replace(part, " ").Trim();
        var (body, unit) = SplitUnit(Compact(part));
        if (body.Length > 1 && body[0] is '(' or '[')
        {
            var inner = body[1..^1];
            var pieces = inner.Contains(';') ? inner.Split(';') : inner.Split(',');
            body = $"({string.Join("; ", pieces)})";
        }

        return unit.Length == 0 ? body : unit[0] is '°' or '%' or '‰' ? body + unit : $"{body} {unit}";
    }

    /// <summary>No spaces, and the various minus signs as '-'.</summary>
    private static string Compact(string text) => WhiteSpace().Replace(text, "").Replace('−', '-').Replace('–', '-');

    /// <summary>"40sm²" → ("40", "sm²"); "80°" → ("80", "°"); "(3;-4)" → ("(3;-4)", "").</summary>
    private static (string Body, string Unit) SplitUnit(string text)
    {
        var match = UnitSuffix().Match(text);
        return match.Success ? (text[..match.Index], match.Value) : (text, "");
    }

    // A unit after the last digit or bracket: degrees, percent, or a short word with an optional ²/³ ("sm²", "kq", "m³").
    [GeneratedRegex(@"(?<=[\d)\]])(?:[°%‰]|\p{L}{1,4}[²³]?)$")]
    private static partial Regex UnitSuffix();

    /// <summary>The student's answer as stored: trimmed, runs of spaces collapsed; null when blank.</summary>
    public static string? CleanAnswer(string? answer) =>
        string.IsNullOrWhiteSpace(answer) ? null : WhiteSpace().Replace(answer, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"^(?<sign>[+-]?)(?<a>\d+(\.\d+)?|\.\d+)(/(?<b>\d+(\.\d+)?))?$")]
    private static partial Regex NumberPattern();
}

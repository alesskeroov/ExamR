using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace ExamFlow.Api.Features.Questions;

/// <summary>
/// Answers of open coded (kodlaşdırılan) tasks: the student writes a number into the answer grid. A key may hold several
/// accepted answers separated by ';' (the comma is the decimal separator in Azerbaijan). Answers are compared as exact
/// numbers, so "2,5", "2.5", "5/2" and "+2,50" all match each other; nothing is rounded.
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

    /// <summary>Parses "-3", "2,5", "0.25", "5/2", "−1/4". Null when the text is not a number.</summary>
    public static Number? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = WhiteSpace().Replace(text, "").Replace(',', '.').Replace('−', '-').Replace('–', '-');
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

    /// <summary>The accepted answers of a key, in order; empty parts are skipped.</summary>
    public static IReadOnlyList<string> Split(string? key) =>
        string.IsNullOrWhiteSpace(key) ? [] : key.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Cleans an admin's key ("2,5 ; 5/2") into its stored form ("2,5; 5/2"). Returns an error message when a part is
    /// not a number, or the key is empty or too long.
    /// </summary>
    public static (string? Key, string? Error) NormalizeKey(string? key)
    {
        var parts = Split(key);
        if (parts.Count == 0) return (null, "Kodlaşdırılan sualın düzgün cavabını yazın (məs. 2,5 və ya 5/2).");
        if (parts.FirstOrDefault(p => Parse(p) is null) is { } bad)
            return (null, $"\"{bad}\" ədəd deyil. Cavab tam ədəd, onluq kəsr (2,5) və ya adi kəsr (5/2) olmalıdır.");
        var stored = string.Join($"{Separator} ", parts.Select(p => WhiteSpace().Replace(p, "")));
        return stored.Length > MaxKeyLength ? (null, $"Cavab ən çox {MaxKeyLength} simvol ola bilər.") : (stored, null);
    }

    /// <summary>Whether the student's answer equals any accepted answer of the key.</summary>
    public static bool Matches(string? key, string? answer) =>
        Parse(answer) is { } given && Split(key).Any(p => Parse(p) is { } accepted && accepted.SameValue(given));

    /// <summary>The student's answer as stored: trimmed, inner spaces removed; null when blank.</summary>
    public static string? CleanAnswer(string? answer) =>
        string.IsNullOrWhiteSpace(answer) ? null : WhiteSpace().Replace(answer, "");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"^(?<sign>[+-]?)(?<a>\d+(\.\d+)?|\.\d+)(/(?<b>\d+(\.\d+)?))?$")]
    private static partial Regex NumberPattern();
}

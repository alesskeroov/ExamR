using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ExamFlow.Parsing;

/// <summary>SHA-256 of the normalised question and its options, independent of option order.</summary>
public static partial class ContentHasher
{
    private static readonly CultureInfo Azerbaijani = CultureInfo.GetCultureInfo("az-Latn-AZ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static byte[] Compute(string questionText, IEnumerable<string> optionTexts)
    {
        var options = optionTexts.Select(Normalize).Order(StringComparer.Ordinal);
        var payload = Normalize(questionText) + "\n" + string.Join("\n", options);
        return SHA256.HashData(Encoding.UTF8.GetBytes(payload));
    }

    public static string Normalize(string text) =>
        Whitespace().Replace(text.ToLower(Azerbaijani), " ").Trim().TrimEnd('.', ',', ';', ':', '?', '!').TrimEnd();
}

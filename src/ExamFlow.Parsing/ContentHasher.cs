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

    /// <summary>
    /// <paramref name="visualKeys"/>: hashes of the question's images. Without images the result is exactly the
    /// text-only hash, so stored hashes of text questions do not change; same text with different images differs.
    /// </summary>
    public static byte[] Compute(string questionText, IEnumerable<string> optionTexts, IEnumerable<string>? visualKeys = null)
    {
        var options = optionTexts.Select(Normalize).Order(StringComparer.Ordinal);
        var payload = Normalize(questionText) + "\n" + string.Join("\n", options);
        var visuals = visualKeys?.Order(StringComparer.Ordinal).ToList() ?? [];
        if (visuals.Count > 0)
        {
            payload += "\n\0visuals:" + string.Join(",", visuals);
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(payload));
    }

    public static string Normalize(string text) =>
        Whitespace().Replace(text.ToLower(Azerbaijani), " ").Trim().TrimEnd('.', ',', ';', ':', '?', '!').TrimEnd();
}

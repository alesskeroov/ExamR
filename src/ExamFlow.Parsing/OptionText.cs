using System.Text.RegularExpressions;

namespace ExamFlow.Parsing;

/// <summary>Removes answer hints that source files print inside the correct option's text.</summary>
public static partial class OptionText
{
    // "C) ✓ text": the check mark after a letter label marks the correct option. "√3", "√(x+1)", "√e" are square roots.
    [GeneratedRegex(@"^\s*(?:[✓✔☑]|√(?![\d(\p{Ll}]))\s*")]
    private static partial Regex LeadingMarker();

    // "B) 6 ✓ DOĞRU": a check mark at the end of the option, optionally followed by a "correct" word.
    [GeneratedRegex(@"(?:\s*[✓✔☑]|\s+√)\s*(?:doğru|düzgün|correct|true)?\s*[.!]?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingMarker();

    // Source references printed only after the correct option ("Mühazirə istinadı: Mövzu 5; …") give the answer away.
    [GeneratedRegex(@"\s*(?:Mühazirə|Mənbə)\s+istinadı\s*[:：].*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex AnswerReference();

    public static bool HasCorrectMarker(string text) => LeadingMarker().IsMatch(text) || TrailingMarker().IsMatch(text);

    public static string Clean(string text)
    {
        var cleaned = TrailingMarker().Replace(AnswerReference().Replace(LeadingMarker().Replace(text, ""), ""), "").Trim();
        // Never empty an option: an option that is only a reference keeps its original text.
        return cleaned.Length > 0 ? cleaned : text.Trim();
    }
}

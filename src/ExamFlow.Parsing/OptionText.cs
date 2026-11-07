using System.Text.RegularExpressions;

namespace ExamFlow.Parsing;

/// <summary>Removes answer hints that source files print inside the correct option's text.</summary>
public static partial class OptionText
{
    // "C) ✓ text": the check mark after a letter label marks the correct option.
    [GeneratedRegex(@"^\s*[√✓✔☑]\s*")]
    private static partial Regex LeadingMarker();

    // Source references printed only after the correct option ("Mühazirə istinadı: Mövzu 5; …") give the answer away.
    [GeneratedRegex(@"\s*(?:Mühazirə|Mənbə)\s+istinadı\s*[:：].*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex AnswerReference();

    public static bool HasCorrectMarker(string text) => LeadingMarker().IsMatch(text);

    public static string Clean(string text)
    {
        var cleaned = AnswerReference().Replace(LeadingMarker().Replace(text, ""), "").Trim();
        // Never empty an option: an option that is only a reference keeps its original text.
        return cleaned.Length > 0 ? cleaned : text.Trim();
    }
}

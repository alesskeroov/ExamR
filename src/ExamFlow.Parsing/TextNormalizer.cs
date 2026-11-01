using System.Text;

namespace ExamFlow.Parsing;

public static class TextNormalizer
{
    private static readonly (string From, string To)[] Ligatures =
    [
        ("ﬀ", "ff"), ("ﬁ", "fi"), ("ﬂ", "fl"), ("ﬃ", "ffi"), ("ﬄ", "ffl"),
    ];

    public static string Normalize(string text)
    {
        var result = text.Normalize(NormalizationForm.FormC);
        foreach (var (from, to) in Ligatures)
        {
            result = result.Replace(from, to, StringComparison.Ordinal);
        }

        return result.Replace(' ', ' ').Replace("­", string.Empty, StringComparison.Ordinal).Trim();
    }
}

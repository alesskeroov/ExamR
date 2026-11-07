namespace ExamFlow.Parsing.Tests;

public class OptionTextTests
{
    private static TextLine L(string text) => new(1, text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(TextToken.Plain).ToList());

    [Fact]
    public void Check_mark_after_a_letter_label_marks_the_answer_and_is_removed_from_the_text()
    {
        var result = new PdfQuestionParser().ParseLines([
            L("1. Çoxmərhələli build son image-i kiçiltməyə necə kömək edir?"),
            L("A) Bütün build alətləri son image-də saxlanır"),
            L("B) ✓ Build asılılıqları aralıq mərhələdə qalır Mühazirə istinadı: Mövzu 5; 3.6 Dockerfile və build keşi; s. 11"),
            L("C) Image qatları volume-larla əvəz edilir"),
        ], 1);

        var question = Assert.Single(result.Questions);
        Assert.Equal(1, question.CorrectIndex);
        Assert.Equal("Build asılılıqları aralıq mərhələdə qalır", question.Options[1].Text);
        Assert.All(question.Options, o => Assert.DoesNotContain("✓", o.Text));
    }

    [Theory]
    [InlineData("✓ HTTPS", "HTTPS")]
    [InlineData("√HTTPS", "HTTPS")]
    [InlineData("HTTPS Mənbə istinadı: s. 4", "HTTPS")]
    [InlineData("Şəbəkə ✓ səviyyəsi", "Şəbəkə ✓ səviyyəsi")]
    [InlineData("Mühazirə istinadı: s. 2", "Mühazirə istinadı: s. 2")]
    public void Clean_removes_only_leading_marks_and_trailing_answer_references(string text, string expected)
    {
        Assert.Equal(expected, OptionText.Clean(text));
    }
}

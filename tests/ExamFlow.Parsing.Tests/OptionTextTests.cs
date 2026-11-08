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

    [Fact]
    public void Check_mark_at_the_end_of_an_option_marks_the_answer_and_is_removed_from_the_text()
    {
        var result = new PdfQuestionParser().ParseLines([
            L("001. 48 və 42 ədədlərinin ƏBOB-u neçədir?"),
            L("A) 11"),
            L("B) 6 ✓ DOĞRU"),
            L("C) 10"),
            L("D) 8"),
        ], 1);

        var question = Assert.Single(result.Questions);
        Assert.Equal(1, question.CorrectIndex);
        Assert.Equal("6", question.Options[1].Text);
        Assert.All(question.Options, o => Assert.DoesNotContain("✓", o.Text));
    }

    [Theory]
    [InlineData("√3")]
    [InlineData("√e")]
    [InlineData("2√3")]
    [InlineData("√(7 + 4√3)")]
    public void Square_roots_are_not_answer_marks(string text)
    {
        Assert.False(OptionText.HasCorrectMarker(text));
        Assert.Equal(text, OptionText.Clean(text));
    }

    [Fact]
    public void Square_root_options_do_not_hide_the_marked_answer()
    {
        var result = new PdfQuestionParser().ParseLines([
            L("6. √(7 + 4√3) − √(7 − 4√3) ifadəsinin qiymətini tapın."),
            L("A) 4"), L("B) 2√3 ✓"), L("C) 4√3"), L("D) √e"), L("E) √3"),
        ], 1);

        var question = Assert.Single(result.Questions);
        Assert.Equal(1, question.CorrectIndex);
        Assert.Equal(["4", "2√3", "4√3", "√e", "√3"], question.Options.Select(o => o.Text));
    }

    [Theory]
    [InlineData("6 ✓ DOĞRU", "6")]
    [InlineData("24 ✓", "24")]
    [InlineData("HTTPS ✔ düzgün", "HTTPS")]
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

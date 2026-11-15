using ExamFlow.Api.Features.Dim;
using ExamFlow.Parsing;

namespace ExamFlow.Api.Tests;

public class DimTextToolsTests
{
    [Fact]
    public void Topic_ranges_accept_common_formats_and_report_unread_lines()
    {
        var (ranges, unread) = DimTextTools.ParseTopicRanges("1-5: Fonetika\n6–10 Leksika\n\n11. Morfologiya\n12-14:Sintaksis\nqarışıq sətir\n9-3: Səhv");

        Assert.Equal([(1, 5, "Fonetika"), (6, 10, "Leksika"), (11, 11, "Morfologiya"), (12, 14, "Sintaksis")],
            ranges.Select(r => (r.From, r.To, r.Topic)));
        Assert.Equal(["qarışıq sətir", "9-3: Səhv"], unread);
    }

    [Fact]
    public void A_later_range_refines_an_earlier_one()
    {
        var (ranges, _) = DimTextTools.ParseTopicRanges("1-20: Cəbr\n5: Faiz");

        Assert.Equal("Cəbr", DimTextTools.TopicFor(ranges, 4));
        Assert.Equal("Faiz", DimTextTools.TopicFor(ranges, 5));
        Assert.Null(DimTextTools.TopicFor(ranges, 21));
    }

    [Fact]
    public void Inline_options_are_split_and_a_lone_letter_in_a_stem_is_not()
    {
        Assert.Equal(["2 + 2 = ?", "A) 3", "B) 4", "C) 5"], DimTextTools.SplitInlineOptions("2 + 2 = ? A) 3 B) 4 C) 5"));
        Assert.Equal(["A. 12", "B. 15"], DimTextTools.SplitInlineOptions("A. 12 B. 15"));
        Assert.Equal(["Hansı bəndə B) daxildir?"], DimTextTools.SplitInlineOptions("Hansı bəndə B) daxildir?"));
    }

    [Fact]
    public void A_pasted_test_becomes_closed_and_open_questions()
    {
        const string text = """
            1. 2 + 2 neçədir?
            A) 3   B) 4   C) 5   D) 6   E) 7
            2. Paytaxtı seçin:
            A) Bakı
            B) Gəncə
            C) Şəki
            D) Quba
            E) Lənkəran
            3. 1/2 + 2 cəmini tapın.
            """;

        var result = DimTextTools.ParsePastedTest(new PdfQuestionParser(), text);

        Assert.Equal([1, 2, 3], result.Questions.Select(q => q.Number));
        Assert.Equal(["3", "4", "5", "6", "7"], result.Questions[0].Options.Select(o => o.Text));
        Assert.Equal(5, result.Questions[1].Options.Count);
        Assert.Empty(result.Questions[2].Options);
        Assert.Contains("1/2 + 2", result.Questions[2].Text);
    }

    [Theory]
    [InlineData(new byte[] { 0x49, 0x44, 0x33, 4, 0, 0, 0, 0, 0, 0, 0, 0 }, "audio/mpeg")]
    [InlineData(new byte[] { 0xFF, 0xFB, 0x90, 0x64, 0, 0, 0, 0, 0, 0, 0, 0 }, "audio/mpeg")]
    [InlineData(new byte[] { 0, 0, 0, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20 }, "audio/mp4")]
    [InlineData(new byte[] { 0x4F, 0x67, 0x67, 0x53, 0, 2, 0, 0, 0, 0, 0, 0 }, "audio/ogg")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, "audio/wav")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0, 0, 0, 0 }, null)]
    public void Audio_format_is_recognised_by_its_first_bytes(byte[] head, string? contentType) =>
        Assert.Equal(contentType, DimAudioFormat.Detect(head)?.ContentType);
}

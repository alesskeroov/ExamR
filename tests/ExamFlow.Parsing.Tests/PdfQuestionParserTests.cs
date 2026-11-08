namespace ExamFlow.Parsing.Tests;

public class PdfQuestionParserTests
{
    [Fact]
    public void Parses_standard_format_and_detects_bold_answer()
    {
        var result = new TestPdf()
            .Line("Sual 1: What is ASP.NET Core?")
            .Line("A) Database system")
            .Line(new Run("B)", Bold: true), new Run("Web framework", Bold: true))
            .Line("C) Operating system")
            .Line("D) Programming language")
            .Parse();

        Assert.True(result.IsSuccess);
        var question = Assert.Single(result.Questions);
        Assert.Equal(1, question.Number);
        Assert.Equal("What is ASP.NET Core?", question.Text);
        Assert.Equal(["Database system", "Web framework", "Operating system", "Programming language"], question.Options.Select(o => o.Text));
        Assert.Equal(["A", "B", "C", "D"], question.Options.Select(o => o.Label));
        Assert.Equal(1, question.CorrectIndex);
        Assert.Null(question.Warning);
    }

    [Fact]
    public void Empty_detector_list_falls_back_to_style_detection()
    {
        var bytes = new TestPdf()
            .Line("1. Question").Line("A) one").Line(new Run("B) two", Bold: true)).Line("C) three")
            .Build();

        var result = new PdfQuestionParser([]).Parse(new MemoryStream(bytes));

        Assert.Equal(1, Assert.Single(result.Questions).CorrectIndex);
    }

    [Fact]
    public void Exponents_and_indices_stay_inside_the_formula()
    {
        var result = new TestPdf()
            .Line(new Run("1. Hesablayin: x"), new Run("2", Rise: 5, Size: 8, NoGap: true), new Run("+ 2"),
                new Run("n+1", Rise: 5, Size: 8, NoGap: true), new Run("+ log"), new Run("3", Rise: -4, Size: 8, NoGap: true),
                new Run("9 = ?", NoGap: true))
            .Line("A) 1").Line(new Run("B) 2", Bold: true)).Line("C) 3")
            .Parse();

        var question = Assert.Single(result.Questions);
        Assert.Equal("Hesablayin: x² + 2ⁿ⁺¹ + log₃9 = ?", question.Text);
        Assert.Equal(["1", "2", "3"], question.Options.Select(o => o.Text));
        Assert.Equal(1, question.CorrectIndex);
    }

    [Fact]
    public void Without_emphasis_the_answer_is_left_unknown()
    {
        var result = new TestPdf()
            .Line("1. Which keyword declares a constant in C#?")
            .Line("A) var").Line("B) const").Line("C) static")
            .Parse();

        Assert.Null(Assert.Single(result.Questions).CorrectIndex);
    }

    [Fact]
    public void Two_bold_options_are_ambiguous_and_produce_a_warning()
    {
        var result = new TestPdf()
            .Line("1. Pick one")
            .Line(new Run("A) first", Bold: true))
            .Line(new Run("B) second", Bold: true))
            .Line("C) third")
            .Parse();

        var question = Assert.Single(result.Questions);
        Assert.Null(question.CorrectIndex);
        Assert.NotNull(question.Warning);
    }

    [Fact]
    public void Whole_document_in_bold_gives_no_answer()
    {
        var result = new TestPdf()
            .Line(new Run("1. Bold question", Bold: true))
            .Line(new Run("A) one", Bold: true))
            .Line(new Run("B) two", Bold: true))
            .Parse();

        Assert.Null(Assert.Single(result.Questions).CorrectIndex);
    }

    [Fact]
    public void Bold_label_only_is_detected()
    {
        var result = new TestPdf()
            .Line("1. Label emphasis")
            .Line("A) one")
            .Line(new Run("B)", Bold: true), new Run("two"))
            .Line("C) three")
            .Parse();

        Assert.Equal(1, Assert.Single(result.Questions).CorrectIndex);
    }

    [Fact]
    public void Coloured_option_is_detected()
    {
        var result = new TestPdf()
            .Line("1. Colour emphasis")
            .Line("A) one")
            .Line("B) two")
            .Line(new Run("C) three", Color: (200, 0, 0)))
            .Line("D) four")
            .Parse();

        Assert.Equal(2, Assert.Single(result.Questions).CorrectIndex);
    }

    [Fact]
    public void Multi_line_texts_and_mixed_numbering_are_merged()
    {
        var result = new TestPdf()
            .Line("1) First question that is")
            .Line("split over two lines?")
            .Line("A) answer one")
            .Line("continues here")
            .Line("B) answer two")
            .Line("2- Second question?")
            .Line("a) yes").Line("b) no")
            .Parse();

        Assert.Equal(2, result.Questions.Count);
        Assert.Equal("First question that is split over two lines?", result.Questions[0].Text);
        Assert.Equal("answer one continues here", result.Questions[0].Options[0].Text);
        Assert.Equal(["yes", "no"], result.Questions[1].Options.Select(o => o.Text));
    }

    [Fact]
    public void Inline_options_on_one_line_are_split()
    {
        var result = new TestPdf()
            .Line("1. Inline options")
            .Line("A) red   B) green")
            .Line("C) blue   D) black")
            .Parse();

        Assert.Equal(["red", "green", "blue", "black"], Assert.Single(result.Questions).Options.Select(o => o.Text));
    }

    [Fact]
    public void Duplicate_question_in_the_same_document_is_skipped()
    {
        var result = new TestPdf()
            .Line("1. Same question").Line("A) x").Line("B) y")
            .Line("2. Same question").Line("A) y").Line("B) x")
            .Parse();

        Assert.Single(result.Questions);
        Assert.Equal(1, result.DuplicatesSkipped);
    }

    [Fact]
    public void Repeated_header_and_page_numbers_are_removed()
    {
        var pdf = new TestPdf();
        for (var page = 1; page <= 3; page++)
        {
            if (page > 1)
            {
                pdf.NewPage();
            }

            pdf.Line("Informatika test bazasi 2026")
                .Line($"{page}. Question {page}?")
                .Line("A) yes").Line("B) no")
                .Line($"{page}");
        }

        var result = pdf.Parse();

        Assert.Equal(3, result.Questions.Count);
        Assert.All(result.Questions, q => Assert.DoesNotContain("Informatika", q.Text));
        Assert.All(result.Questions, q => Assert.Equal("no", q.Options[1].Text));
    }

    [Fact]
    public void Question_with_a_single_option_is_kept_with_a_warning()
    {
        var result = new TestPdf()
            .Line("1. Broken question").Line("A) only one")
            .Line("2. Fine question").Line("A) x").Line("B) y")
            .Parse();

        Assert.Equal(2, result.Questions.Count);
        Assert.Contains("1 variant", result.Questions[0].Warning);
        Assert.Contains(result.Warnings, w => w.StartsWith("Sual 1:"));
    }

    [Fact]
    public void Azerbaijani_text_is_read_and_bold_answer_detected()
    {
        if (!TestPdf.UnicodeFontsAvailable)
        {
            return; // Needs Arial from the Windows fonts folder.
        }

        var result = new TestPdf().WithUnicodeFonts()
            .Line("Sual 1: Azərbaycanın paytaxtı hansı şəhərdir?")
            .Line("A) Gəncə")
            .Line(new Run("B) Bakı", Bold: true))
            .Line("C) Şəki")
            .Line("D) İsmayıllı")
            .Parse();

        var question = Assert.Single(result.Questions);
        Assert.Equal("Azərbaycanın paytaxtı hansı şəhərdir?", question.Text);
        Assert.Equal(["Gəncə", "Bakı", "Şəki", "İsmayıllı"], question.Options.Select(o => o.Text));
        Assert.Equal(1, question.CorrectIndex);
    }

    [Fact]
    public void Document_without_text_is_reported_as_scanned()
    {
        var result = new TestPdf().Line("x").Parse();

        Assert.Equal(ParseFailure.Scanned, result.Failure);
    }

    [Fact]
    public void Text_without_questions_is_reported()
    {
        var result = new TestPdf().Line("This document has plenty of text but contains no numbered questions at all.").Parse();

        Assert.Equal(ParseFailure.NoQuestions, result.Failure);
    }

    [Fact]
    public void Garbage_bytes_are_unreadable()
    {
        var bytes = "%PDF-1.7 this is not really a pdf"u8.ToArray();

        Assert.Equal(ParseFailure.Unreadable, new PdfQuestionParser().Parse(new MemoryStream(bytes)).Failure);
        Assert.False(PdfQuestionParser.Inspect(new MemoryStream(bytes)).IsReadable);
    }

    [Fact]
    public void Inspect_reports_page_count()
    {
        var bytes = new TestPdf().Line("a").NewPage().Line("b").Build();

        Assert.Equal(new PdfInspection(true, false, 2), PdfQuestionParser.Inspect(new MemoryStream(bytes)));
    }
}

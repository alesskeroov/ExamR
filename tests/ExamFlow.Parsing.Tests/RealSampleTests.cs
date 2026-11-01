namespace ExamFlow.Parsing.Tests;

/// <summary>
/// Regression tests on real exam PDFs from <c>samples/</c>.
/// unec-sample.pdf: table layout, "•" bullets, "√" marks the correct option, numbers and
/// markers vertically centred in multi-line cells. 669 questions, each with exactly one "√".
/// </summary>
public class RealSampleTests
{
    private static readonly Lazy<ParseResult?> Unec = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "unec-sample.pdf");
        return File.Exists(path) ? new PdfQuestionParser().Parse(File.OpenRead(path)) : null;
    });

    [Fact]
    public void Unec_all_questions_parsed_with_their_check_marked_answer()
    {
        if (Unec.Value is not { } result)
        {
            return; // Sample not present.
        }

        Assert.True(result.IsSuccess);
        Assert.Equal(669, result.Questions.Count);
        Assert.Equal(Enumerable.Range(1, 669), result.Questions.Select(q => q.Number));
        Assert.All(result.Questions, q => Assert.NotNull(q.CorrectIndex));
        Assert.All(result.Questions, q => Assert.InRange(q.Options.Count, 4, 5));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Unec_page_title_is_not_part_of_the_first_question()
    {
        if (Unec.Value is not { } result)
        {
            return;
        }

        Assert.Equal("Hansı alternativ cavabda cinayət hüququnun tənzim predmeti aydın ifadə edilmişdir:", result.Questions[0].Text);
        Assert.Equal(3, result.Questions[0].CorrectIndex);
    }

    [Fact]
    public void Unec_two_line_option_with_centred_check_mark_is_joined_in_order()
    {
        if (Unec.Value is not { } result)
        {
            return;
        }

        var question = result.Questions.Single(q => q.Number == 4);
        Assert.Equal(5, question.Options.Count);
        Assert.Equal(2, question.CorrectIndex);
        Assert.StartsWith("Dünyadakı bütün uşaqların", question.Options[2].Text);
        Assert.EndsWith("universal şəkildə təsbitini", question.Options[2].Text);
        Assert.Equal("Uşaqların yalnız məktəb təhsili almaq hüququnu", question.Options[3].Text);
    }

    [Fact]
    public void Unec_question_text_around_centred_number_is_joined_in_order()
    {
        if (Unec.Value is not { } result)
        {
            return;
        }

        Assert.Equal(
            "Miqrasiya orqanlarında xüsusi rütbələr nəzərdə tutulan vəzifələrdə çalışan miqrasiya işçilərinin yaşa görə əmək pensiyası hüququ hansı şərtlər olduqda yaranır",
            result.Questions.Single(q => q.Number == 39).Text);
        Assert.StartsWith("Təbiət obyеktlərinin mühafizəsi", result.Questions.Single(q => q.Number == 130).Text);
    }
}

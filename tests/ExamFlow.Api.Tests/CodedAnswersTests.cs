using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Tests;

public class CodedAnswersTests
{
    [Theory]
    [InlineData("2,5", "2.5")]
    [InlineData("2,5", "5/2")]
    [InlineData("2,5", " +2,50 ")]
    [InlineData("-3", "−3")]
    [InlineData("-0,25", "-1/4")]
    [InlineData("12", "12,0")]
    [InlineData("0,5", ",5")]
    [InlineData("4; 2,5", "5/2")]
    public void Equal_numbers_match_whatever_their_spelling(string key, string answer) =>
        Assert.True(CodedAnswers.Matches(key, answer));

    [Theory]
    [InlineData("2,5", "2,6")]
    [InlineData("1/3", "0,333")]
    [InlineData("3", "-3")]
    [InlineData("3", "")]
    [InlineData("3", "üç")]
    [InlineData("3", "3/0")]
    public void Different_or_invalid_answers_do_not_match(string key, string answer) =>
        Assert.False(CodedAnswers.Matches(key, answer));

    [Fact]
    public void Key_is_normalised_and_may_hold_numbers_pairs_units_and_words()
    {
        Assert.Equal(("2,5; 5/2", null), CodedAnswers.NormalizeKey(" 2,5 ;5 / 2 ;"));
        Assert.Equal(("(3; -4); (2; 2)", null), CodedAnswers.NormalizeKey("(3;−4); (2 ; 2)"));
        Assert.Equal(("80°; 40 sm²", null), CodedAnswers.NormalizeKey("80 °; 40sm²"));
        Assert.Equal(("feli sifət", null), CodedAnswers.NormalizeKey("  feli   sifət "));
        Assert.NotNull(CodedAnswers.NormalizeKey(" ; ").Error);
        Assert.NotNull(CodedAnswers.NormalizeKey(" . ").Error);
    }

    [Theory]
    [InlineData("(3; -4)", "(3;−4)")]
    [InlineData("(3; -4)", "3; -4")]
    [InlineData("(2; 2)", "(2,2)")]
    [InlineData("(2,5; 1)", "(5/2; 1)")]
    [InlineData("(1; 2); (2; 1)", "(2;1)")]
    [InlineData("80°", "80")]
    [InlineData("80°", "80 °")]
    [InlineData("40 sm²", "40")]
    [InlineData("40 sm²", "40sm²")]
    [InlineData("İsim", "isim")]
    [InlineData("feli sifət", "Feli  sifət.")]
    [InlineData("1, 3, 5", "1,3,5")]
    [InlineData("mübtəda; xəbər", "Xəbər")]
    public void Pairs_units_and_words_match(string key, string answer) =>
        Assert.True(CodedAnswers.Matches(key, answer));

    [Theory]
    [InlineData("(3; -4)", "(-4; 3)")]
    [InlineData("(3; -4)", "3")]
    [InlineData("(3; -4)", "(3; -4; 1)")]
    [InlineData("80°", "81")]
    [InlineData("isim", "sifət")]
    [InlineData("8", "səkkiz")]
    [InlineData("ısım", "isim")]
    public void Different_pairs_numbers_and_words_do_not_match(string key, string answer) =>
        Assert.False(CodedAnswers.Matches(key, answer));

    [Fact]
    public void Any_non_blank_answer_can_be_saved()
    {
        Assert.True(CodedAnswers.IsValidAnswer("mübtəda"));
        Assert.True(CodedAnswers.IsValidAnswer("(3; -4)"));
        Assert.False(CodedAnswers.IsValidAnswer("  "));
        Assert.Equal("feli sifət", CodedAnswers.CleanAnswer("  feli   sifət "));
    }

    [Fact]
    public void Coded_exam_questions_are_graded_by_the_typed_number()
    {
        ExamQuestion Coded(string? key, string? answer) =>
            new() { Snapshot = new QuestionSnapshot { IsCoded = true }, CorrectAnswer = key, AnswerText = answer };

        Assert.Equal(ExamOutcome.Correct, ExamEngine.OutcomeOf(Coded("2,5", "5/2")));
        Assert.Equal(ExamOutcome.Incorrect, ExamEngine.OutcomeOf(Coded("2,5", "3")));
        Assert.Equal(ExamOutcome.Unanswered, ExamEngine.OutcomeOf(Coded("2,5", null)));
        Assert.Equal(ExamOutcome.Ungraded, ExamEngine.OutcomeOf(Coded(null, "3")));
    }
}

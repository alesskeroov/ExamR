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
    public void Key_is_normalised_and_every_part_must_be_a_number()
    {
        Assert.Equal(("2,5; 5/2", null), CodedAnswers.NormalizeKey(" 2,5 ;5 / 2 ;"));
        Assert.NotNull(CodedAnswers.NormalizeKey("2,5; x").Error);
        Assert.NotNull(CodedAnswers.NormalizeKey(" ; ").Error);
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

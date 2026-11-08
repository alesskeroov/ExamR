using System.Text.Json;
using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class QuizLogicTests
{
    private static readonly IndexedQuestion[] Questions = Enumerable.Range(1, 6)
        .Select(n => new IndexedQuestion(n, $"Sual {n} nədir?", $"Cavab {n} sənəddə belə izah olunur.", 0, n)).ToArray();

    private static QuizAttempt Attempt(int number, QuizStatus status, int minutesAgo) =>
        new(QuizLogic.Key(Questions[number - 1].Text), status, DateTimeOffset.UnixEpoch.AddDays(1).AddMinutes(-minutesAgo));

    [Fact]
    public void A_session_never_repeats_a_question_and_ends_when_all_were_asked()
    {
        var asked = new List<int>();
        for (var i = 0; i < Questions.Length; i++)
        {
            var next = QuizLogic.Pick(Questions, null, asked, [], new Random(i));
            Assert.NotNull(next);
            Assert.DoesNotContain(next!.Number, asked);
            asked.Add(next.Number);
        }
        Assert.Null(QuizLogic.Pick(Questions, null, asked, [], new Random(1)));
    }

    [Fact]
    public void Topic_or_review_filter_limits_the_questions()
    {
        for (var seed = 0; seed < 20; seed++)
            Assert.Contains(QuizLogic.Pick(Questions, [2, 5], null, [], new Random(seed))!.Number, new[] { 2, 5 });
        Assert.Null(QuizLogic.Pick(Questions, [99], null, [], new Random(1)));
    }

    [Fact]
    public void Recently_asked_questions_are_avoided_while_others_remain()
    {
        var recent = Enumerable.Range(1, 5).Select(n => Attempt(n, QuizStatus.Correct, n)).ToList();
        for (var seed = 0; seed < 20; seed++)
            Assert.Equal(6, QuizLogic.Pick(Questions, null, null, recent, new Random(seed))!.Number);
    }

    [Fact]
    public void Latest_grade_decides_and_unchecked_never_overrides_it()
    {
        var latest = QuizLogic.Latest([Attempt(1, QuizStatus.Incorrect, 30), Attempt(1, QuizStatus.Correct, 10), Attempt(2, QuizStatus.Correct, 30),
            Attempt(2, QuizStatus.Incorrect, 5), Attempt(3, QuizStatus.Incorrect, 20), Attempt(3, QuizStatus.Unchecked, 1), Attempt(4, QuizStatus.Unchecked, 1)]);
        Assert.Equal(QuizStatus.Correct, latest[QuizLogic.Key(Questions[0].Text)].Status);
        Assert.Equal(QuizStatus.Incorrect, latest[QuizLogic.Key(Questions[1].Text)].Status);
        Assert.Equal(QuizStatus.Incorrect, latest[QuizLogic.Key(Questions[2].Text)].Status);
        Assert.False(latest.ContainsKey(QuizLogic.Key(Questions[3].Text)));
    }

    [Fact]
    public void Old_partial_answers_count_as_wrong()
    {
        Assert.True(QuizLogic.IsWrong(QuizStatus.Partial));
        Assert.False(QuizLogic.IsWrong(QuizStatus.Unchecked));
    }

    [Fact]
    public void Weak_topics_are_the_ones_with_wrong_latest_answers_worst_first()
    {
        var latest = QuizLogic.Latest([Attempt(1, QuizStatus.Incorrect, 1), Attempt(2, QuizStatus.Incorrect, 1), Attempt(3, QuizStatus.Correct, 1),
            Attempt(4, QuizStatus.Incorrect, 1), Attempt(5, QuizStatus.Correct, 1)]);
        var weak = QuizLogic.WeakTopics(latest, Questions, [("A", [1, 2, 3]), ("B", [4, 5]), ("C", [6])]);
        Assert.Equal(["A", "B"], weak.Select(t => t.Title));
        Assert.Equal(2, weak[0].Wrong);
        Assert.Equal(3, weak[0].Answered);
    }

    private static JsonElement Grade(string status, params string[] missing) =>
        JsonSerializer.SerializeToElement(new { status, feedback = "Əsas fikir düzgündür.", missingPoints = missing });

    [Fact]
    public void Grades_are_only_correct_incorrect_or_unchecked()
    {
        var correct = QuizLogic.ParseGrade(Grade("CORRECT", "kiçik detal"));
        Assert.Equal(QuizStatus.Correct, correct.Status);
        Assert.Empty(correct.Missing);
        var wrong = QuizLogic.ParseGrade(Grade("INCORRECT", "Protokolun tərifi", "Nümunə"));
        Assert.Equal(QuizStatus.Incorrect, wrong.Status);
        Assert.Equal(2, wrong.Missing.Count);
        Assert.Equal(QuizStatus.Unchecked, QuizLogic.ParseGrade(Grade("UNCHECKABLE")).Status);
        Assert.Throws<JsonException>(() => QuizLogic.ParseGrade(Grade("PARTIAL")));
    }

    [Fact]
    public void Grading_schema_has_no_partial_grade()
    {
        var statuses = StudyPrompts.GradeSchema()["properties"]!["status"]!["enum"]!.AsArray().Select(s => s!.GetValue<string>());
        Assert.Equal(["CORRECT", "INCORRECT", "UNCHECKABLE"], statuses);
        Assert.Contains("MEANING", StudyPrompts.GradeTask);
    }
}

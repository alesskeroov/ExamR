using ExamFlow.Api.Features.Exams;

namespace ExamFlow.Api.Tests;

public class ExamEngineTests
{
    private static ExamCandidate Candidate(int hash, bool gradable = true) =>
        new(Guid.NewGuid(), BitConverter.GetBytes(hash), gradable);

    [Fact]
    public void Picks_the_requested_number_without_repeats()
    {
        var candidates = Enumerable.Range(0, 100).Select(i => Candidate(i)).ToList();

        var picked = ExamEngine.PickQuestions(candidates, 30, new Random(1));

        Assert.Equal(30, picked.Count);
        Assert.Equal(30, picked.Distinct().Count());
        Assert.All(picked, id => Assert.Contains(candidates, c => c.Id == id));
    }

    [Fact]
    public void Questions_with_the_same_content_are_never_both_picked()
    {
        // 10 distinct questions, each present twice (e.g. in two PDFs).
        var candidates = Enumerable.Range(0, 20).Select(i => Candidate(i % 10)).ToList();

        var picked = ExamEngine.PickQuestions(candidates, 10, new Random(2));
        var hashes = picked.Select(id => BitConverter.ToInt32(candidates.Single(c => c.Id == id).ContentHash));

        Assert.Equal(10, hashes.Distinct().Count());
        Assert.Equal(10, ExamEngine.CountDistinct(candidates));
    }

    [Fact]
    public void Each_exam_gets_a_different_selection_and_order()
    {
        var candidates = Enumerable.Range(0, 200).Select(i => Candidate(i)).ToList();

        var first = ExamEngine.PickQuestions(candidates, 30, Random.Shared);
        var second = ExamEngine.PickQuestions(candidates, 30, Random.Shared);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Shuffle_is_uniform()
    {
        // Each of 4 items should land in each position ~25% of the time.
        var counts = new int[4, 4];
        var random = new Random(3);
        for (var run = 0; run < 40_000; run++)
        {
            var items = new[] { 0, 1, 2, 3 };
            ExamEngine.Shuffle(items, random);
            for (var position = 0; position < 4; position++)
            {
                counts[items[position], position]++;
            }
        }

        foreach (var count in counts)
        {
            Assert.InRange(count, 9_500, 10_500);
        }
    }

    [Fact]
    public void Grading_counts_outcomes_and_leaves_unknown_answers_out_of_the_score()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var exam = new Exam
        {
            StartedAt = DateTimeOffset.UnixEpoch,
            ExpiresAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
            Questions =
            [
                new() { CorrectOptionId = a, SelectedOptionId = a },     // correct
                new() { CorrectOptionId = a, SelectedOptionId = a },     // correct
                new() { CorrectOptionId = a, SelectedOptionId = b },     // incorrect
                new() { CorrectOptionId = a, SelectedOptionId = null },  // unanswered
                new() { CorrectOptionId = null, SelectedOptionId = a },  // ungraded (answer unknown)
            ],
        };

        ExamEngine.Grade(exam, DateTimeOffset.UnixEpoch.AddMinutes(2), CompletionReason.Submitted);

        Assert.Equal(ExamStatus.Completed, exam.Status);
        Assert.Equal((short)2, exam.CorrectCount);
        Assert.Equal((short)1, exam.IncorrectCount);
        Assert.Equal((short)1, exam.UnansweredCount);
        Assert.Equal((short)1, exam.UngradedCount);
        Assert.Equal(50m, exam.ScorePercent); // 2 of 4 gradable
        Assert.Equal(
            [ExamOutcome.Correct, ExamOutcome.Correct, ExamOutcome.Incorrect, ExamOutcome.Unanswered, ExamOutcome.Ungraded],
            exam.Questions.Select(q => q.Outcome!.Value));
    }

    [Fact]
    public void Immediate_mode_reveals_the_answer_only_after_the_user_asked_for_it()
    {
        var exam = new Exam { RevealMode = RevealMode.Immediate };
        var picked = new ExamQuestion { CorrectOptionId = Guid.NewGuid(), SelectedOptionId = Guid.NewGuid() };
        var revealedUnanswered = new ExamQuestion { CorrectOptionId = Guid.NewGuid(), LockedAt = DateTimeOffset.UnixEpoch };

        Assert.False(ExamEngine.MayReveal(exam, picked));
        Assert.True(ExamEngine.MayReveal(exam, revealedUnanswered));
    }

    [Fact]
    public void At_end_mode_never_reveals_during_the_exam()
    {
        var revealed = new ExamQuestion { CorrectOptionId = Guid.NewGuid(), SelectedOptionId = Guid.NewGuid(), LockedAt = DateTimeOffset.UnixEpoch };

        Assert.False(ExamEngine.MayReveal(new Exam { RevealMode = RevealMode.AtEnd }, revealed));
    }

    [Fact]
    public void Chosen_time_limit_overrides_the_default()
    {
        var options = new ExamOptions();

        Assert.Equal(TimeSpan.FromMinutes(30), ExamEngine.Duration(null, options));
        Assert.Equal(TimeSpan.FromMinutes(45), ExamEngine.Duration(45, options));
    }

    [Fact]
    public void Time_limit_of_an_exam_is_its_start_to_deadline_span()
    {
        var exam = new Exam { StartedAt = DateTimeOffset.UnixEpoch, ExpiresAt = DateTimeOffset.UnixEpoch.AddMinutes(35) };

        Assert.Equal(35, ExamEngine.TimeLimitMinutes(exam));
    }

    [Fact]
    public void Score_is_null_when_no_question_can_be_graded()
    {
        var exam = new Exam { ExpiresAt = DateTimeOffset.UnixEpoch, Questions = [new() { CorrectOptionId = null }] };

        ExamEngine.Grade(exam, DateTimeOffset.UnixEpoch, CompletionReason.Submitted);

        Assert.Null(exam.ScorePercent);
    }

    [Fact]
    public void Completion_time_is_capped_at_the_deadline()
    {
        var exam = new Exam { ExpiresAt = DateTimeOffset.UnixEpoch.AddMinutes(30), Questions = [] };

        ExamEngine.Grade(exam, DateTimeOffset.UnixEpoch.AddHours(5), CompletionReason.TimeExpired);

        Assert.Equal(exam.ExpiresAt, exam.CompletedAt);
        Assert.Equal(CompletionReason.TimeExpired, exam.CompletionReason);
    }

    [Fact]
    public void Default_time_limit_is_30_minutes_whatever_the_question_count()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), new ExamOptions().DefaultDuration);
    }
}

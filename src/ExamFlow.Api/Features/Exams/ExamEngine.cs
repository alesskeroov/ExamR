namespace ExamFlow.Api.Features.Exams;

public sealed record ExamCandidate(Guid Id, byte[] ContentHash, bool IsGradable);

/// <summary>Pure exam logic (selection, shuffling, grading); no database access, so it is unit-testable.</summary>
public static class ExamEngine
{
    /// <summary>
    /// Picks <paramref name="count"/> random questions without repeats. Candidates are shuffled first and then
    /// de-duplicated by content hash, so which copy of a duplicated question is used is random as well.
    /// </summary>
    public static List<Guid> PickQuestions(IReadOnlyList<ExamCandidate> candidates, int count, Random random)
    {
        var shuffled = candidates.ToArray();
        Shuffle(shuffled, random);

        var seen = new HashSet<string>();
        var picked = new List<Guid>(count);
        foreach (var candidate in shuffled)
        {
            if (picked.Count == count)
            {
                break;
            }

            if (seen.Add(Convert.ToHexString(candidate.ContentHash)))
            {
                picked.Add(candidate.Id);
            }
        }

        return picked;
    }

    public static int CountDistinct(IEnumerable<ExamCandidate> candidates) =>
        candidates.Select(c => Convert.ToHexString(c.ContentHash)).Distinct().Count();

    /// <summary>Fisher–Yates: every permutation is equally likely.</summary>
    public static void Shuffle<T>(IList<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static ExamOutcome OutcomeOf(ExamQuestion question) => question switch
    {
        { CorrectOptionId: null } => ExamOutcome.Ungraded,
        { SelectedOptionId: null } => ExamOutcome.Unanswered,
        _ when question.SelectedOptionId == question.CorrectOptionId => ExamOutcome.Correct,
        _ => ExamOutcome.Incorrect,
    };

    /// <summary>
    /// Scores the exam on the server. Unanswered questions count against the score;
    /// questions whose answer is unknown (Ungraded) are left out of the percentage.
    /// </summary>
    public static void Grade(Exam exam, DateTimeOffset now, CompletionReason reason)
    {
        foreach (var question in exam.Questions)
        {
            question.Outcome = OutcomeOf(question);
        }

        short Count(ExamOutcome outcome) => (short)exam.Questions.Count(q => q.Outcome == outcome);

        exam.CorrectCount = Count(ExamOutcome.Correct);
        exam.IncorrectCount = Count(ExamOutcome.Incorrect);
        exam.UnansweredCount = Count(ExamOutcome.Unanswered);
        exam.UngradedCount = Count(ExamOutcome.Ungraded);

        var gradable = exam.Questions.Count - exam.UngradedCount.Value;
        exam.ScorePercent = gradable > 0 ? Math.Round(exam.CorrectCount.Value * 100m / gradable, 2) : null;

        exam.Status = ExamStatus.Completed;
        exam.CompletionReason = reason;
        exam.CompletedAt = now < exam.ExpiresAt ? now : exam.ExpiresAt;
    }
}

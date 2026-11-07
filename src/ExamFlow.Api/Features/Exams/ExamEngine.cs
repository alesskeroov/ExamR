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

    /// <summary>Time limit of a new exam: the user's choice, or the default per-question allowance.</summary>
    public static TimeSpan Duration(int questionCount, int? durationMinutes, ExamOptions options) =>
        durationMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : options.DurationFor(questionCount);

    /// <summary>The exam's time limit in whole minutes (what a retake starts from).</summary>
    public static int TimeLimitMinutes(Exam exam) => (int)Math.Round((exam.ExpiresAt - exam.StartedAt).TotalMinutes);

    /// <summary>
    /// Whether the correct answer may be sent to the client while the exam runs. Immediate mode only: normally after
    /// answering; in LockAfterCorrect exams only once the question is locked, so a wrong pick does not leak the answer.
    /// </summary>
    public static bool MayReveal(Exam exam, ExamQuestion question) =>
        exam.RevealMode == RevealMode.Immediate
        && (exam.LockAfterCorrect ? question.LockedAt is not null : question.SelectedOptionId is not null);

    /// <summary>A LockAfterCorrect exam locks a question as soon as the stored answer is the known correct one.</summary>
    public static bool LocksOnAnswer(Exam exam, ExamQuestion question) =>
        exam.LockAfterCorrect && exam.RevealMode == RevealMode.Immediate
        && question.CorrectOptionId is not null && question.SelectedOptionId == question.CorrectOptionId;

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

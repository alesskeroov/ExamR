using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Features.Exams;

public sealed record ExamCandidate(Guid Id, byte[] ContentHash, bool IsGradable);

/// <summary>How a question was graded in one of the user's finished exams.</summary>
public sealed record PastAnswer(byte[] ContentHash, DateTimeOffset CompletedAt, ExamOutcome Outcome);

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

    /// <summary>
    /// Mistakes mode: the candidates whose most recent graded answer was wrong or left blank. Once the user answers
    /// a question correctly it drops out; questions never seen or never graded are not included.
    /// </summary>
    public static List<ExamCandidate> Mistakes(IReadOnlyList<ExamCandidate> candidates, IEnumerable<PastAnswer> history)
    {
        var latest = history
            .Where(a => a.Outcome != ExamOutcome.Ungraded)
            .GroupBy(a => Convert.ToHexString(a.ContentHash))
            .ToDictionary(g => g.Key, g => g.MaxBy(a => a.CompletedAt)!.Outcome);

        return candidates
            .Where(c => latest.TryGetValue(Convert.ToHexString(c.ContentHash), out var outcome)
                && outcome is ExamOutcome.Incorrect or ExamOutcome.Unanswered)
            .ToList();
    }

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
        { Snapshot.IsCoded: true, CorrectAnswer: null } => ExamOutcome.Ungraded,
        { Snapshot.IsCoded: true, AnswerText: null } => ExamOutcome.Unanswered,
        { Snapshot.IsCoded: true } => CodedAnswers.Matches(question.CorrectAnswer, question.AnswerText) ? ExamOutcome.Correct : ExamOutcome.Incorrect,
        { CorrectOptionId: null } => ExamOutcome.Ungraded,
        { SelectedOptionId: null } => ExamOutcome.Unanswered,
        _ when question.SelectedOptionId == question.CorrectOptionId => ExamOutcome.Correct,
        _ => ExamOutcome.Incorrect,
    };

    /// <summary>Time limit of a new exam: the user's choice, or the default per-question allowance.</summary>
    public static TimeSpan Duration(int? durationMinutes, ExamOptions options) =>
        durationMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : options.DefaultDuration;

    /// <summary>The exam's time limit in whole minutes (what a retake starts from).</summary>
    public static int TimeLimitMinutes(Exam exam) => (int)Math.Round((exam.ExpiresAt - exam.StartedAt).TotalMinutes);

    /// <summary>
    /// Whether the correct answer may be sent to the client while the exam runs: Immediate mode only, and only once
    /// the user asked to see it, which locks the question. Picking an option alone never reveals the answer.
    /// </summary>
    public static bool MayReveal(Exam exam, ExamQuestion question) =>
        exam.RevealMode == RevealMode.Immediate && question.LockedAt is not null;

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

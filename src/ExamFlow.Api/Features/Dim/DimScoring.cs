using System.Text.RegularExpressions;
using ExamFlow.Api.Features.Exams;

namespace ExamFlow.Api.Features.Dim;

/// <param name="IsOpen">An open coded task (typed number) rather than a closed A–E question.</param>
public sealed record DimGradedItem(string Subject, string? Topic, ExamOutcome Outcome, bool IsOpen = false);

/// <summary>
/// One subject's result. Closed* and Open* are the points the measured parts earn under the official formula (0/0 when
/// the test had no such questions); Measured* is their sum; Estimated assumes the same performance on the unmeasured
/// parts (e.g. written tasks) and is out of <see cref="FullMaxPoints"/>.
/// </summary>
public sealed record DimSubjectScore(
    string Subject,
    string Name,
    decimal Coefficient,
    int Total,
    int Correct,
    int Incorrect,
    int Unanswered,
    int Ungraded,
    decimal ClosedPoints,
    decimal ClosedMaxPoints,
    int OpenTotal,
    int OpenCorrect,
    decimal OpenPoints,
    decimal OpenMaxPoints,
    decimal MeasuredPoints,
    decimal MeasuredMaxPoints,
    decimal EstimatedPoints,
    decimal FullMaxPoints);

public sealed record DimWeakTopic(string Subject, string Topic, int Total, int Correct, decimal Accuracy);

public sealed record DimScore(
    IReadOnlyList<DimSubjectScore> Subjects,
    decimal ClosedPoints,
    decimal ClosedMaxPoints,
    decimal OpenPoints,
    decimal OpenMaxPoints,
    decimal MeasuredPoints,
    decimal MeasuredMaxPoints,
    decimal EstimatedPoints,
    decimal FullMaxPoints,
    IReadOnlyList<DimWeakTopic> WeakTopics);

/// <summary>
/// Pure DİM scoring for closed (A–E) and open coded questions. Written tasks are graded by examiners and are not part
/// of the tests, so a real 300/400 point result cannot be computed: the measured parts are scored under the official
/// formula, and the full score is estimated by assuming the same performance on what was not measured.
/// </summary>
public static partial class DimScoring
{
    public const decimal WeakTopicThreshold = 0.6m;

    public static DimScore Score(DimTrack track, string? groupCode, IReadOnlyList<DimGradedItem> items)
    {
        var subjects = items.GroupBy(i => i.Subject)
            .Select(g => SubjectScore(track, groupCode, g.Key, g.ToList()))
            .OrderByDescending(s => s.Coefficient)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        var weak = items.Where(i => !string.IsNullOrWhiteSpace(i.Topic) && i.Outcome != ExamOutcome.Ungraded)
            .GroupBy(i => (i.Subject, Topic: i.Topic!.Trim()))
            .Select(g => (g.Key, Total: g.Count(), Correct: g.Count(i => i.Outcome == ExamOutcome.Correct)))
            .Where(t => t.Correct < t.Total && (decimal)t.Correct / t.Total < WeakTopicThreshold)
            .Select(t => new DimWeakTopic(t.Key.Subject, t.Key.Topic, t.Total, t.Correct, Math.Round((decimal)t.Correct / t.Total * 100, 1)))
            .OrderBy(t => t.Accuracy)
            .ThenByDescending(t => t.Total)
            .ToList();

        return new DimScore(subjects,
            subjects.Sum(s => s.ClosedPoints), subjects.Sum(s => s.ClosedMaxPoints),
            subjects.Sum(s => s.OpenPoints), subjects.Sum(s => s.OpenMaxPoints),
            subjects.Sum(s => s.MeasuredPoints), subjects.Sum(s => s.MeasuredMaxPoints),
            subjects.Sum(s => s.EstimatedPoints), subjects.Sum(s => s.FullMaxPoints), weak);
    }

    /// <summary>
    /// Official tasks an open question of the test stands for: the coded tasks, or for a subject without them
    /// (Azerbaijani, foreign language) its written tasks, which count twice.
    /// </summary>
    public static int OpenUnits(DimSubjectSpec spec) => spec.Coded > 0 ? spec.Coded : 2 * spec.Written;

    private static DimSubjectScore SubjectScore(DimTrack track, string? groupCode, string subject, IReadOnlyList<DimGradedItem> items)
    {
        var spec = DimCatalog.Spec(track, subject);
        var coefficient = DimCatalog.Coefficient(track, groupCode, subject);
        var perPoint = (decimal)DimCatalog.SubjectMaxPoints / spec.Denominator * coefficient;
        int Count(IEnumerable<DimGradedItem> part, ExamOutcome outcome) => part.Count(i => i.Outcome == outcome);

        // Each part is scaled to its official task count, so a test may hold more or fewer questions than the real exam.
        (decimal Points, decimal Max) Part(IReadOnlyList<DimGradedItem> part, int units, bool penalty)
        {
            var gradable = part.Count - Count(part, ExamOutcome.Ungraded);
            if (gradable == 0 || units == 0) return (0m, 0m);
            // Net answers (Blok closed: a wrong answer cancels a quarter of a correct one), never below zero.
            var net = Math.Max(0m, Count(part, ExamOutcome.Correct) - (penalty ? Count(part, ExamOutcome.Incorrect) / 4m : 0m));
            return (net / gradable * units * perPoint, units * perPoint);
        }

        var closedItems = items.Where(i => !i.IsOpen).ToList();
        var openItems = items.Where(i => i.IsOpen).ToList();
        var closed = Part(closedItems, spec.Closed, spec.WrongPenalty);
        var open = Part(openItems, OpenUnits(spec), penalty: false);
        var measured = closed.Points + open.Points;
        var measuredMax = closed.Max + open.Max;
        var fullMax = DimCatalog.SubjectMaxPoints * coefficient;

        return new DimSubjectScore(subject, spec.Name, coefficient, items.Count, Count(items, ExamOutcome.Correct),
            Count(items, ExamOutcome.Incorrect), Count(items, ExamOutcome.Unanswered), Count(items, ExamOutcome.Ungraded),
            ClosedPoints: Round(closed.Points), ClosedMaxPoints: Round(closed.Max),
            OpenTotal: openItems.Count, OpenCorrect: Count(openItems, ExamOutcome.Correct),
            OpenPoints: Round(open.Points), OpenMaxPoints: Round(open.Max),
            MeasuredPoints: Round(measured), MeasuredMaxPoints: Round(measuredMax),
            EstimatedPoints: measuredMax > 0 ? Round(measured / measuredMax * fullMax) : 0m,
            FullMaxPoints: fullMax);
    }

    /// <summary>DİM rounds subject points to 0.1.</summary>
    private static decimal Round(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Parses an answer key such as "1-A 2) C, 3.b" or "1A 2C" into question number → option index (A = 0 … E = 4).
    /// Returns the numbers that appear more than once with different letters as conflicts.
    /// </summary>
    public static (Dictionary<int, int> Key, List<int> Conflicts) ParseAnswerKey(string text)
    {
        var key = new Dictionary<int, int>();
        var conflicts = new List<int>();
        foreach (Match match in AnswerKeyPair().Matches(text))
        {
            var number = int.Parse(match.Groups["n"].Value);
            var option = char.ToUpperInvariant(match.Groups["o"].Value[0]) - 'A';
            if (key.TryGetValue(number, out var existing) && existing != option)
            {
                conflicts.Add(number);
            }

            key[number] = option;
        }

        return (key, conflicts.Distinct().ToList());
    }

    [GeneratedRegex(@"(?<!\d)(?<n>\d{1,4})\s*[-–.:)=]?\s*(?<o>[A-Ea-e])(?![A-Za-z])")]
    private static partial Regex AnswerKeyPair();
}

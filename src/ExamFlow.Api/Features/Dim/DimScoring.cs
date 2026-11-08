using System.Text.RegularExpressions;
using ExamFlow.Api.Features.Exams;

namespace ExamFlow.Api.Features.Dim;

public sealed record DimGradedItem(string Subject, string? Topic, ExamOutcome Outcome);

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
    decimal EstimatedPoints,
    decimal FullMaxPoints);

public sealed record DimWeakTopic(string Subject, string Topic, int Total, int Correct, decimal Accuracy);

public sealed record DimScore(
    IReadOnlyList<DimSubjectScore> Subjects,
    decimal ClosedPoints,
    decimal ClosedMaxPoints,
    decimal EstimatedPoints,
    decimal FullMaxPoints,
    IReadOnlyList<DimWeakTopic> WeakTopics);

/// <summary>
/// Pure DİM scoring for closed (A–E) questions only. A real 300/400 point result also needs open coded and written
/// tasks, so two numbers are reported: the points the closed part earns under the official formula (out of what the
/// closed part can earn), and an estimate that assumes the same performance on the open part.
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
            subjects.Sum(s => s.EstimatedPoints), subjects.Sum(s => s.FullMaxPoints), weak);
    }

    private static DimSubjectScore SubjectScore(DimTrack track, string? groupCode, string subject, IReadOnlyList<DimGradedItem> items)
    {
        var spec = DimCatalog.Spec(track, subject);
        var coefficient = DimCatalog.Coefficient(track, groupCode, subject);
        int Count(ExamOutcome outcome) => items.Count(i => i.Outcome == outcome);
        var correct = Count(ExamOutcome.Correct);
        var incorrect = Count(ExamOutcome.Incorrect);
        var gradable = items.Count - Count(ExamOutcome.Ungraded);

        // Net closed answers (Blok: a wrong answer cancels a quarter of a correct one), never below zero.
        var net = Math.Max(0m, correct - (spec.WrongPenalty ? incorrect / 4m : 0m));
        // The test may hold more or fewer closed questions than the real exam, so performance is scaled to the official count.
        var ratio = gradable > 0 ? net / gradable : 0m;
        var perPoint = (decimal)DimCatalog.SubjectMaxPoints / spec.Denominator * coefficient;

        return new DimSubjectScore(subject, spec.Name, coefficient, items.Count, correct, incorrect, Count(ExamOutcome.Unanswered),
            Count(ExamOutcome.Ungraded),
            ClosedPoints: Round(ratio * spec.Closed * perPoint),
            ClosedMaxPoints: Round(spec.Closed * perPoint),
            EstimatedPoints: Round(ratio * DimCatalog.SubjectMaxPoints * coefficient),
            FullMaxPoints: DimCatalog.SubjectMaxPoints * coefficient);
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

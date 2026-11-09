using System.Text.Json;

namespace ExamFlow.Api.Features.Learning;

/// <summary>One graded (or not gradable) answer of the student, newest decides.</summary>
public sealed record QuizAttempt(string Question, QuizStatus Status, DateTimeOffset At);

public sealed record WeakTopic(string Title, IReadOnlyList<int> Numbers, int Wrong, int Answered);

/// <summary>
/// "Məni yoxla" rules without database or AI, so they can be unit-tested. Questions are identified by their stored text
/// (<see cref="Key"/>), because quiz items keep the question text, not its number.
/// </summary>
public static class QuizLogic
{
    /// <summary>Questions of one session when the student does not stop earlier.</summary>
    public const int SessionSize = 10;
    /// <summary>Most question numbers a filter (topic, review list) or the asked list may carry.</summary>
    public const int MaxNumbers = 60;
    private const int AvoidRecent = 8;

    public static string Key(string questionText) => questionText.Length <= 1000 ? questionText : questionText[..999].TrimEnd() + "…";

    /// <summary>"Partial" exists only in older answers; it counts as wrong (the question goes to review).</summary>
    public static bool IsWrong(QuizStatus status) => status is QuizStatus.Incorrect or QuizStatus.Partial;
    public static bool IsGraded(QuizStatus status) => status is QuizStatus.Correct or QuizStatus.Incorrect or QuizStatus.Partial;

    /// <summary>
    /// Next question: only from <paramref name="numbers"/> when given (topic or review list), never one already
    /// <paramref name="asked"/> in this session, preferably not one of the last few asked, and sometimes a weak one.
    /// Null when nothing is left.
    /// </summary>
    public static IndexedQuestion? Pick(IReadOnlyList<IndexedQuestion> questions, IReadOnlyCollection<int>? numbers, IReadOnlyCollection<int>? asked,
        IReadOnlyList<QuizAttempt> recentNewestFirst, Random random)
    {
        var pool = questions.Where(q => (numbers is not { Count: > 0 } || numbers.Contains(q.Number)) && (asked is null || !asked.Contains(q.Number))).ToList();
        if (pool.Count == 0) return null;
        var recent = recentNewestFirst.Take(AvoidRecent).Select(a => a.Question).ToHashSet();
        var fresh = pool.Where(q => !recent.Contains(Key(q.Text))).ToList();
        if (fresh.Count == 0) fresh = pool;
        var latest = Latest(recentNewestFirst);
        var weak = fresh.Where(q => latest.TryGetValue(Key(q.Text), out var a) && IsWrong(a.Status)).ToList();
        return weak.Count > 0 && random.NextDouble() < 0.35 ? weak[random.Next(weak.Count)] : fresh[random.Next(fresh.Count)];
    }

    /// <summary>Latest graded attempt per question ("Yoxlanılmadı" never overrides a real grade).</summary>
    public static Dictionary<string, QuizAttempt> Latest(IEnumerable<QuizAttempt> attempts) =>
        attempts.Where(a => IsGraded(a.Status)).GroupBy(a => a.Question).ToDictionary(g => g.Key, g => g.MaxBy(a => a.At)!);

    /// <summary>Topics whose questions the student most recently got wrong, worst first.</summary>
    public static IReadOnlyList<WeakTopic> WeakTopics(IReadOnlyDictionary<string, QuizAttempt> latest, IReadOnlyList<IndexedQuestion> questions,
        IEnumerable<(string Title, IReadOnlyList<int> Numbers)> groups, int take = 5)
    {
        var byNumber = questions.ToDictionary(q => q.Number);
        return groups.Select(g =>
            {
                var answered = g.Numbers.Where(byNumber.ContainsKey).Select(n => latest.GetValueOrDefault(Key(byNumber[n].Text))).OfType<QuizAttempt>().ToList();
                return new WeakTopic(g.Title, g.Numbers, answered.Count(a => IsWrong(a.Status)), answered.Count);
            })
            .Where(t => t.Wrong > 0)
            .OrderByDescending(t => t.Wrong).ThenByDescending(t => t.Wrong / (double)t.Answered)
            .Take(take).ToList();
    }

    /// <summary>
    /// Model grade → status. Only CORRECT, INCORRECT and UNCHECKABLE exist; anything else is invalid output.
    /// A correct answer never carries missing points; an unchecked one has no verdict to explain.
    /// </summary>
    public static (QuizStatus Status, string Feedback, IReadOnlyList<string> Missing) ParseGrade(JsonElement json)
    {
        var status = json.GetProperty("status").GetString() switch
        {
            "CORRECT" => QuizStatus.Correct,
            "INCORRECT" => QuizStatus.Incorrect,
            "UNCHECKABLE" => QuizStatus.Unchecked,
            _ => throw new JsonException("status"),
        };
        var feedback = StudyJson.Text(json, "feedback", 300);
        return (status, feedback, status == QuizStatus.Incorrect ? StudyJson.Texts(json, "missingPoints", 4, 200) : []);
    }
}

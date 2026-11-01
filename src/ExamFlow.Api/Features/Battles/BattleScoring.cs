namespace ExamFlow.Api.Features.Battles;

/// <summary>Pure scoring and ranking rules (unit-tested, no I/O).</summary>
public static class BattleScoring
{
    public const int MaxPoints = 1000;

    /// <summary>
    /// Kahoot-style: a correct answer earns 1000 points when instant, falling linearly to 500 at the
    /// deadline. Wrong or missing answers earn 0.
    /// </summary>
    public static int Points(bool correct, TimeSpan elapsed, TimeSpan limit)
    {
        if (!correct || limit <= TimeSpan.Zero)
        {
            return 0;
        }

        var fraction = Math.Clamp(elapsed / limit, 0, 1);
        return (int)Math.Round(MaxPoints * (1 - fraction / 2));
    }

    /// <summary>Highest score first; ties broken by more correct answers, then faster correct answers, then name.</summary>
    public static List<BattleParticipant> Rank(IEnumerable<BattleParticipant> participants) =>
        participants
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.CorrectCount)
            .ThenBy(p => p.CorrectTimeMs)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Removes control characters and surrounding whitespace; HTML is escaped by the client when rendering.</summary>
    public static string CleanMessage(string text) =>
        new string(text.Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();
}

namespace ExamFlow.Api.Features.Courses;

/// <summary>Pure rules of the preparation panel: days left, readiness and today's three tasks.</summary>
public static class CoursePlanner
{
    public const int MaxTasks = 3;
    private const int MinDailyQuestions = 10;
    private const int MaxDailyQuestions = 50;
    private const int MinDailyCards = 5;
    private const int MaxDailyCards = 40;

    /// <summary>Whole days until the exam (0 = today); null without a date or once the exam day has passed.</summary>
    public static int? DaysLeft(DateOnly? examDate, DateOnly today) =>
        examDate is { } date && date >= today ? date.DayNumber - today.DayNumber : null;

    /// <summary>Mastered cards and correctly answered bank questions as one percentage; each counts half when both exist.</summary>
    public static int Readiness(IReadOnlyList<MaterialProgress> materials, IReadOnlyList<BankProgress> banks)
    {
        var cards = materials.Sum(m => m.Cards);
        var questions = banks.Sum(b => b.Questions);
        double? cardShare = cards > 0 ? (double)materials.Sum(m => m.Mastered) / cards : null;
        double? bankShare = questions > 0 ? (double)banks.Sum(b => b.Correct) / questions : null;
        var share = (cardShare, bankShare) switch
        {
            ({ } c, { } b) => (c + b) / 2,
            ({ } c, null) => c,
            (null, { } b) => b,
            _ => 0,
        };
        return (int)Math.Round(share * 100);
    }

    /// <summary>
    /// Today's work, most useful first: re-test wrong answers, read the lecture part they point to, study weak or new
    /// cards, then new bank questions. The amount of new work is spread over the days left (at least one day).
    /// </summary>
    public static IReadOnlyList<CourseTask> Today(int? daysLeft, IReadOnlyList<MaterialProgress> materials, IReadOnlyList<BankProgress> banks,
        IReadOnlyList<WeakSpot> weakSpots)
    {
        var days = Math.Max(1, (daysLeft ?? 7) + 1); // the exam day itself still counts
        var tasks = new List<CourseTask>();

        if (banks.Where(b => b.Wrong > 0).MaxBy(b => b.Wrong) is { } mistakes)
            tasks.Add(new("Mistakes", "Səhvlərini yenidən həll et",
                $"«{mistakes.Name}» bankında son cavabı səhv olan {mistakes.Wrong} sual", mistakes.DocumentId, null, mistakes.Wrong, null));

        if (weakSpots.FirstOrDefault() is { } spot)
            tasks.Add(new("Read", spot.Section is { Length: > 0 } section ? $"«{section}» hissəsini oxu" : "Zəif hissəni oxu",
                $"«{spot.MaterialName}» — səhv cavablarının {spot.WrongAnswers}-i buradan", spot.MaterialDocumentId, spot.ChatId, spot.WrongAnswers, spot.ChunkOrdinal));

        if (materials.Where(m => m.Weak > 0).MaxBy(m => m.Weak) is { } weak)
            tasks.Add(new("Cards", "Zəif kartları təkrarla", $"«{weak.Name}» — {weak.Weak} zəif kart", weak.DocumentId, weak.ChatId, weak.Weak, null));
        else if (materials.Where(m => m.Cards > m.Mastered).MaxBy(m => m.Cards - m.Mastered) is { } open)
        {
            var count = Math.Clamp((int)Math.Ceiling((open.Cards - open.Mastered) / (double)days), MinDailyCards, MaxDailyCards);
            count = Math.Min(count, open.Cards - open.Mastered);
            tasks.Add(new("Cards", "Kartlarla öyrən", $"«{open.Name}» — bu gün {count} kart", open.DocumentId, open.ChatId, count, null));
        }

        if (banks.Where(b => b.Questions > b.Answered).MaxBy(b => b.Questions - b.Answered) is { } fresh)
        {
            var unseen = fresh.Questions - fresh.Answered;
            var count = Math.Min(unseen, Math.Clamp((int)Math.Ceiling(unseen / (double)days), MinDailyQuestions, MaxDailyQuestions));
            tasks.Add(new("Test", "Yeni suallar həll et", $"«{fresh.Name}» — bu gün {count} sual (hələ {unseen} sual görülməyib)", fresh.DocumentId, null, count, null));
        }

        if (tasks.Count == 0)
            tasks.Add(new("Done", "Hər şey təkrarlanıb", materials.Count + banks.Count == 0
                ? "Fənnə mühazirə faylı və ya sual bankı əlavə edin."
                : "Qarışıq test və ya kart təkrarı ilə biliyini möhkəmləndir.", null, null, null, null));
        return tasks.Take(MaxTasks).ToList();
    }
}

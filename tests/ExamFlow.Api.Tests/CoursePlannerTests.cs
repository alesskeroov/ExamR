using ExamFlow.Api.Features.Courses;

namespace ExamFlow.Api.Tests;

public class CoursePlannerTests
{
    private static readonly Guid Lecture = Guid.NewGuid();
    private static readonly Guid Bank = Guid.NewGuid();
    private static MaterialProgress Material(int cards, int mastered, int weak) => new(Lecture, "Mühazirə 1.pptx", "pptx", null, true, cards, mastered, weak);
    private static BankProgress Questions(int total, int answered, int correct, int wrong) => new(Bank, "Bank.pdf", total, answered, correct, wrong);

    [Fact]
    public void Days_left_counts_today_and_ends_after_the_exam_day()
    {
        var today = new DateOnly(2026, 11, 14);
        Assert.Equal(0, CoursePlanner.DaysLeft(today, today));
        Assert.Equal(6, CoursePlanner.DaysLeft(new DateOnly(2026, 11, 20), today));
        Assert.Null(CoursePlanner.DaysLeft(new DateOnly(2026, 11, 13), today));
        Assert.Null(CoursePlanner.DaysLeft(null, today));
    }

    [Fact]
    public void Readiness_averages_cards_and_bank_when_both_exist()
    {
        Assert.Equal(55, CoursePlanner.Readiness([Material(10, 5, 0)], [Questions(100, 80, 60, 20)]));
        Assert.Equal(50, CoursePlanner.Readiness([Material(10, 5, 0)], []));
        Assert.Equal(60, CoursePlanner.Readiness([], [Questions(100, 80, 60, 20)]));
        Assert.Equal(0, CoursePlanner.Readiness([], []));
    }

    [Fact]
    public void Mistakes_and_their_lecture_part_come_first_and_at_most_three_tasks()
    {
        var spot = new WeakSpot(Lecture, "Mühazirə 1.pptx", "pptx", null, "Hidridlər", 12, 12, 4, 7);
        var tasks = CoursePlanner.Today(10, [Material(40, 10, 3)], [Questions(200, 50, 30, 20)], [spot]);

        Assert.Equal(["Mistakes", "Read", "Cards"], tasks.Select(t => t.Kind));
        Assert.Equal(20, tasks[0].Count);
        Assert.Equal(Bank, tasks[0].DocumentId);
        Assert.Equal("«Hidridlər» hissəsini oxu", tasks[1].Title);
        Assert.Equal(4, tasks[1].ChunkOrdinal);
    }

    [Fact]
    public void New_work_is_spread_over_the_days_left()
    {
        // 150 unseen questions over 5 days (+ the exam day) = 25 a day; 60 unmastered cards = 10 a day.
        var tasks = CoursePlanner.Today(5, [Material(60, 0, 0)], [Questions(150, 0, 0, 0)], []);

        Assert.Equal(["Cards", "Test"], tasks.Select(t => t.Kind));
        Assert.Equal(10, tasks[0].Count);
        Assert.Equal(25, tasks[1].Count);
    }

    [Fact]
    public void Daily_amounts_have_a_floor_but_never_exceed_what_is_left()
    {
        var tasks = CoursePlanner.Today(30, [Material(3, 0, 0)], [Questions(4, 0, 0, 0)], []);

        Assert.Equal(3, tasks[0].Count);
        Assert.Equal(4, tasks[1].Count);
    }

    [Fact]
    public void Nothing_left_gives_one_done_task()
    {
        var task = Assert.Single(CoursePlanner.Today(3, [Material(10, 10, 0)], [Questions(10, 10, 10, 0)], []));
        Assert.Equal("Done", task.Kind);
        Assert.Equal("Fənnə mühazirə faylı və ya sual bankı əlavə edin.", Assert.Single(CoursePlanner.Today(null, [], [], [])).Detail);
    }
}

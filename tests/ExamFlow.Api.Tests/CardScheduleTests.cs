using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class CardScheduleTests
{
    // 2026-11-15 10:00 in Baku (UTC+4).
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BakuMidnight = new(2026, 11, 15, 0, 0, 0, TimeSpan.FromHours(4));

    [Fact]
    public void Again_comes_back_today()
    {
        var (interval, due) = CardSchedule.Next(10, CardGrade.Again, Now);
        Assert.Equal(0, interval);
        Assert.Equal(Now, due);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 3)]
    [InlineData(3, 8)]
    [InlineData(30, 60)]
    [InlineData(60, 60)]
    public void Good_grows_the_gap_up_to_sixty_days(int interval, int expected)
    {
        var (next, due) = CardSchedule.Next(interval, CardGrade.Good, Now);
        Assert.Equal(expected, next);
        Assert.Equal(BakuMidnight.AddDays(expected), due);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 6)]
    public void Hard_stretches_the_gap_a_little(int interval, int expected) =>
        Assert.Equal(expected, CardSchedule.Next(interval, CardGrade.Hard, Now).IntervalDays);

    [Fact]
    public void The_day_starts_at_midnight_in_Baku()
    {
        // 23:30 UTC is already 03:30 the next day in Baku.
        var lateUtc = new DateTimeOffset(2026, 11, 14, 23, 30, 0, TimeSpan.Zero);
        Assert.Equal(BakuMidnight, CardSchedule.DayStart(lateUtc));
    }

    [Fact]
    public void Rows_from_before_scheduling_are_due()
    {
        Assert.True(CardSchedule.IsDue(null, Now.AddDays(-3), Now));
        Assert.False(CardSchedule.IsDue(Now.AddDays(1), Now.AddDays(-3), Now));
    }
}

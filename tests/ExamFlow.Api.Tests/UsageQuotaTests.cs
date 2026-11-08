using ExamFlow.Api.Features.Usage;

namespace ExamFlow.Api.Tests;

public class UsageQuotaTests
{
    [Theory]
    [InlineData("2026-11-30T19:59:59Z", "2026-10-31T20:00:00Z")]
    [InlineData("2026-11-30T20:00:00Z", "2026-11-30T20:00:00Z")]
    [InlineData("2024-02-29T20:00:00Z", "2024-02-29T20:00:00Z")]
    public void Month_start_uses_Baku_boundary(string now, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), UsageQuota.MonthStart(DateTimeOffset.Parse(now)));

    [Fact]
    public void Reset_crosses_year_boundary() => Assert.Equal(DateTimeOffset.Parse("2026-12-31T20:00:00Z"), UsageQuota.NextReset(DateTimeOffset.Parse("2026-12-15T00:00:00Z")));

    [Theory]
    [InlineData(UsageKind.Upload, 3)]
    [InlineData(UsageKind.Exam, 10)]
    [InlineData(UsageKind.Battle, 3)]
    [InlineData(UsageKind.AiRequest, 100)]
    public void Defaults_and_exemptions(UsageKind kind, int expected)
    {
        Assert.Equal(expected, UsageQuota.EffectiveLimit(kind, null, new(), false));
        Assert.Null(UsageQuota.EffectiveLimit(kind, null, new(), true));
        Assert.Null(UsageQuota.EffectiveLimit(kind, new() { Unlimited = true }, new(), false));
    }

    [Fact]
    public void Custom_zero_blocks_and_null_inherits()
    {
        var custom = new UserUsageLimit { MonthlyUploads = 0, MonthlyExams = 25 };
        Assert.Equal(0, UsageQuota.EffectiveLimit(UsageKind.Upload, custom, new(), false));
        Assert.Equal(25, UsageQuota.EffectiveLimit(UsageKind.Exam, custom, new(), false));
        Assert.Equal(3, UsageQuota.EffectiveLimit(UsageKind.Battle, custom, new(), false));
        Assert.False(new UsageCheck(UsageKind.Upload, 0, 0, DateTimeOffset.UtcNow).Allowed);
        Assert.True(new UsageCheck(UsageKind.Upload, 100, null, DateTimeOffset.UtcNow).Allowed);
    }

    [Fact]
    public void Message_displays_reset_in_Baku() => Assert.Contains("1 dekabr, 00:00", UsageQuota.LimitMessage(UsageKind.Upload, 3, DateTimeOffset.Parse("2026-11-30T20:00:00Z")));
}

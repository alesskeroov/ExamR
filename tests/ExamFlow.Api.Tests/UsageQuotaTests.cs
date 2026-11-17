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

    private static readonly PlanQuota Defaults = PlanQuota.From(new UsageOptions());
    private static readonly PlanQuota Pro = new(25, 150, 30, 30, 10, 100);
    private static readonly PlanQuota Unlimited = new(null, null, null, null, null, 100);

    [Theory]
    [InlineData(UsageKind.Upload, 3)]
    [InlineData(UsageKind.Exam, 10)]
    [InlineData(UsageKind.Battle, 3)]
    [InlineData(UsageKind.AiRequest, 100)]
    [InlineData(UsageKind.DimTest, 0)]
    [InlineData(UsageKind.Guide, 0)]
    public void Defaults_and_exemptions(UsageKind kind, int expected)
    {
        Assert.Equal(expected, UsageQuota.EffectiveLimit(kind, null, Defaults, false));
        Assert.Null(UsageQuota.EffectiveLimit(kind, null, Defaults, true));
        Assert.Null(UsageQuota.EffectiveLimit(kind, new() { Unlimited = true }, Defaults, false));
    }

    [Fact]
    public void Custom_zero_blocks_and_null_inherits()
    {
        var custom = new UserUsageLimit { MonthlyUploads = 0, MonthlyExams = 25 };
        Assert.Equal(0, UsageQuota.EffectiveLimit(UsageKind.Upload, custom, Defaults, false));
        Assert.Equal(25, UsageQuota.EffectiveLimit(UsageKind.Exam, custom, Defaults, false));
        Assert.Equal(3, UsageQuota.EffectiveLimit(UsageKind.Battle, custom, Defaults, false));
        Assert.False(new UsageCheck(UsageKind.Upload, 0, 0, DateTimeOffset.UtcNow).Allowed);
        Assert.True(new UsageCheck(UsageKind.Upload, 100, null, DateTimeOffset.UtcNow).Allowed);
    }

    [Fact]
    public void Precedence_is_admin_then_custom_then_package()
    {
        var custom = new UserUsageLimit { MonthlyDimTests = 2 };
        Assert.Equal(2, UsageQuota.EffectiveLimit(UsageKind.DimTest, custom, Pro, false));
        Assert.Equal(150, UsageQuota.EffectiveLimit(UsageKind.Exam, custom, Pro, false));
        Assert.Null(UsageQuota.EffectiveLimit(UsageKind.DimTest, custom, Pro, true));
        // A null package limit means unlimited, unless the user has their own limit.
        Assert.Null(UsageQuota.EffectiveLimit(UsageKind.Upload, null, Unlimited, false));
        Assert.Equal(5, UsageQuota.EffectiveLimit(UsageKind.Upload, new() { MonthlyUploads = 5 }, Unlimited, false));
    }

    [Fact]
    public void Zero_limit_reads_as_not_included()
    {
        var message = UsageQuota.LimitMessage(UsageKind.DimTest, 0, DateTimeOffset.Parse("2026-11-30T20:00:00Z"));
        Assert.Contains("DİM sınaqları paketinizə daxil deyil", message);
        Assert.DoesNotContain("(0)", message);
    }

    [Fact]
    public void Guide_limit_follows_package_and_custom_limit()
    {
        Assert.Equal(10, UsageQuota.EffectiveLimit(UsageKind.Guide, null, Pro, false));
        Assert.Null(UsageQuota.EffectiveLimit(UsageKind.Guide, null, Unlimited, false));
        Assert.Equal(1, UsageQuota.EffectiveLimit(UsageKind.Guide, new() { MonthlyGuides = 1 }, Pro, false));
        Assert.Contains("Öyrənmə bələdçisi paketinizə daxil deyil", UsageQuota.LimitMessage(UsageKind.Guide, 0, DateTimeOffset.Parse("2026-11-30T20:00:00Z")));
    }

    [Fact]
    public void Message_displays_reset_in_Baku() => Assert.Contains("1 dekabr, 00:00", UsageQuota.LimitMessage(UsageKind.Upload, 3, DateTimeOffset.Parse("2026-11-30T20:00:00Z")));
}

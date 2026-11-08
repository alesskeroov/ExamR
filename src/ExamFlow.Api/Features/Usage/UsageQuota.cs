namespace ExamFlow.Api.Features.Usage;

public static class UsageQuota
{
    private static readonly TimeSpan BakuOffset = TimeSpan.FromHours(4);
    private static readonly string[] Months = ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avqust", "sentyabr", "oktyabr", "noyabr", "dekabr"];

    public static DateTimeOffset MonthStart(DateTimeOffset nowUtc)
    {
        var local = nowUtc.ToOffset(BakuOffset);
        return new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, BakuOffset).ToUniversalTime();
    }

    public static DateTimeOffset NextReset(DateTimeOffset nowUtc) => MonthStart(nowUtc).ToOffset(BakuOffset).AddMonths(1).ToUniversalTime();

    public static int? EffectiveLimit(UsageKind kind, UserUsageLimit? custom, UsageOptions defaults, bool isAdmin) =>
        isAdmin || custom?.Unlimited == true ? null : kind switch
        {
            UsageKind.Upload => custom?.MonthlyUploads ?? defaults.MonthlyUploads,
            UsageKind.Exam => custom?.MonthlyExams ?? defaults.MonthlyExams,
            UsageKind.Battle => custom?.MonthlyBattles ?? defaults.MonthlyBattles,
            UsageKind.AiRequest => custom?.MonthlyAiRequests ?? defaults.MonthlyAiRequests,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static string LimitMessage(UsageKind kind, int? limit, DateTimeOffset resetsAt)
    {
        var label = kind switch { UsageKind.Upload => "sənəd yükləmə", UsageKind.Exam => "imtahan", UsageKind.Battle => "battle yaratma", _ => "AI sorğu" };
        var local = resetsAt.ToOffset(BakuOffset);
        return $"Bu ay üçün {label} limitiniz ({limit}) bitib. Limit {local.Day} {Months[local.Month - 1]}, 00:00-da yenilənir.";
    }
}

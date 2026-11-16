namespace ExamFlow.Api.Features.Usage;

/// <summary>Monthly limits of the user's package. <c>null</c> = unlimited.</summary>
public sealed record PlanQuota(int? Uploads, int? Exams, int? Battles, int? DimTests, int? AiRequests)
{
    // Used only when no Free package row exists (e.g. before the billing migration is applied).
    public static PlanQuota From(UsageOptions o) => new(o.MonthlyUploads, o.MonthlyExams, o.MonthlyBattles, o.MonthlyDimTests, o.MonthlyAiRequests);
}

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

    /// <summary>Admin → the user's own limit → the package limit. <c>null</c> = unlimited.</summary>
    public static int? EffectiveLimit(UsageKind kind, UserUsageLimit? custom, PlanQuota plan, bool isAdmin) =>
        isAdmin || custom?.Unlimited == true ? null : kind switch
        {
            UsageKind.Upload => custom?.MonthlyUploads ?? plan.Uploads,
            UsageKind.Exam => custom?.MonthlyExams ?? plan.Exams,
            UsageKind.Battle => custom?.MonthlyBattles ?? plan.Battles,
            UsageKind.DimTest => custom?.MonthlyDimTests ?? plan.DimTests,
            UsageKind.AiRequest => custom?.MonthlyAiRequests ?? plan.AiRequests,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static string LimitMessage(UsageKind kind, int? limit, DateTimeOffset resetsAt)
    {
        if (limit == 0)
        {
            var feature = kind switch
            {
                UsageKind.Upload => "Sənəd yükləmək", UsageKind.Exam => "İmtahan vermək", UsageKind.Battle => "Battle yaratmaq",
                UsageKind.DimTest => "DİM sınaqları", _ => "AI sorğuları",
            };
            return $"{feature} paketinizə daxil deyil. Paketlər səhifəsindən uyğun paket seçə bilərsiniz.";
        }
        var label = kind switch
        {
            UsageKind.Upload => "sənəd yükləmə", UsageKind.Exam => "imtahan", UsageKind.Battle => "battle yaratma",
            UsageKind.DimTest => "DİM sınağı", _ => "AI sorğu",
        };
        var local = resetsAt.ToOffset(BakuOffset);
        return $"Bu ay üçün {label} limitiniz ({limit}) bitib. Limit {local.Day} {Months[local.Month - 1]}, 00:00-da yenilənir.";
    }
}

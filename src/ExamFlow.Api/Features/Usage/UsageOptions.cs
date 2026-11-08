using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Usage;

public sealed class UsageOptions
{
    public const string Section = "UsageLimits";
    [Range(0, 100000)] public int MonthlyUploads { get; set; } = 3;
    [Range(0, 100000)] public int MonthlyExams { get; set; } = 10;
    [Range(0, 100000)] public int MonthlyBattles { get; set; } = 3;
    [Range(0, 100000)] public int MonthlyAiRequests { get; set; } = 100;
}

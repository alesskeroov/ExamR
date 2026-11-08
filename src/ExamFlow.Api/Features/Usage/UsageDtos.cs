using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Usage;

public sealed record UsageCheck(UsageKind Kind, int Used, int? Limit, DateTimeOffset ResetsAt)
{
    public bool Allowed => Limit is null || Used < Limit;
    public Dictionary<string, object?> Extensions => new() { ["kind"] = Kind.ToString(), ["used"] = Used, ["limit"] = Limit, ["resetsAt"] = ResetsAt };
    public IResult Problem() => TypedResults.Problem(UsageQuota.LimitMessage(Kind, Limit, ResetsAt), statusCode: 429, extensions: Extensions);
}

public sealed record UsageItemDto(UsageKind Kind, int Used, int? Limit);
public sealed record UsageDto(DateTimeOffset ResetsAt, bool Unlimited, IReadOnlyList<UsageItemDto> Items);
public sealed record AdminUsageDto(Guid Id, string Nickname, string? Email, bool IsAdmin, UsageDto Usage, UserUsageLimit? Custom, UsageOptions Defaults);

public sealed class UpdateUsageRequest
{
    [Range(0, 100000)] public int? MonthlyUploads { get; init; }
    [Range(0, 100000)] public int? MonthlyExams { get; init; }
    [Range(0, 100000)] public int? MonthlyBattles { get; init; }
    [Range(0, 100000)] public int? MonthlyAiRequests { get; init; }
    public bool Unlimited { get; init; }
}

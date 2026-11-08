namespace ExamFlow.Api.Features.Usage;

public enum UsageKind { Upload, Exam, Battle, AiRequest }

public sealed class UsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UsageKind Kind { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ResourceId { get; set; }
}

public sealed class UserUsageLimit
{
    public Guid UserId { get; set; }
    public int? MonthlyUploads { get; set; }
    public int? MonthlyExams { get; set; }
    public int? MonthlyBattles { get; set; }
    public int? MonthlyAiRequests { get; set; }
    public bool Unlimited { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

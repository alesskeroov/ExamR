using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Usage;

namespace ExamFlow.Api.Features.Billing;

public sealed record PlanDto(
    Guid Id, string Name, string? Description, decimal Price, int DurationDays, bool IsFree, bool IsActive, bool IsFeatured, int SortOrder,
    int? MonthlyUploads, int? MonthlyExams, int? MonthlyBattles, int? MonthlyDimTests);

public sealed record NextPlanDto(Guid PlanId, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
/// <summary>The user's package now. <c>EndsAt</c> is the end of the whole paid chain of this package; null for Free.</summary>
public sealed record CurrentPlanDto(Guid? PlanId, string Name, bool IsFree, DateTimeOffset? EndsAt, NextPlanDto? Next);

public sealed record PaymentRequestDto(
    Guid Id, string Code, Guid PlanId, string PlanName, int Periods, int Days, decimal Amount, PaymentChannel Channel, PaymentStatus Status,
    string? AdminNote, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt, string Message, string? WhatsAppUrl);

public sealed record BillingDto(
    bool Enabled, CurrentPlanDto Current, IReadOnlyList<PlanDto> Plans, string? WhatsAppUrl, string? InstagramUrl, string? Instructions,
    PaymentRequestDto? Pending);

public sealed record SubscriptionDto(
    Guid Id, Guid PlanId, string PlanName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, SubscriptionSource Source, string? Note,
    DateTimeOffset? CancelledAt, DateTimeOffset CreatedAt);

public sealed record BillingHistoryDto(IReadOnlyList<SubscriptionDto> Subscriptions, IReadOnlyList<PaymentRequestDto> Payments);

public sealed class CreatePaymentRequest
{
    public Guid PlanId { get; init; }
    [Range(1, BillingRules.MaxPeriods)] public int Periods { get; init; } = 1;
    public PaymentChannel Channel { get; init; } = PaymentChannel.WhatsApp;
}

// ---- Admin ----

public sealed record CustomLimitsDto(int? MonthlyUploads, int? MonthlyExams, int? MonthlyBattles, int? MonthlyDimTests, bool Unlimited);

public sealed record AdminBillingUserDto(
    Guid Id, string Nickname, string? Email, bool IsAdmin, CurrentPlanDto Plan, UsageDto Usage, CustomLimitsDto? Custom, bool HasPending);

public sealed record AdminBillingUserDetailDto(AdminBillingUserDto User, BillingHistoryDto History);

public sealed record AdminPaymentDto(Guid UserId, string Nickname, string? Email, PaymentRequestDto Request, string? ReviewedBy);

public sealed record AdminPlanDto(PlanDto Plan, int ActiveUsers);

public sealed record PlanChangePreviewDto(PlanChangeKind Kind, string? CurrentPlanName, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int BonusDays);

public sealed class ApprovePaymentRequest
{
    [Range(1, BillingRules.MaxPeriods)] public int? Periods { get; init; }
    /// <summary>Extra days on top of the package; for an upgrade it replaces the computed bonus. Null = computed.</summary>
    [Range(0, 365)] public int? BonusDays { get; init; }
    [MaxLength(500)] public string? Note { get; init; }
}

public sealed class RejectPaymentRequest
{
    [MaxLength(500)] public string? Note { get; init; }
}

public enum BulkAction { AssignPlan, Extend, CancelPlan, SetLimits, ClearLimits, SetUnlimited }

public sealed class BulkRequest
{
    [Required, MinLength(1), MaxLength(200)] public List<Guid> UserIds { get; init; } = [];
    public BulkAction Action { get; init; }
    public Guid? PlanId { get; init; }
    [Range(1, BillingRules.MaxPeriods)] public int? Periods { get; init; }
    /// <summary>AssignPlan: extra days; Extend: days to add.</summary>
    [Range(1, 365)] public int? Days { get; init; }
    [Range(0, 100000)] public int? MonthlyUploads { get; init; }
    [Range(0, 100000)] public int? MonthlyExams { get; init; }
    [Range(0, 100000)] public int? MonthlyBattles { get; init; }
    [Range(0, 100000)] public int? MonthlyDimTests { get; init; }
    [MaxLength(500)] public string? Note { get; init; }
}

public sealed record BulkItemDto(Guid UserId, bool Updated, string? Reason);
public sealed record BulkResultDto(int Updated, int Skipped, IReadOnlyList<BulkItemDto> Items);

public sealed class UpsertPlanRequest
{
    [Required, MaxLength(40)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
    [Range(0, 10000)] public decimal Price { get; init; }
    [Range(1, 365)] public int DurationDays { get; init; } = 30;
    public bool IsActive { get; init; } = true;
    public bool IsFeatured { get; init; }
    [Range(0, 1000)] public int SortOrder { get; init; }
    [Range(0, 100000)] public int? MonthlyUploads { get; init; }
    [Range(0, 100000)] public int? MonthlyExams { get; init; }
    [Range(0, 100000)] public int? MonthlyBattles { get; init; }
    [Range(0, 100000)] public int? MonthlyDimTests { get; init; }
}

public sealed record BillingSettingsDto(bool Enabled, string? WhatsAppNumber, string? InstagramUsername, string? PaymentInstructions, DateTimeOffset UpdatedAt);

public sealed class UpdateBillingSettingsRequest
{
    public bool Enabled { get; init; }
    [RegularExpression(@"^994\d{9}$", ErrorMessage = "WhatsApp nömrəsi 994XXXXXXXXX formatında olmalıdır.")]
    public string? WhatsAppNumber { get; init; }
    [RegularExpression(@"^[A-Za-z0-9._]{1,30}$", ErrorMessage = "Instagram adı yalnız hərf, rəqəm, nöqtə və alt xətdən ibarət olmalıdır.")]
    public string? InstagramUsername { get; init; }
    [MaxLength(1000)] public string? PaymentInstructions { get; init; }
}

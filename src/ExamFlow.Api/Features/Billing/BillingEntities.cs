namespace ExamFlow.Api.Features.Billing;

public enum SubscriptionSource { Payment, Manual }
public enum PaymentStatus { Pending, Approved, Rejected, Cancelled }
public enum PaymentChannel { WhatsApp, Instagram, Other }

/// <summary>A package. Limits are monthly; <c>null</c> = unlimited, 0 = not included.</summary>
public sealed class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; } = 30;
    public bool IsFree { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public int? MonthlyUploads { get; set; }
    public int? MonthlyExams { get; set; }
    public int? MonthlyBattles { get; set; }
    public int? MonthlyDimTests { get; set; }
    public int? MonthlyGuides { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>One paid (or granted) period. Active = not cancelled and StartsAt &lt;= now &lt; EndsAt.</summary>
public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public SubscriptionSource Source { get; set; }
    public Guid? PaymentRequestId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class PaymentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public int Periods { get; set; } = 1;
    public decimal Amount { get; set; }
    public PaymentChannel Channel { get; set; }
    public PaymentStatus Status { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}

/// <summary>Single row (Id = 1): sales switch and how users reach the admin to pay.</summary>
public sealed class BillingSettings
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? InstagramUsername { get; set; }
    public string? PaymentInstructions { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

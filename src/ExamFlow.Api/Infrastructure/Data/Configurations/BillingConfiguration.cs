using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public static readonly Guid FreeId = Guid.Parse("6a1f0c3e-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Seeded = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(40).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Price).HasPrecision(10, 2);
        b.HasIndex(x => x.IsFree).IsUnique().HasFilter("\"IsFree\"");
        b.ToTable("Plans", t =>
        {
            foreach (var column in new[] { "MonthlyUploads", "MonthlyExams", "MonthlyBattles", "MonthlyDimTests" })
                t.HasCheckConstraint($"CK_Plans_{column}", $"\"{column}\" IS NULL OR \"{column}\" BETWEEN 0 AND 100000");
            t.HasCheckConstraint("CK_Plans_Price", "\"Price\" >= 0");
            t.HasCheckConstraint("CK_Plans_DurationDays", "\"DurationDays\" BETWEEN 1 AND 365");
        });
        b.HasData(
            Seed(FreeId, "Free", "Saytı sınamaq üçün", 0, 30, 0, 1, 3, 0, 0, isFree: true),
            Seed(Guid.Parse("6a1f0c3e-0000-4000-8000-000000000002"), "İmtahan həftəsi", "İmtahandan əvvəlki 7 gün üçün", 3, 7, 1, 3, 30, 5, 5),
            Seed(Guid.Parse("6a1f0c3e-0000-4000-8000-000000000003"), "Standart", "Müntəzəm hazırlıq üçün", 5, 30, 2, 10, 60, 10, 10),
            Seed(Guid.Parse("6a1f0c3e-0000-4000-8000-000000000004"), "Pro", "Ən çox seçilən paket", 7, 30, 3, 25, 150, 30, 30, featured: true),
            Seed(Guid.Parse("6a1f0c3e-0000-4000-8000-000000000005"), "Premium", "Limitsiz istifadə", 10, 30, 4, null, null, null, null));
    }

    private static Plan Seed(Guid id, string name, string description, decimal price, int days, int order,
        int? uploads, int? exams, int? battles, int? dim, bool isFree = false, bool featured = false) => new()
    {
        Id = id, Name = name, Description = description, Price = price, DurationDays = days, SortOrder = order, IsFree = isFree,
        IsActive = true, IsFeatured = featured, MonthlyUploads = uploads, MonthlyExams = exams, MonthlyBattles = battles,
        MonthlyDimTests = dim, UpdatedAt = Seeded,
    };
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PaymentRequest>().WithMany().HasForeignKey(x => x.PaymentRequestId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.UserId, x.EndsAt });
        b.ToTable("Subscriptions", t => t.HasCheckConstraint("CK_Subscriptions_Period", "\"EndsAt\" > \"StartsAt\""));
    }
}

public sealed class PaymentRequestConfiguration : IEntityTypeConfiguration<PaymentRequest>
{
    public void Configure(EntityTypeBuilder<PaymentRequest> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(12).IsRequired();
        b.Property(x => x.Amount).HasPrecision(10, 2);
        b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AdminNote).HasMaxLength(500);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Code).IsUnique();
        // One open request per user: a second click returns the existing one.
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("\"Status\" = 'Pending'");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.ToTable("PaymentRequests", t => t.HasCheckConstraint("CK_PaymentRequests_Periods", "\"Periods\" BETWEEN 1 AND 12"));
    }
}

public sealed class BillingSettingsConfiguration : IEntityTypeConfiguration<BillingSettings>
{
    public void Configure(EntityTypeBuilder<BillingSettings> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WhatsAppNumber).HasMaxLength(20);
        b.Property(x => x.InstagramUsername).HasMaxLength(30);
        b.Property(x => x.PaymentInstructions).HasMaxLength(1000);
        b.ToTable("BillingSettings", t => t.HasCheckConstraint("CK_BillingSettings_Single", "\"Id\" = 1"));
        b.HasData(new BillingSettings { Id = 1, Enabled = false, InstagramUsername = "examr_az", UpdatedAt = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero) });
    }
}

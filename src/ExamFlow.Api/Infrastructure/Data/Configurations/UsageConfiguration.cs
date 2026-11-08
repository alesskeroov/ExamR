using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public sealed class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.Kind, x.OccurredAt });
        b.HasIndex(x => new { x.ResourceId, x.Kind });
    }
}

public sealed class UserUsageLimitConfiguration : IEntityTypeConfiguration<UserUsageLimit>
{
    public void Configure(EntityTypeBuilder<UserUsageLimit> b)
    {
        b.HasKey(x => x.UserId);
        b.HasOne<AppUser>().WithOne().HasForeignKey<UserUsageLimit>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.ToTable("UserUsageLimits", t =>
        {
            foreach (var column in new[] { "MonthlyUploads", "MonthlyExams", "MonthlyBattles", "MonthlyAiRequests" })
                t.HasCheckConstraint($"CK_UserUsageLimits_{column}", $"\"{column}\" IS NULL OR \"{column}\" BETWEEN 0 AND 100000");
        });
    }
}

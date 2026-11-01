using ExamFlow.Api.Features.Battles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class BattleResultConfiguration : IEntityTypeConfiguration<BattleResult>
{
    public void Configure(EntityTypeBuilder<BattleResult> builder)
    {
        builder.ToTable("BattleResults");

        builder.Property(r => r.RoomCode).HasMaxLength(8).IsUnicode(false).IsRequired();
        builder.Property(r => r.PlayedAt).HasPrecision(3);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.UserId, r.PlayedAt }).IsDescending(false, true);
        builder.HasIndex(r => new { r.BattleId, r.UserId }).IsUnique();
    }
}

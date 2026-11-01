using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Battles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class StoredBattleRoomConfiguration : IEntityTypeConfiguration<StoredBattleRoom>, IEntityTypeConfiguration<StoredBattleMember>
{
    public void Configure(EntityTypeBuilder<StoredBattleRoom> builder)
    {
        builder.Property(r => r.Code).HasMaxLength(6).IsUnicode(false);
        builder.HasIndex(r => r.Code).IsUnique();
        builder.HasIndex(r => new { r.HostUserId, r.Status });
        builder.HasIndex(r => r.CreatedById);
        builder.Property(r => r.Name).HasMaxLength(100);
        builder.Property(r => r.DocumentName).HasMaxLength(512);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Version).IsRowVersion();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.HostUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Members).WithOne().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<StoredBattleMember> builder)
    {
        builder.HasKey(m => new { m.RoomId, m.UserId });
        builder.HasIndex(m => m.UserId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

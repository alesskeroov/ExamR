using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Friends;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>, IEntityTypeConfiguration<UserBlock>,
    IEntityTypeConfiguration<SocialNotification>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.HasKey(f => new { f.UserLowId, f.UserHighId });
        builder.HasIndex(f => f.UserHighId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(f => f.UserLowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(f => f.UserHighId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(f => f.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.HasKey(b => new { b.UserId, b.BlockedUserId });
        builder.HasIndex(b => b.BlockedUserId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(b => b.BlockedUserId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SocialNotification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Kind).HasMaxLength(24);
        builder.Property(n => n.Status).HasMaxLength(24);
        builder.Property(n => n.RoomCode).HasMaxLength(6).IsUnicode(false);
        builder.Property(n => n.HostName).HasMaxLength(100);
        builder.Property(n => n.DocumentName).HasMaxLength(512);
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

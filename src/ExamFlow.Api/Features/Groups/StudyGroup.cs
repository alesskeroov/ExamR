using ExamFlow.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Groups;

public sealed class StudyGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<StudyGroupMember> Members { get; set; } = new List<StudyGroupMember>();
}

public sealed class StudyGroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public StudyGroup Group { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

public sealed class StudyGroupConfiguration : IEntityTypeConfiguration<StudyGroup>, IEntityTypeConfiguration<StudyGroupMember>
{
    public void Configure(EntityTypeBuilder<StudyGroup> builder)
    {
        builder.ToTable("StudyGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(120);
        builder.Property(g => g.NormalizedName).HasMaxLength(120).UseCollation("Latin1_General_100_BIN2");
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.HasIndex(g => g.NormalizedName).IsUnique();
    }

    public void Configure(EntityTypeBuilder<StudyGroupMember> builder)
    {
        builder.ToTable("StudyGroupMembers");
        builder.HasKey(m => new { m.GroupId, m.UserId });
        builder.HasIndex(m => m.UserId);
        builder.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Documents;

public sealed class DocumentUserShare
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
}
public sealed class DocumentGroupShare
{
    public Guid DocumentId { get; set; }
    public Guid GroupId { get; set; }
}
public sealed class DocumentShareConfiguration : IEntityTypeConfiguration<DocumentUserShare>, IEntityTypeConfiguration<DocumentGroupShare>
{
    public void Configure(EntityTypeBuilder<DocumentUserShare> b)
    {
        b.ToTable("DocumentUserShares");
        b.HasKey(s => new { s.DocumentId, s.UserId });
        b.HasIndex(s => s.UserId);
        b.HasOne<UploadedDocument>().WithMany().HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<DocumentGroupShare> b)
    {
        b.ToTable("DocumentGroupShares");
        b.HasKey(s => new { s.DocumentId, s.GroupId });
        b.HasIndex(s => s.GroupId);
        b.HasOne<UploadedDocument>().WithMany().HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<StudyGroup>().WithMany().HasForeignKey(s => s.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}

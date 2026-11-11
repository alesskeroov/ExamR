using ExamFlow.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Feedback;

public sealed class FeedbackMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string? DocumentName { get; set; }
    public string PagePath { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseId { get; set; }
}

public sealed class FeedbackMessageConfiguration : IEntityTypeConfiguration<FeedbackMessage>
{
    public void Configure(EntityTypeBuilder<FeedbackMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(16);
        builder.Property(x => x.Text).HasMaxLength(1500);
        builder.Property(x => x.SenderName).HasMaxLength(100);
        builder.Property(x => x.SenderEmail).HasMaxLength(256);
        builder.Property(x => x.DocumentName).HasMaxLength(255);
        builder.Property(x => x.PagePath).HasMaxLength(256);
        builder.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique();
        builder.HasIndex(x => x.NextAttemptAt).HasFilter("\"SentAt\" IS NULL");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

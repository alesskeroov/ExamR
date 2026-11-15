using ExamFlow.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class QuestionPassageConfiguration : IEntityTypeConfiguration<QuestionPassage>
{
    public void Configure(EntityTypeBuilder<QuestionPassage> builder)
    {
        builder.ToTable("QuestionPassages", t =>
            t.HasCheckConstraint("CK_QuestionPassages_Range", "\"FromNumber\" >= 1 AND \"ToNumber\" >= \"FromNumber\""));

        builder.Property(p => p.Title).HasMaxLength(QuestionPassage.MaxTitleLength);
        builder.Property(p => p.Text).HasMaxLength(QuestionPassage.MaxTextLength).IsRequired();
        builder.Property(p => p.CreatedAt).HasPrecision(3);
        builder.Property(p => p.UpdatedAt).HasPrecision(3);

        builder.HasOne(p => p.Document)
            .WithMany()
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.DocumentId, p.FromNumber });
    }
}

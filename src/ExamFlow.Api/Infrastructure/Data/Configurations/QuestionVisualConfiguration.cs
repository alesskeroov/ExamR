using ExamFlow.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class QuestionVisualConfiguration : IEntityTypeConfiguration<QuestionVisual>
{
    public void Configure(EntityTypeBuilder<QuestionVisual> builder)
    {
        builder.ToTable("QuestionVisuals", t =>
            t.HasCheckConstraint("CK_QuestionVisuals_OptionPosition", "[OptionPosition] IS NULL OR [OptionPosition] BETWEEN 0 AND 5"));

        builder.Property(v => v.MappingStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(v => v.Note).HasMaxLength(200);
        builder.Property(v => v.StorageKey).HasMaxLength(120).IsUnicode(false);
        builder.Property(v => v.ContentType).HasMaxLength(50).IsUnicode(false);
        builder.Property(v => v.Sha256).HasColumnType("binary(32)").IsRequired();
        builder.Property(v => v.CreatedAt).HasPrecision(3);

        builder.HasOne(v => v.Document)
            .WithMany()
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction: Document → Question → Visual plus Document → Visual would be multiple cascade paths in SQL Server.
        // Reprocessing removes a document's visuals together with its questions.
        builder.HasOne(v => v.Question)
            .WithMany()
            .HasForeignKey(v => v.QuestionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(v => new { v.DocumentId, v.SortOrder });
        builder.HasIndex(v => v.QuestionId);
    }
}

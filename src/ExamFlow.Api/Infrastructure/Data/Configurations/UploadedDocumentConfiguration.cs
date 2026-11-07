using ExamFlow.Api.Features.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class UploadedDocumentConfiguration : IEntityTypeConfiguration<UploadedDocument>
{
    public void Configure(EntityTypeBuilder<UploadedDocument> builder)
    {
        builder.ToTable("UploadedDocuments");

        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.StoredFileName).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(d => d.Sha256).HasColumnType("bytea").IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(d => d.ErrorMessage).HasMaxLength(1000);
        builder.Property(d => d.CreatedAt).HasPrecision(3);
        builder.Property(d => d.UpdatedAt).HasPrecision(3);
        builder.Property(d => d.ProcessedAt).HasPrecision(3);

        builder.OwnsOne(d => d.ProcessingReport, report => report.ToJson());

        builder.HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(d => new { d.UploadedByUserId, d.Sha256 }).IsUnique()
            .HasDatabaseName("UX_UploadedDocuments_User_Sha256");
        builder.HasIndex(d => d.UploadedByUserId);
        builder.HasIndex(d => d.Status);
    }
}

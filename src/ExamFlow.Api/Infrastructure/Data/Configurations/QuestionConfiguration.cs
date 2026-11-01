using ExamFlow.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public const string SearchCollation = "Azeri_Latin_100_CI_AS";

    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.Property(q => q.Text).HasMaxLength(4000).UseCollation(SearchCollation).IsRequired();
        builder.Property(q => q.ContentHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(q => q.VerificationStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(q => q.ParseWarning).HasMaxLength(500);
        builder.Property(q => q.CreatedAt).HasPrecision(3);
        builder.Property(q => q.UpdatedAt).HasPrecision(3);
        builder.Property(q => q.RowVersion).IsRowVersion();

        builder.HasOne(q => q.Document)
            .WithMany(d => d.Questions)
            .HasForeignKey(q => q.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Options)
            .WithOne()
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.DocumentId, q.SourceNumber }).IncludeProperties(q => q.VerificationStatus);
        builder.HasIndex(q => q.ContentHash);
    }
}

public class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("AnswerOptions", t =>
            t.HasCheckConstraint("CK_AnswerOptions_Position", "[Position] BETWEEN 0 AND 5"));

        builder.Property(o => o.Label).HasColumnType("char(1)").IsRequired();
        builder.Property(o => o.Text).HasMaxLength(1000).UseCollation(QuestionConfiguration.SearchCollation).IsRequired();

        builder.HasIndex(o => new { o.QuestionId, o.Position }).IsUnique().HasDatabaseName("UX_AnswerOptions_QuestionId_Position");
        builder.HasIndex(o => o.QuestionId)
            .IsUnique()
            .HasFilter("[IsCorrect] = 1")
            .HasDatabaseName("UX_AnswerOptions_SingleCorrect");
    }
}

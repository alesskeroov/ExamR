using ExamFlow.Api.Features.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams", t =>
            t.HasCheckConstraint("CK_Exams_QuestionCount", $"[QuestionCount] BETWEEN 1 AND {ExamOptions.HardMaxQuestions}"));

        builder.Property(e => e.RevealMode).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.CompletionReason).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.StartedAt).HasPrecision(3);
        builder.Property(e => e.ExpiresAt).HasPrecision(3);
        builder.Property(e => e.CompletedAt).HasPrecision(3);
        builder.Property(e => e.ScorePercent).HasPrecision(5, 2);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Questions)
            .WithOne()
            .HasForeignKey(q => q.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.StartedAt }).IsDescending(false, true);

        // One active exam per user (EX-9); also guards against two parallel "start" requests.
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("[Status] = 'InProgress'")
            .HasDatabaseName("UX_Exams_UserActive");
    }
}

public class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ToTable("ExamQuestions");

        builder.Property(q => q.Outcome).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(q => q.AnsweredAt).HasPrecision(3);

        builder.OwnsOne(q => q.Snapshot, snapshot =>
        {
            snapshot.ToJson();
            snapshot.OwnsMany(s => s.Options);
        });

        // Deleting a bank question keeps past exams intact (they use the snapshot).
        builder.HasOne(q => q.Question)
            .WithMany()
            .HasForeignKey(q => q.QuestionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(q => new { q.ExamId, q.Position }).IsUnique().HasDatabaseName("UX_ExamQuestions_ExamId_Position");
    }
}

using System.Text.Json;
using ExamFlow.Api.Features.Dim;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Infrastructure.Data.Configurations;

public class DimBankQuestionConfiguration : IEntityTypeConfiguration<DimBankQuestion>
{
    public void Configure(EntityTypeBuilder<DimBankQuestion> builder)
    {
        builder.ToTable("DimBankQuestions");
        builder.HasKey(b => b.QuestionId);
        builder.Property(b => b.Track).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(b => b.Subject).HasMaxLength(10).IsUnicode(false);
        builder.Property(b => b.Topic).HasMaxLength(200);
        builder.Property(b => b.ApprovedAt).HasPrecision(3);
        // Deleting the source document removes its questions from the bank too.
        builder.HasOne(b => b.Question).WithOne().HasForeignKey<DimBankQuestion>(b => b.QuestionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.Track, b.Subject });
    }
}

public class DimTestConfiguration : IEntityTypeConfiguration<DimTest>
{
    public void Configure(EntityTypeBuilder<DimTest> builder)
    {
        builder.ToTable("DimTests");
        builder.Property(t => t.Track).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(t => t.GroupCode).HasMaxLength(10).IsUnicode(false);
        builder.Property(t => t.Title).HasMaxLength(200);
        builder.Property(t => t.AvailableFrom).HasPrecision(3);
        builder.Property(t => t.AvailableTo).HasPrecision(3);
        builder.Property(t => t.CreatedAt).HasPrecision(3);
        builder.Property(t => t.Sections).HasColumnType("jsonb").Json();
        builder.HasIndex(t => new { t.Track, t.IsPublished });
    }
}

public class DimAttemptConfiguration : IEntityTypeConfiguration<DimAttempt>
{
    public void Configure(EntityTypeBuilder<DimAttempt> builder)
    {
        builder.ToTable("DimAttempts");
        builder.HasKey(a => a.ExamId);
        builder.Property(a => a.StartedAt).HasPrecision(3);
        builder.Property(a => a.Items).HasColumnType("jsonb").Json();
        builder.HasOne(a => a.Exam).WithOne().HasForeignKey<DimAttempt>(a => a.ExamId).OnDelete(DeleteBehavior.Cascade);
        // A test with attempts cannot be deleted (only unpublished), so results keep their test.
        builder.HasOne(a => a.Test).WithMany().HasForeignKey(a => a.TestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.TestId, a.UserId });
    }
}

internal static class JsonColumnExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Stores a list of plain objects as one JSON document, compared by content for change tracking.</summary>
    public static PropertyBuilder<List<T>> Json<T>(this PropertyBuilder<List<T>> property) =>
        property.HasConversion(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<List<T>>(v, Options) ?? new List<T>(),
            new ValueComparer<List<T>>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                v => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(v, Options), Options)!));
}

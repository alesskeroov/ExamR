using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Courses;

/// <summary>Material = lecture file read in the guide; Bank = exam PDF whose questions the student practises.</summary>
public enum CourseDocumentRole { Material, Bank }

/// <summary>A student's subject ("Fənn"): lecture materials, question banks and the exam date, private to its owner.</summary>
public sealed class StudyCourse
{
    public const int MaxName = 100;
    public const int MaxCoursesPerUser = 50;
    public const int MaxMaterials = 10;
    public const int MaxBanks = 3;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly? ExamDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<StudyCourseDocument> Documents { get; set; } = [];
}

public sealed class StudyCourseDocument
{
    public Guid CourseId { get; set; }
    public Guid DocumentId { get; set; }
    public UploadedDocument Document { get; set; } = null!;
    public CourseDocumentRole Role { get; set; }
    /// <summary>Order the student listed the files in.</summary>
    public int Position { get; set; }
}

public sealed class StudyCourseConfiguration : IEntityTypeConfiguration<StudyCourse>, IEntityTypeConfiguration<StudyCourseDocument>
{
    public void Configure(EntityTypeBuilder<StudyCourse> builder)
    {
        builder.ToTable("StudyCourses");
        builder.Property(c => c.Name).HasMaxLength(StudyCourse.MaxName).IsRequired();
        builder.HasIndex(c => c.UserId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Documents).WithOne().HasForeignKey(d => d.CourseId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<StudyCourseDocument> builder)
    {
        builder.ToTable("StudyCourseDocuments");
        builder.HasKey(d => new { d.CourseId, d.DocumentId });
        builder.Property(d => d.Role).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(d => d.DocumentId);
        // A deleted file simply leaves the course.
        builder.HasOne(d => d.Document).WithMany().HasForeignKey(d => d.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

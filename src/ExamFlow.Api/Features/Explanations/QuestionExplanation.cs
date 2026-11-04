using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Explanations;

/// <summary>
/// One explanation per question content (text + options + correct answer), shared by every PDF and exam that holds
/// the same question. Only <see cref="ExplanationStatus.Approved"/> rows are ever shown to users.
/// </summary>
public class QuestionExplanation : IAuditable
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the normalised question content; see <see cref="ExplanationEngine.Fingerprint"/>.</summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>Bank question the explanation was made for (admin listing only); cleared if that question is deleted.</summary>
    public Guid? SourceQuestionId { get; set; }

    public string Explanation { get; set; } = string.Empty;
    public ExplanationQuestionType QuestionType { get; set; }
    public ExplanationStatus Status { get; set; }

    /// <summary>Why the row needs a human look, e.g. the model's result disagrees with the answer key.</summary>
    public string? ReviewReason { get; set; }

    /// <summary>Model that wrote it; null when an admin wrote it by hand.</summary>
    public string? Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum ExplanationQuestionType { Theory, Math }

public enum ExplanationStatus { Draft, Review, Approved, Rejected }

/// <summary>An admin-started AI generation run for one PDF; progress survives restarts because it lives in the database.</summary>
public class ExplanationJob : IAuditable
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public ExplanationJobStatus Status { get; set; }
    public int Total { get; set; }
    public int Done { get; set; }
    public int Failed { get; set; }
    public decimal EstimatedUsd { get; set; }

    /// <summary>Real spend including failed calls that were still billed.</summary>
    public decimal SpentUsd { get; set; }

    /// <summary>"Mock" or "OpenAI": which provider produced this run.</summary>
    public string Provider { get; set; } = string.Empty;
    public int Requests { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>Questions whose answer could not be used in this run; they are skipped on resume so a bad item is not paid for twice.</summary>
    public List<Guid> FailedQuestionIds { get; set; } = [];

    /// <summary>Short machine code of the last failure (never provider text).</summary>
    public string? LastError { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum ExplanationJobStatus { Pending, Running, Completed, Failed, Cancelled, BudgetExhausted }

public sealed class ExplanationConfiguration : IEntityTypeConfiguration<QuestionExplanation>, IEntityTypeConfiguration<ExplanationJob>
{
    public const int MaxExplanationLength = 4000;

    public void Configure(EntityTypeBuilder<QuestionExplanation> builder)
    {
        builder.ToTable("QuestionExplanations");
        builder.Property(e => e.Fingerprint).HasColumnType("binary(32)").IsRequired();
        builder.HasIndex(e => e.Fingerprint).IsUnique();
        builder.Property(e => e.Explanation).HasMaxLength(MaxExplanationLength);
        builder.Property(e => e.QuestionType).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(e => e.ReviewReason).HasMaxLength(300);
        builder.Property(e => e.Model).HasMaxLength(64).IsUnicode(false);
        builder.Property(e => e.CostUsd).HasPrecision(18, 6);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.SourceQuestionId);
        builder.HasOne<Question>().WithMany().HasForeignKey(e => e.SourceQuestionId).OnDelete(DeleteBehavior.SetNull);
    }

    public void Configure(EntityTypeBuilder<ExplanationJob> builder)
    {
        builder.ToTable("ExplanationJobs");
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(j => j.EstimatedUsd).HasPrecision(18, 6);
        builder.Property(j => j.SpentUsd).HasPrecision(18, 6);
        builder.Property(j => j.LastError).HasMaxLength(64).IsUnicode(false);
        builder.Property(j => j.Provider).HasMaxLength(16).IsUnicode(false);
        builder.HasIndex(j => j.CreatedAt);
        // At most one unfinished run per PDF, also across API instances.
        builder.HasIndex(j => j.DocumentId).IsUnique().HasFilter("[Status] IN ('Pending', 'Running')").HasDatabaseName("IX_ExplanationJobs_DocumentId_Active");
        builder.HasOne<UploadedDocument>().WithMany().HasForeignKey(j => j.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

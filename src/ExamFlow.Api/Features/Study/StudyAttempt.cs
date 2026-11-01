using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Study;

public sealed class StudyAttempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public string Prompt { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public int? CorrectIndex { get; set; }
    public string? ExpectedAnswer { get; set; }
    public string? ReferenceText { get; set; }
    public DateTimeOffset? EvaluationLeaseUntil { get; set; }
    public string? Answer { get; set; }
    public int? Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
}

public sealed class StudyAttemptConfiguration : IEntityTypeConfiguration<StudyAttempt>
{
    public void Configure(EntityTypeBuilder<StudyAttempt> builder)
    {
        builder.ToTable("StudyAttempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Prompt).HasMaxLength(4000);
        builder.Property(a => a.ExpectedAnswer).HasMaxLength(4000);
        builder.Property(a => a.ReferenceText).HasMaxLength(16000);
        builder.Property(a => a.Answer).HasMaxLength(16000);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Question>().WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.UserId, a.QuestionId, a.CompletedAt });
    }
}

public sealed class StudyAiTurn
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DocumentId { get; set; }
    public string Message { get; set; } = "";
    public string? ReplyJson { get; set; }
    public string Model { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int InputTokens { get; set; }
    public int CachedInputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool IsEstimated { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StudyAiTurnConfiguration : IEntityTypeConfiguration<StudyAiTurn>
{
    public void Configure(EntityTypeBuilder<StudyAiTurn> builder)
    {
        builder.ToTable("StudyAiTurns");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Message).HasMaxLength(16000);
        builder.Property(t => t.Model).HasMaxLength(100);
        builder.Property(t => t.Status).HasMaxLength(20);
        builder.Property(t => t.EstimatedCostUsd).HasPrecision(18, 8);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.NoAction);
        // Keep usage accounting after a source document is removed or reprocessed.
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });
        builder.HasIndex(t => new { t.UserId, t.DocumentId, t.CreatedAt });
    }
}

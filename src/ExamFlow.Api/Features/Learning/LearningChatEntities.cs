using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

public enum LearningMessageRole { User, Assistant }
public enum LearningMessageStatus { Completed, Cancelled, Failed }
public enum LearningUsageStatus { Reserved, Completed, Failed, Released }

/// <summary>A tutor conversation bound to exactly one indexed document; the document never changes for a session.</summary>
public sealed class LearningChatSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<LearningChatMessage> Messages { get; set; } = [];
}

public sealed class LearningChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public LearningMessageRole Role { get; set; }
    public string Content { get; set; } = "";
    public LearningMessageStatus Status { get; set; }
    /// <summary>Assistant only: false when the document had no matching source and the model was not called.</summary>
    public bool? Grounded { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<LearningMessageSource> Sources { get; set; } = [];
}

/// <summary>Citation shown under an answer; copied from chunk metadata, never from model output.</summary>
public sealed class LearningMessageSource
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public int Number { get; set; }
    public int ChunkOrdinal { get; set; }
    public int? PageFrom { get; set; }
    public int? PageTo { get; set; }
    public string? Section { get; set; }
    public string Excerpt { get; set; } = "";
    public double Score { get; set; }
    public bool Cited { get; set; }
}

/// <summary>
/// One model call (or reservation for one). Kept after a chat is deleted so the budget cannot be reset by deleting
/// history. Local estimate only: OpenAI's own billing/limits stay the source of truth.
/// </summary>
public sealed class LearningAiUsage
{
    public bool CountsTowardsMonthlyQuota { get; set; } = true;
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? MessageId { get; set; }
    public string Model { get; set; } = "";
    public LearningUsageStatus Status { get; set; }
    public int InputTokens { get; set; }
    public int CachedInputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal ReservedUsd { get; set; }
    public decimal CostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class LearningChatConfiguration :
    IEntityTypeConfiguration<LearningChatSession>, IEntityTypeConfiguration<LearningChatMessage>,
    IEntityTypeConfiguration<LearningMessageSource>, IEntityTypeConfiguration<LearningAiUsage>
{
    public void Configure(EntityTypeBuilder<LearningChatSession> builder)
    {
        builder.ToTable("LearningChatSessions");
        builder.Property(s => s.Title).HasMaxLength(120);
        builder.HasIndex(s => new { s.UserId, s.UpdatedAt });
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(s => s.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Messages).WithOne().HasForeignKey(m => m.SessionId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<LearningChatMessage> builder)
    {
        builder.ToTable("LearningChatMessages");
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(m => new { m.SessionId, m.CreatedAt });
        builder.HasMany(m => m.Sources).WithOne().HasForeignKey(s => s.MessageId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<LearningMessageSource> builder)
    {
        builder.ToTable("LearningMessageSources");
        builder.Property(s => s.Section).HasMaxLength(200);
        builder.Property(s => s.Excerpt).HasMaxLength(400);
    }

    public void Configure(EntityTypeBuilder<LearningAiUsage> builder)
    {
        builder.ToTable("LearningAiUsage");
        builder.Property(u => u.Model).HasMaxLength(64);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(u => u.ReservedUsd).HasPrecision(18, 6);
        builder.Property(u => u.CostUsd).HasPrecision(18, 6);
        builder.HasIndex(u => new { u.UserId, u.CreatedAt });
        builder.HasIndex(u => u.Status);
    }
}

/// <summary>Section "LearningAi". The API key is read from the OPENAI_API_KEY / OpenAI__ApiKey environment, never from appsettings.</summary>
public sealed class LearningAiOptions
{
    public const string Section = "LearningAi";
    /// <summary>Feature flag; off by default so nothing can spend money until it is switched on.</summary>
    public bool Enabled { get; set; }
    [Required] public string Model { get; set; } = "gpt-4.1-mini";
    /// <summary>Cheaper model for mechanical steps (grouping questions); quality-sensitive work stays on <see cref="Model"/>.</summary>
    [Required] public string LightModel { get; set; } = "gpt-4o-mini";
    [Range(0, 1000)] public decimal LightInputUsdPerMillion { get; set; } = 0.15m;
    [Range(0, 1000)] public decimal LightCachedInputUsdPerMillion { get; set; } = 0.075m;
    [Range(0, 1000)] public decimal LightOutputUsdPerMillion { get; set; } = 0.60m;
    [Required, Url] public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    // USD per 1M tokens for Model (standard tier, checked against OpenAI pricing on 2026-11-02).
    [Range(0, 1000)] public decimal InputUsdPerMillion { get; set; } = 0.40m;
    [Range(0, 1000)] public decimal CachedInputUsdPerMillion { get; set; } = 0.10m;
    [Range(0, 1000)] public decimal OutputUsdPerMillion { get; set; } = 1.60m;
    [Range(0, 1000)] public decimal TotalBudgetUsd { get; set; } = 4m;
    [Range(1, 10000)] public int DailyRequestsPerUser { get; set; } = 40;
    [Range(64, 4000)] public int MaxOutputTokens { get; set; } = 900;
    [Range(5, 300)] public int TimeoutSeconds { get; set; } = 60;
    [Range(1, 10)] public int ContextChunks { get; set; } = 5;
    [Range(0, 20)] public int HistoryMessages { get; set; } = 6;
    [Range(0, 20000)] public int HistoryMaxCharacters { get; set; } = 4000;
    [Range(1, 4000)] public int MaxQuestionCharacters { get; set; } = 1500;
}

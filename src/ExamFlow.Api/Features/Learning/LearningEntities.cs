using ExamFlow.Api.Features.Documents;
using ExamFlow.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

public enum LearningStatus { Pending, Processing, Completed, Failed }

/// <summary>Text index of an uploaded document for the AI tutor. One per document; the file itself stays in IFileStorage.</summary>
public sealed class LearningDocument
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public UploadedDocument Document { get; set; } = null!;
    public LearningStatus Status { get; set; }
    public string Format { get; set; } = "";
    public int? PageCount { get; set; }
    public int ParagraphCount { get; set; }
    public long CharacterCount { get; set; }
    public int ChunkCount { get; set; }
    public bool LooksScanned { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public List<LearningDocumentChunk> Chunks { get; set; } = [];
}

public sealed class LearningDocumentChunk
{
    public Guid Id { get; set; }
    public Guid LearningDocumentId { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = "";
    public int TokenEstimate { get; set; }
    public int? PageFrom { get; set; }
    public int? PageTo { get; set; }
    public string? Section { get; set; }
    public int ParagraphFrom { get; set; }
    public int ParagraphTo { get; set; }
}

/// <summary>One processing attempt; kept for status history and restart recovery.</summary>
public sealed class LearningProcessingJob
{
    public Guid Id { get; set; }
    public Guid LearningDocumentId { get; set; }
    public LearningStatus Status { get; set; }
    public int Attempt { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class LearningConfiguration :
    IEntityTypeConfiguration<LearningDocument>, IEntityTypeConfiguration<LearningDocumentChunk>, IEntityTypeConfiguration<LearningProcessingJob>
{
    public void Configure(EntityTypeBuilder<LearningDocument> builder)
    {
        builder.ToTable("LearningDocuments");
        builder.HasIndex(d => d.DocumentId).IsUnique(); // a file is indexed once
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.Format).HasMaxLength(8);
        builder.Property(d => d.Error).HasMaxLength(500);
        builder.HasOne(d => d.Document).WithMany().HasForeignKey(d => d.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Chunks).WithOne().HasForeignKey(c => c.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<LearningDocumentChunk> builder)
    {
        builder.ToTable("LearningDocumentChunks");
        builder.HasIndex(c => new { c.LearningDocumentId, c.Ordinal }).IsUnique();
        builder.Property(c => c.Section).HasMaxLength(200);
    }

    public void Configure(EntityTypeBuilder<LearningProcessingJob> builder)
    {
        builder.ToTable("LearningProcessingJobs");
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(j => j.Error).HasMaxLength(500);
        builder.HasIndex(j => j.Status);
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(j => j.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LearningOptions
{
    public const string Section = "Learning";
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public int MaxPages { get; set; } = 1000;
    public long MaxCharacters { get; set; } = 5_000_000;
    public int MaxConcurrentJobs { get; set; } = 1;
    public int JobTimeoutSeconds { get; set; } = 300;
    public int MaxAttempts { get; set; } = 2;
    public int TargetTokens { get; set; } = 650;
    public int MinTokens { get; set; } = 500;
    public int MaxTokens { get; set; } = 800;
    public int OverlapTokens { get; set; } = 100;

    public RetrievalOptions Retrieval { get; set; } = new();
    public ChunkingOptions Chunking => new(TargetTokens, MinTokens, MaxTokens, OverlapTokens);
    public TextExtractionLimits Limits => new(MaxPages, MaxCharacters);
}

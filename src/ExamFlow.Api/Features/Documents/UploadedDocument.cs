using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Features.Documents;

public class UploadedDocument : IAuditable
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public bool ShareWithEveryone { get; set; }
    public long FileSizeBytes { get; set; }
    public byte[] Sha256 { get; set; } = [];
    public int? PageCount { get; set; }
    public DocumentStatus Status { get; set; }
    public ProcessingReport? ProcessingReport { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid UploadedByUserId { get; set; }
    public AppUser UploadedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public List<Question> Questions { get; set; } = [];
}

public enum DocumentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}

public class ProcessingReport
{
    public int QuestionsFound { get; set; }
    public int AutoDetected { get; set; }
    public int Unverified { get; set; }
    public int DuplicatesSkipped { get; set; }
    public List<string> Warnings { get; set; } = [];
}

using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;

namespace ExamFlow.Api.Features.Questions;

public class Question : IAuditable
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public UploadedDocument Document { get; set; } = null!;
    public int SourceNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public byte[] ContentHash { get; set; } = [];
    public VerificationStatus VerificationStatus { get; set; }
    public string? ParseWarning { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<AnswerOption> Options { get; set; } = [];
}

public class AnswerOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public byte Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public enum VerificationStatus
{
    Unverified,
    AutoDetected,
    Verified,
}

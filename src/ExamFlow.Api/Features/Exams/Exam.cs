using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Features.Exams;

public class Exam
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public short QuestionCount { get; set; }
    public RevealMode RevealMode { get; set; }

    /// <summary>Document the questions were drawn from (one item). Older exams may hold none (= all documents).</summary>
    public List<Guid> SourceDocumentIds { get; set; } = [];

    /// <summary>Inclusive range of original PDF question numbers (<c>Question.SourceNumber</c>); null on older exams.</summary>
    public int? RangeFrom { get; set; }
    public int? RangeTo { get; set; }

    public ExamStatus Status { get; set; }
    public CompletionReason? CompletionReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public short? CorrectCount { get; set; }
    public short? IncorrectCount { get; set; }
    public short? UnansweredCount { get; set; }
    public short? UngradedCount { get; set; }
    public decimal? ScorePercent { get; set; }
    public List<ExamQuestion> Questions { get; set; } = [];
}

/// <summary>A question frozen into an exam: text and shuffled options are copied, so later bank edits do not change it.</summary>
public class ExamQuestion
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid? QuestionId { get; set; }
    public Question? Question { get; set; }
    public short Position { get; set; }
    public QuestionSnapshot Snapshot { get; set; } = new();

    /// <summary>Never sent to the client before the reveal rules allow it. Null = answer unknown, not graded.</summary>
    public Guid? CorrectOptionId { get; set; }

    public Guid? SelectedOptionId { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public ExamOutcome? Outcome { get; set; }
}

public class QuestionSnapshot
{
    public string Text { get; set; } = string.Empty;
    public List<OptionSnapshot> Options { get; set; } = [];

    /// <summary>Images frozen with the question; missing (null) in exams created before visuals existed.</summary>
    public List<VisualSnapshot>? Visuals { get; set; } = [];
}

/// <summary>An image of a frozen question. <see cref="StorageKey"/> never leaves the server.</summary>
public class VisualSnapshot
{
    public Guid VisualId { get; set; }

    /// <summary>The option (<see cref="OptionSnapshot.OptionId"/>) the image belongs to; null = question body.</summary>
    public Guid? OptionId { get; set; }
    public string? StorageKey { get; set; }
    public string? ContentType { get; set; }
    public int PageNumber { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class OptionSnapshot
{
    // Not named "Id": EF would treat that as a key, which JSON-mapped owned types cannot have.
    public Guid OptionId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public enum RevealMode
{
    /// <summary>The correct answer is shown right after answering; the answer can still be changed until the exam ends.</summary>
    Immediate,

    /// <summary>Correct answers are shown only on the result page.</summary>
    AtEnd,
}

public enum ExamStatus
{
    InProgress,
    Completed,
}

public enum CompletionReason
{
    Submitted,
    TimeExpired,
}

public enum ExamOutcome
{
    Correct,
    Incorrect,
    Unanswered,
    Ungraded,
}

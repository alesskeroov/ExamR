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

    /// <summary>Documents the questions were drawn from; empty means all documents.</summary>
    public List<Guid> SourceDocumentIds { get; set; } = [];

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
    /// <summary>The correct answer is shown right after answering; the answer is then locked.</summary>
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

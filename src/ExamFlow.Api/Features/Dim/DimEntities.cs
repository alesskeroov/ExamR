using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Features.Dim;

/// <summary>
/// A bank question approved by an admin for DİM tests. The question itself (text, options, correct answer) stays in
/// <see cref="Question"/>, uploaded through the normal PDF/DOCX pipeline; this row only classifies it.
/// </summary>
public class DimBankQuestion
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public DimTrack Track { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
}

public class DimTest
{
    public Guid Id { get; set; }
    public DimTrack Track { get; set; }
    public DimTestKind Kind { get; set; }

    /// <summary>Blok specialty group (<see cref="DimCatalog.Groups"/>) of a full test; null otherwise.</summary>
    public string? GroupCode { get; set; }

    public string Title { get; set; } = string.Empty;
    public short DurationMinutes { get; set; }

    /// <summary>Attempts allowed per user; null = unlimited.</summary>
    public short? AttemptLimit { get; set; }

    /// <summary>Optional window in which an attempt may start; null bounds = always open.</summary>
    public DateTimeOffset? AvailableFrom { get; set; }
    public DateTimeOffset? AvailableTo { get; set; }

    public bool IsPublished { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<DimTestSection> Sections { get; set; } = [];
}

/// <summary>
/// One subject of a test: fixed questions (a ready-made test), or <see cref="RandomCount"/> questions drawn from the
/// bank for every attempt.
/// </summary>
public class DimTestSection
{
    public string Subject { get; set; } = string.Empty;
    public List<Guid> QuestionIds { get; set; } = [];
    public int RandomCount { get; set; }

    /// <summary>When set, random questions are drawn only from bank questions of this uploaded document.</summary>
    public Guid? SourceDocumentId { get; set; }

    public int QuestionCount => QuestionIds.Count > 0 ? QuestionIds.Count : RandomCount;
}

/// <summary>Links a user's attempt (a normal <see cref="Exam"/>, so the exam screen is reused) to its DİM test.</summary>
public class DimAttempt
{
    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public Guid TestId { get; set; }
    public DimTest Test { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Subject and topic of every exam position, frozen at start like the questions themselves.</summary>
    public List<DimAttemptItem> Items { get; set; } = [];
}

public class DimAttemptItem
{
    public int Position { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Topic { get; set; }
}

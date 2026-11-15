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

/// <summary>
/// A listening recording (e.g. the English section) of a DİM source document or manual set. It belongs to the
/// questions whose number lies in [<see cref="QuestionFrom"/>, <see cref="QuestionTo"/>]; when a test is started the
/// link is frozen into each question's snapshot, so the student can play it during the exam.
/// </summary>
public class DimAudio
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public ExamFlow.Api.Features.Documents.UploadedDocument Document { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public int QuestionFrom { get; set; }
    public int QuestionTo { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>How many times a student may play it during an exam, like the real exam (2); null = no limit.</summary>
    public int? MaxPlays { get; set; }

    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool Covers(int number) => number >= QuestionFrom && number <= QuestionTo;
}

/// <summary>How many times the student started a recording in one exam (only counted while the exam runs).</summary>
public class DimAudioPlay
{
    public Guid ExamId { get; set; }
    public Guid AudioId { get; set; }
    public int Count { get; set; }
}

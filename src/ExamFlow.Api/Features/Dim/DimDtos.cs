using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Features.Dim;

// ---- Catalog ----

public sealed record DimSubjectDto(string Code, string Name, int Closed, int Coded, int Written, bool WrongPenalty);

public sealed record DimGroupSubjectDto(string Code, string Name, decimal Coefficient);

public sealed record DimGroupDto(string Code, string Name, IReadOnlyList<DimGroupSubjectDto> Subjects);

public sealed record DimCatalogDto(IReadOnlyList<DimSubjectDto> Buraxilis, IReadOnlyList<DimSubjectDto> Blok, IReadOnlyList<DimGroupDto> Groups);

// ---- Admin: bank ----

public sealed record DimOptionDto(Guid Id, string Label, string Text, bool IsCorrect);

public sealed record DimBankInfoDto(DimTrack Track, string Subject, string? Topic);

/// <param name="CodedAnswer">Accepted answers of an open coded question (no options), e.g. "2,5; 5/2".</param>
public sealed record DimSourceQuestionDto(
    Guid Id, int SourceNumber, string Text, IReadOnlyList<DimOptionDto> Options, VerificationStatus VerificationStatus,
    string? ParseWarning, DimBankInfoDto? Bank, string? CodedAnswer = null);

public sealed class DimAnswerKeyRequest
{
    /// <summary>For example "1-A 2-C 3-B" or one pair per line.</summary>
    [Required, StringLength(20_000, MinimumLength = 2)]
    public string Text { get; init; } = string.Empty;
}

public sealed record DimAnswerKeyResultDto(int Applied, IReadOnlyList<int> NotFound, IReadOnlyList<int> Invalid, IReadOnlyList<int> Conflicts);

public sealed class DimApproveRequest
{
    public DimTrack Track { get; init; }

    [Required, StringLength(10)]
    public string Subject { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Topic { get; init; }

    [Required, MinLength(1), MaxLength(500)]
    public List<Guid> QuestionIds { get; init; } = [];
}

public sealed record DimRejectedDto(Guid QuestionId, string Reason);

public sealed record DimApproveResultDto(int Approved, IReadOnlyList<DimRejectedDto> Rejected);

public sealed class DimApproveDocumentRequest
{
    public DimTrack Track { get; init; }

    [Required, StringLength(10)]
    public string Subject { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Topic { get; init; }
}

public sealed record DimEditOptionRequest(Guid Id, [property: Required, StringLength(1000)] string Text);

public sealed class DimEditQuestionRequest
{
    [Required, StringLength(4000)]
    public string Text { get; init; } = string.Empty;

    [Required]
    public List<DimEditOptionRequest> Options { get; init; } = [];

    /// <summary>Closed questions: the option that is correct.</summary>
    public Guid? CorrectOptionId { get; init; }

    /// <summary>Questions without options: accepted answers, e.g. "2,5" or "2,5; 5/2".</summary>
    [StringLength(CodedAnswers.MaxKeyLength)]
    public string? CodedAnswer { get; init; }
}

public sealed class DimCreateSetRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;
}

public sealed class DimCreateQuestionRequest
{
    public Guid DocumentId { get; init; }
    public DimTrack Track { get; init; }

    [Required, StringLength(10)]
    public string Subject { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Topic { get; init; }

    [Required, StringLength(4000)]
    public string Text { get; init; } = string.Empty;

    /// <summary>Empty = open coded question; otherwise the 2–5 option texts in order (A, B, …).</summary>
    [MaxLength(DimAdminEndpoints.MaxOptions)]
    public List<string> Options { get; init; } = [];

    /// <summary>Index of the correct option (A = 0) of a closed question.</summary>
    [Range(0, DimAdminEndpoints.MaxOptions - 1)]
    public int? CorrectIndex { get; init; }

    /// <summary>Accepted answers of a coded question, e.g. "2,5" or "2,5; 5/2".</summary>
    [StringLength(CodedAnswers.MaxKeyLength)]
    public string? CodedAnswer { get; init; }
}

public sealed record DimDocumentBankDto(DimTrack Track, string Subject, string Name, int Count);

/// <summary>An uploaded document with how many of its questions fit DİM tests and how many are already in the bank.</summary>
/// <param name="IsManual">A set of questions typed in by hand (no file), see <see cref="DimAdminEndpoints.ManualSetSuffix"/>.</param>
public sealed record DimSourceDocumentDto(
    Guid Id, string OriginalFileName, DateTimeOffset CreatedAt, int QuestionCount, int Valid, IReadOnlyList<DimDocumentBankDto> Bank,
    bool IsManual = false);

public sealed record DimBankStatDto(DimTrack Track, string Subject, string Name, int Count, int Topics);

// ---- Admin: tests ----

public sealed class DimSectionRequest
{
    [Required, StringLength(10)]
    public string Subject { get; init; } = string.Empty;

    /// <summary>Fixed questions (a ready-made test). Leave empty to use <see cref="RandomCount"/>.</summary>
    public List<Guid> QuestionIds { get; init; } = [];

    [Range(0, ExamOptions.HardMaxQuestions)]
    public int RandomCount { get; init; }

    /// <summary>true = draw new random questions for every attempt; false = draw once now and keep them fixed.</summary>
    public bool DrawPerAttempt { get; init; }

    /// <summary>Restrict the section to bank questions of one uploaded document.</summary>
    public Guid? SourceDocumentId { get; init; }
}

public sealed class DimTestRequest
{
    public DimTrack Track { get; init; }
    public DimTestKind Kind { get; init; }

    [StringLength(10)]
    public string? GroupCode { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    [Range(1, ExamOptions.MaxDurationMinutes)]
    public int DurationMinutes { get; init; }

    [Range(1, 100)]
    public int? AttemptLimit { get; init; }

    public DateTimeOffset? AvailableFrom { get; init; }
    public DateTimeOffset? AvailableTo { get; init; }
    public bool IsPublished { get; init; }

    [Required, MinLength(1), MaxLength(10)]
    public List<DimSectionRequest> Sections { get; init; } = [];
}

public sealed class DimTestSettingsRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    [Range(1, ExamOptions.MaxDurationMinutes)]
    public int DurationMinutes { get; init; }

    [Range(1, 100)]
    public int? AttemptLimit { get; init; }

    public DateTimeOffset? AvailableFrom { get; init; }
    public DateTimeOffset? AvailableTo { get; init; }
    public bool IsPublished { get; init; }
}

public sealed record DimSectionDto(string Subject, string Name, int QuestionCount, bool Random, Guid? SourceDocumentId = null);

public sealed record DimAdminTestDto(
    Guid Id, DimTrack Track, DimTestKind Kind, string? GroupCode, string Title, int DurationMinutes, int? AttemptLimit,
    DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableTo, bool IsPublished, DateTimeOffset CreatedAt,
    IReadOnlyList<DimSectionDto> Sections, int Attempts);

// ---- User ----

public sealed record DimLastAttemptDto(Guid ExamId, ExamStatus Status, DateTimeOffset StartedAt, decimal? ScorePercent);

public sealed record DimTestDto(
    Guid Id, DimTrack Track, DimTestKind Kind, string? GroupCode, string? GroupName, string Title, int DurationMinutes,
    int QuestionCount, IReadOnlyList<DimSectionDto> Sections, int? AttemptLimit, int AttemptsUsed,
    DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableTo, bool IsOpen, DimLastAttemptDto? LastAttempt);

/// <summary>Body of the start request; empty body = answers revealed at the end, like the real exam.</summary>
public sealed class DimStartRequest
{
    public RevealMode RevealMode { get; init; } = RevealMode.AtEnd;
}

public sealed record DimStartedDto(Guid ExamId);

public sealed record DimReportDto(
    Guid ExamId, Guid TestId, string Title, DimTrack Track, DimTestKind Kind, string? GroupCode, string? GroupName,
    int Correct, int Incorrect, int Unanswered, int Ungraded, decimal? ScorePercent, DimScore Score, string Notice,
    int? AttemptLimit, int AttemptsUsed, bool CanStart);

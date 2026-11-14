using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Features.Exams;

public sealed class CreateExamRequest
{
    [Range(1, ExamOptions.HardMaxQuestions)]
    public int QuestionCount { get; init; }

    [Required]
    public Guid? DocumentId { get; init; }

    /// <summary>First and last original question number of the PDF to draw from (inclusive).</summary>
    [Range(1, 100_000)]
    public int RangeFrom { get; init; }

    [Range(1, 100_000)]
    public int RangeTo { get; init; }

    public RevealMode RevealMode { get; init; } = RevealMode.AtEnd;

    public ExamPool Pool { get; init; } = ExamPool.All;

    /// <summary>Time limit in minutes; null = <see cref="ExamOptions.DefaultDurationMinutes"/>.</summary>
    [Range(1, ExamOptions.MaxDurationMinutes)]
    public int? DurationMinutes { get; init; }
}

/// <summary>Which questions of the range an exam draws from.</summary>
public enum ExamPool
{
    All,

    /// <summary>Only questions the user last answered wrong or left blank (see <see cref="ExamEngine.Mistakes"/>).</summary>
    Mistakes,
}

public sealed class AnswerRequest
{
    public Guid? SelectedOptionId { get; init; }

    /// <summary>The typed number of a coded question; null or blank clears the answer.</summary>
    [StringLength(CodedAnswers.MaxAnswerLength)]
    public string? AnswerText { get; init; }
}

public sealed record ExamOptionDto(Guid Id, string Label, string Text);

/// <summary>Correct answer shown during the exam; only in Immediate mode and only after the user revealed it.</summary>
/// <param name="CorrectAnswer">Coded questions: the accepted answers.</param>
/// <param name="IsCorrect">Coded questions: whether the typed number matches (the client cannot compare numbers itself).</param>
public sealed record AnswerRevealDto(Guid? CorrectOptionId, bool IsGradable, string? CorrectAnswer = null, bool? IsCorrect = null);

public sealed record ExamSessionQuestionDto(
    Guid ExamQuestionId,
    int Position,
    string Text,
    IReadOnlyList<ExamOptionDto> Options,
    Guid? SelectedOptionId,
    AnswerRevealDto? Reveal,
    IReadOnlyList<VisualDto> Visuals,
    bool Locked,
    bool IsCoded = false,
    string? AnswerText = null);

public sealed record ExamSessionDto(
    Guid Id,
    ExamStatus Status,
    RevealMode RevealMode,
    int QuestionCount,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ServerNow,
    IReadOnlyList<ExamSessionQuestionDto> Questions);

public sealed record AnswerResultDto(Guid ExamQuestionId, Guid? SelectedOptionId, AnswerRevealDto? Reveal, bool Locked, string? AnswerText = null);

public sealed record ExamResultQuestionDto(
    int Position,
    string Text,
    IReadOnlyList<ExamOptionDto> Options,
    Guid? SelectedOptionId,
    Guid? CorrectOptionId,
    ExamOutcome Outcome,
    IReadOnlyList<VisualDto> Visuals,
    bool IsCoded = false,
    string? AnswerText = null,
    string? CorrectAnswer = null);

/// <summary>Where the exam's questions came from. DocumentName is null when the PDF was deleted.</summary>
public sealed record ExamSourceDto(Guid? DocumentId, string? DocumentName, int? RangeFrom, int? RangeTo);

public sealed record ExamResultDto(
    Guid Id,
    int QuestionCount,
    RevealMode RevealMode,
    int TimeLimitMinutes,
    ExamSourceDto Source,
    CompletionReason CompletionReason,
    int Correct,
    int Incorrect,
    int Unanswered,
    int Ungraded,
    decimal? ScorePercent,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int DurationSeconds,
    IReadOnlyList<ExamResultQuestionDto> Questions);

public sealed record ExamSummaryDto(
    Guid Id,
    ExamStatus Status,
    int QuestionCount,
    RevealMode RevealMode,
    ExamSourceDto Source,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CompletedAt,
    CompletionReason? CompletionReason,
    int? Correct,
    int? Incorrect,
    int? Unanswered,
    int? Ungraded,
    decimal? ScorePercent);

/// <summary>
/// Questions available in a PDF range; FirstNumber/LastNumber are the PDF's own question numbers.
/// Mistakes = questions of the range the user last answered wrong or left blank.
/// </summary>
public sealed record ExamAvailabilityDto(
    int Available, int Ungraded, int Mistakes, int? FirstNumber, int? LastNumber, int MaxQuestions, int DefaultDurationMinutes);

public sealed record ActiveExamDto(Guid Id);

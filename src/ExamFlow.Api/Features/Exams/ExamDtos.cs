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
}

public sealed class AnswerRequest
{
    public Guid? SelectedOptionId { get; init; }
}

public sealed record ExamOptionDto(Guid Id, string Label, string Text);

/// <summary>Correct answer shown during the exam; only in Immediate mode and only after answering.</summary>
public sealed record AnswerRevealDto(Guid? CorrectOptionId, bool IsGradable);

public sealed record ExamSessionQuestionDto(
    Guid ExamQuestionId,
    int Position,
    string Text,
    IReadOnlyList<ExamOptionDto> Options,
    Guid? SelectedOptionId,
    AnswerRevealDto? Reveal,
    IReadOnlyList<VisualDto> Visuals);

public sealed record ExamSessionDto(
    Guid Id,
    ExamStatus Status,
    RevealMode RevealMode,
    int QuestionCount,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ServerNow,
    IReadOnlyList<ExamSessionQuestionDto> Questions);

public sealed record AnswerResultDto(Guid ExamQuestionId, Guid? SelectedOptionId, AnswerRevealDto? Reveal);

public sealed record ExamResultQuestionDto(
    int Position,
    string Text,
    IReadOnlyList<ExamOptionDto> Options,
    Guid? SelectedOptionId,
    Guid? CorrectOptionId,
    ExamOutcome Outcome,
    IReadOnlyList<VisualDto> Visuals);

/// <summary>Where the exam's questions came from. DocumentName is null when the PDF was deleted.</summary>
public sealed record ExamSourceDto(Guid? DocumentId, string? DocumentName, int? RangeFrom, int? RangeTo);

public sealed record ExamResultDto(
    Guid Id,
    int QuestionCount,
    RevealMode RevealMode,
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

/// <summary>Questions available in a PDF range; FirstNumber/LastNumber are the PDF's own question numbers.</summary>
public sealed record ExamAvailabilityDto(
    int Available, int Ungraded, int? FirstNumber, int? LastNumber, int MaxQuestions, double MinutesPerQuestion);

public sealed record ActiveExamDto(Guid Id);

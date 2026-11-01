using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Exams;

public sealed class CreateExamRequest
{
    [Range(1, ExamOptions.HardMaxQuestions)]
    public int QuestionCount { get; init; }

    /// <summary>Empty or null: questions from all documents.</summary>
    [MaxLength(20)]
    public List<Guid>? DocumentIds { get; init; }

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
    AnswerRevealDto? Reveal);

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
    ExamOutcome Outcome);

public sealed record ExamResultDto(
    Guid Id,
    int QuestionCount,
    RevealMode RevealMode,
    IReadOnlyList<Guid> DocumentIds,
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
    int SourceDocumentCount,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CompletedAt,
    CompletionReason? CompletionReason,
    int? Correct,
    int? Incorrect,
    int? Unanswered,
    int? Ungraded,
    decimal? ScorePercent);

public sealed record ExamAvailabilityDto(int Available, int Ungraded, int MaxQuestions, double MinutesPerQuestion);

public sealed record ActiveExamDto(Guid Id);

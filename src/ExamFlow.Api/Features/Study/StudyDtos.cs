using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Study;

public sealed record StudyMessageRequest(
    Guid DocumentId,
    [property: Required(ErrorMessage = "Mesaj tələb olunur."), StringLength(2000, ErrorMessage = "Mesaj 2000 simvoldan uzun ola bilməz.")] string Message,
    Guid? QuestionId = null,
    Guid? AttemptId = null);
public sealed record StudyAnswerRequest(
    [property: Required(ErrorMessage = "Cavab tələb olunur."), StringLength(16000, ErrorMessage = "Cavab 16000 simvoldan uzun ola bilməz.")] string Answer);
public sealed record StudySource(Guid Id, int Number, string Text, string Answer);
public sealed record StudyReply(string Text, Guid? QuestionId = null, int? SourceNumber = null,
    Guid? AttemptId = null, string[]? Options = null, int? Score = null, string[]? MissingConcepts = null, StudyCitation[]? Sources = null);
public sealed record StudyProgress(Guid QuestionId, int SourceNumber, string Text, int Attempts,
    int? Score, DateTimeOffset? DueAt);
public sealed record StudyDocumentDto(Guid Id, string OriginalFileName, int QuestionCount);

public sealed record StudyCitation(Guid QuestionId, int SourceNumber, string Excerpt);
public sealed record StudyHistoryTurn(string Message, StudyReply Reply, DateTimeOffset CreatedAt);
public sealed record StudyHistory(IReadOnlyList<StudyHistoryTurn> Turns, StudyReply? ActiveAttempt);
public sealed record StudyUsageTotals(int Requests, long InputTokens, long CachedInputTokens, long OutputTokens,
    decimal EstimatedCostUsd, int UncertainRequests);
public sealed record StudyUsage(int DailyLimit, int Remaining, DateTimeOffset ResetsAt, string Model,
    StudyUsageTotals Today, StudyUsageTotals Total, StudyUsageTotals? GlobalTotal);
public sealed record StudyChunk(string Id, Guid QuestionId, int Number, string Text);
public sealed record StudyAiOutput(string Text, bool Grounded, string[] SourceIds, string Prompt,
    string[] Options, int? CorrectIndex, string ExpectedAnswer, int? Score, string[] MissingConcepts);

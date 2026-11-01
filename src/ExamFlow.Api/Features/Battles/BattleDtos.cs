using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Exams;

namespace ExamFlow.Api.Features.Battles;

public sealed class CreateBattleRequest
{
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;
    [Required]
    public Guid? DocumentId { get; init; }

    [Range(1, 100_000)]
    public int RangeFrom { get; init; }

    [Range(1, 100_000)]
    public int RangeTo { get; init; }

    [Range(1, BattleService.MaxQuestions)]
    public int QuestionCount { get; init; }

    [Range(BattleService.MinSecondsPerQuestion, BattleService.MaxSecondsPerQuestion)]
    public int SecondsPerQuestion { get; init; } = 20;
}

public sealed record CreateBattleResponse(string Code);
public sealed record BattleLobbyDto(string Code, Guid HostUserId, string HostName, string DocumentName, int QuestionCount);
public sealed record MyBattleRoomDto(Guid Id, string Name, string Code, string DocumentName,
    int ParticipantCount, RoomStatus Status, bool IsHost, bool IsClosed, bool CanJoin);
public sealed record BattleInviteRosterDto(IReadOnlyList<Guid> ParticipantIds);

public sealed record BattleParticipantDto(
    Guid UserId,
    string DisplayName,
    bool IsHost,
    bool IsConnected,
    bool IsMuted,
    int Score,
    int CorrectCount,
    int Rank,
    int? LastPoints,
    bool? LastCorrect,
    bool HasAnswered,
    string? AvatarUrl = null);

/// <summary>The current question as players see it — no correct answer.</summary>
public sealed record BattleQuestionDto(
    int Index, int Total, string Text, IReadOnlyList<ExamOptionDto> Options, DateTimeOffset EndsAt, int SecondsPerQuestion);

/// <summary>Sent only after the question has closed.</summary>
public sealed record BattleRevealDto(int Index, Guid? CorrectOptionId);

/// <summary>
/// Full room snapshot broadcast on every change. <c>MyUserId</c>, <c>MyAnswerOptionId</c> and <c>RecentMessages</c>
/// are filled only in the reply to the caller's own JoinRoom.
/// </summary>
public sealed record BattleStateDto(
    string Code,
    Guid HostUserId,
    BattleStatus Status,
    BattleSettings Settings,
    IReadOnlyList<BattleParticipantDto> Participants,
    BattleQuestionDto? Question,
    BattleRevealDto? Reveal,
    int AnsweredCount,
    DateTimeOffset ServerNow,
    Guid? MyAnswerOptionId,
    IReadOnlyList<ChatMessageDto>? RecentMessages,
    Guid? MyUserId,
    string Name);

public sealed record ChatMessageDto(Guid Id, Guid UserId, string DisplayName, string Text, DateTimeOffset SentAt, string? AvatarUrl = null);

public sealed record BattleHistoryDto(
    Guid BattleId,
    string RoomCode,
    DateTimeOffset PlayedAt,
    ExamSourceDto Source,
    int QuestionCount,
    int ParticipantCount,
    int Rank,
    int Score,
    int CorrectCount);

/// <summary>Strongly typed client methods (names are the SignalR event names the React app listens to).</summary>
public interface IBattleClient
{
    Task RoomUpdated(BattleStateDto state);
    Task ChatMessage(ChatMessageDto message);
    Task Kicked(string message);
    Task RoomClosed(string message);
}

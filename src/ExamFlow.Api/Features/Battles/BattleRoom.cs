using ExamFlow.Api.Features.Exams;

namespace ExamFlow.Api.Features.Battles;

public enum BattleStatus
{
    Lobby,
    Question,
    Reveal,
    Finished,
    Closed,
}

public sealed record BattleSettings(
    Guid DocumentId, string DocumentName, int RangeFrom, int RangeTo, int QuestionCount, int SecondsPerQuestion);

/// <summary>
/// In-memory state of one live battle (single-server MVP; nothing here survives a restart).
/// Every read or write goes through <see cref="Gate"/>.
/// </summary>
public sealed class BattleRoom(string code, Guid hostUserId, BattleSettings settings, DateTimeOffset createdAt)
{
    public const int MaxParticipants = 10;
    public const int MinParticipants = 2;
    public const int MaxRecentMessages = 50;

    public SemaphoreSlim Gate { get; } = new(1, 1);
    public Guid BattleId { get; } = Guid.NewGuid();
    public string Code { get; } = code;
    public string Group => $"battle:{Code}";
    public Guid HostUserId { get; } = hostUserId;
    public BattleSettings Settings { get; } = settings;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public DateTimeOffset LastActivityAt { get; set; } = createdAt;

    public BattleStatus Status { get; set; } = BattleStatus.Lobby;
    public Dictionary<Guid, BattleParticipant> Participants { get; } = [];
    public HashSet<Guid> Kicked { get; } = [];

    /// <summary>Questions frozen at start; <c>CorrectOptionId</c> never leaves the server before the reveal.</summary>
    public List<ExamQuestion> Questions { get; set; } = [];
    public int CurrentIndex { get; set; } = -1;
    public DateTimeOffset QuestionStartedAt { get; set; }
    public DateTimeOffset Deadline { get; set; }

    /// <summary>Completed to end the current phase early (everyone answered, or the host skipped).</summary>
    public TaskCompletionSource Advance { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LinkedList<ChatMessageDto> RecentMessages { get; } = new();

    public ExamQuestion? CurrentQuestion => CurrentIndex >= 0 && CurrentIndex < Questions.Count ? Questions[CurrentIndex] : null;
    public bool IsActive => Status is BattleStatus.Lobby or BattleStatus.Question or BattleStatus.Reveal;
    public bool HasConnections => Participants.Values.Any(p => p.IsConnected);
}

public sealed class BattleParticipant(Guid userId, string displayName, DateTimeOffset joinedAt)
{
    public Guid UserId { get; } = userId;
    public string DisplayName { get; } = displayName;
    public DateTimeOffset JoinedAt { get; } = joinedAt;
    public HashSet<string> Connections { get; } = [];
    public bool IsConnected => Connections.Count > 0;
    public bool IsMuted { get; set; }

    public int Score { get; set; }
    public int CorrectCount { get; set; }

    /// <summary>Sum of answer times of correct answers; tie-breaker (faster wins).</summary>
    public long CorrectTimeMs { get; set; }

    public Guid? CurrentAnswer { get; set; }
    public TimeSpan CurrentElapsed { get; set; }
    public int? LastPoints { get; set; }
    public bool? LastCorrect { get; set; }

    /// <summary>Send times of recent chat messages, for the anti-spam limit.</summary>
    public Queue<DateTimeOffset> RecentChat { get; } = new();
}

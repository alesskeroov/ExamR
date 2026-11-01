namespace ExamFlow.Api.Features.Battles;

public enum RoomStatus { Waiting, InProgress, Completed }

/// <summary>Durable room identity and recovery snapshot; connection IDs never belong in storage.</summary>
public class StoredBattleRoom
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public Guid HostUserId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public int RangeFrom { get; set; }
    public int RangeTo { get; set; }
    public int QuestionCount { get; set; }
    public int SecondsPerQuestion { get; set; }
    public RoomStatus Status { get; set; }
    public bool IsClosed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string StateJson { get; set; } = string.Empty;
    public byte[] Version { get; set; } = [];
    public List<StoredBattleMember> Members { get; set; } = [];
}

public class StoredBattleMember
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public bool IsKicked { get; set; }
}

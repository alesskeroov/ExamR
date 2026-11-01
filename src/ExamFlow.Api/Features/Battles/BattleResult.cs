using ExamFlow.Api.Features.Auth;

namespace ExamFlow.Api.Features.Battles;

/// <summary>One participant's final result in one battle — the only battle data that is persisted.</summary>
public class BattleResult
{
    public Guid Id { get; set; }

    /// <summary>Groups the rows of the same battle.</summary>
    public Guid BattleId { get; set; }

    public string RoomCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Source PDF; no foreign key so results survive deleting the PDF.</summary>
    public Guid DocumentId { get; set; }

    public int RangeFrom { get; set; }
    public int RangeTo { get; set; }
    public int QuestionCount { get; set; }
    public int ParticipantCount { get; set; }
    public int Rank { get; set; }
    public int Score { get; set; }
    public int CorrectCount { get; set; }
    public DateTimeOffset PlayedAt { get; set; }
}

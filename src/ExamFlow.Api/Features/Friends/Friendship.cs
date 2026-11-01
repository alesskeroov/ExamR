namespace ExamFlow.Api.Features.Friends;

public class Friendship
{
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public Guid RequestedById { get; set; }
    public bool IsAccepted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class UserBlock
{
    public Guid UserId { get; set; }
    public Guid BlockedUserId { get; set; }
}

public class SocialNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? RoomCode { get; set; }
    public string? HostName { get; set; }
    public string? DocumentName { get; set; }
    public int? QuestionCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class SocialOptions
{
    public int InvitationMinutes { get; set; } = 5;
}

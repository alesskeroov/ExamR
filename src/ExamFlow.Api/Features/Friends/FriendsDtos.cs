using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Friends;

public sealed record FriendDto(Guid Id, string DisplayName, bool IsOnline, string? AvatarUrl = null);
public sealed record SearchUserDto(Guid Id, string DisplayName, string Relationship, string? AvatarUrl = null);
public sealed record FriendsDto(IReadOnlyList<FriendDto> Friends, IReadOnlyList<FriendDto> Incoming,
    IReadOnlyList<FriendDto> Outgoing, IReadOnlyList<FriendDto> Blocked);
public sealed record NotificationDto(Guid Id, Guid ActorId, string ActorName, string Kind, string Status,
    string? RoomCode, string? HostName, string? DocumentName, int? QuestionCount,
    DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool IsRead);
public sealed record NotificationsDto(int UnreadCount, IReadOnlyList<NotificationDto> Items);

public sealed class InviteFriendRequest
{
    [Required, RegularExpression("^[A-Za-z0-9]{6}$")]
    public string RoomCode { get; init; } = string.Empty;
}

public sealed record AcceptedInvitationDto(string Code);
public sealed record RoomFriendDto(Guid Id, string DisplayName, bool IsOnline, bool IsInRoom, bool InvitationSent, string? AvatarUrl = null);

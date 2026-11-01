using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Friends;

public sealed class SocialPresence
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public bool IsOnline(Guid userId)
    {
        lock (_gate) return _connections.ContainsKey(userId);
    }

    public void Add(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                _connections[userId] = connections = [];
            connections.Add(connectionId);
        }
    }

    public void Remove(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(userId, out var connections) && connections.Remove(connectionId) && connections.Count == 0)
                _connections.Remove(userId);
        }
    }
}

public sealed class SocialEvents(IHubContext<SocialHub> hub)
{
    public void QueueGroupAdded(AppDbContext db, Guid userId, Guid actorId, string groupName, DateTimeOffset createdAt) =>
        db.SocialNotifications.Add(new SocialNotification { UserId = userId, ActorId = actorId, Kind = "GroupAdded",
            Status = "Accepted", DocumentName = groupName, CreatedAt = createdAt });

    public static string Group(Guid userId) => $"social:{userId}";
    public Task ChangedAsync(Guid userId, CancellationToken ct = default) =>
        hub.Clients.Group(Group(userId)).SendAsync("SocialChanged", ct);
}

[Authorize]
public sealed class SocialHub(SocialPresence presence, AppDbContext db, SocialEvents events) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var id = Context.User!.GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, SocialEvents.Group(id), Context.ConnectionAborted);
        presence.Add(id, Context.ConnectionId);
        await NotifyFriendsAsync(id);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var id = Context.User!.GetUserId();
        presence.Remove(id, Context.ConnectionId);
        await NotifyFriendsAsync(id);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyFriendsAsync(Guid id)
    {
        var ids = await db.Friendships.AsNoTracking()
            .Where(f => f.IsAccepted && (f.UserLowId == id || f.UserHighId == id))
            .Select(f => f.UserLowId == id ? f.UserHighId : f.UserLowId).ToListAsync(CancellationToken.None);
        await Task.WhenAll(ids.Select(friendId => events.ChangedAsync(friendId)));
    }
}

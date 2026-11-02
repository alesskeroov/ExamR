using System.Data;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Battles;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Friends;

public static class FriendsEndpoints
{
    private sealed record OnlineRoomInvitation;
    public static void MapFriendsEndpoints(this RouteGroupBuilder api)
    {
        var friends = api.MapGroup("/friends").RequireAuthorization();
        friends.MapGet("/", List);
        friends.MapGet("/search", Search);
        friends.MapGet("/lobbies", Lobbies);
        friends.MapGet("/room/{code}", RoomFriends);
        friends.MapPost("/{targetId:guid}/{action}", Change).RequireRateLimiting(AuthenticationSetup.SocialRateLimit);
        friends.MapPost("/{targetId:guid}/invite", Invite).Validate<InviteFriendRequest>()
            .RequireRateLimiting(AuthenticationSetup.SocialRateLimit);
        friends.MapPost("/{targetId:guid}/room-invite", Invite).Validate<InviteFriendRequest>()
            .WithMetadata(new OnlineRoomInvitation()).RequireRateLimiting(AuthenticationSetup.SocialRateLimit);

        var notifications = api.MapGroup("/notifications").RequireAuthorization();
        notifications.MapGet("/", Notifications);
        notifications.MapDelete("/", DeleteNotifications);
        notifications.MapDelete("/{id:guid}", DeleteNotification);
        notifications.MapPost("/read", ReadAll);
        notifications.MapPost("/{id:guid}/read", Read);
        notifications.MapPost("/{id:guid}/{action}", Respond);
    }

    private static IQueryable<Friendship> Pair(AppDbContext db, Guid a, Guid b) =>
        db.Friendships.Where(f => (f.UserLowId == a && f.UserHighId == b) || (f.UserLowId == b && f.UserHighId == a));

    private static Task<bool> Blocked(AppDbContext db, Guid a, Guid b, CancellationToken ct) =>
        db.UserBlocks.AnyAsync(x => (x.UserId == a && x.BlockedUserId == b) || (x.UserId == b && x.BlockedUserId == a), ct);

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, SocialPresence presence, CancellationToken ct)
    {
        var id = user.GetUserId();
        var relations = await db.Friendships.AsNoTracking().Where(f => f.UserLowId == id || f.UserHighId == id)
            .Select(f => new { OtherId = f.UserLowId == id ? f.UserHighId : f.UserLowId, f.RequestedById, f.IsAccepted }).ToListAsync(ct);
        var blockedIds = await db.UserBlocks.AsNoTracking().Where(b => b.UserId == id).Select(b => b.BlockedUserId).ToListAsync(ct);
        var ids = relations.Select(r => r.OtherId).Concat(blockedIds).ToArray();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderBy(u => u.Nickname)
            .Select(u => new FriendDto(u.Id, u.Nickname, false, u.AvatarFileName == null ? null : $"/api/profile/avatar/{u.Id}?v={u.AvatarFileName}")).ToListAsync(ct);
        var accepted = relations.Where(r => r.IsAccepted).Select(r => r.OtherId).ToHashSet();
        var incoming = relations.Where(r => !r.IsAccepted && r.RequestedById != id).Select(r => r.OtherId).ToHashSet();
        var outgoing = relations.Where(r => !r.IsAccepted && r.RequestedById == id).Select(r => r.OtherId).ToHashSet();
        return TypedResults.Ok(new FriendsDto(
            people.Where(p => accepted.Contains(p.Id)).Select(p => p with { IsOnline = presence.IsOnline(p.Id) }).ToList(),
            people.Where(p => incoming.Contains(p.Id)).ToList(), people.Where(p => outgoing.Contains(p.Id)).ToList(),
            people.Where(p => blockedIds.Contains(p.Id)).ToList()));
    }

    private static async Task<IResult> Search(string? q, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var id = user.GetUserId();
        var term = q?.Trim() ?? string.Empty;
        if (term.Length is < 2 or > 100) return TypedResults.Ok(Array.Empty<SearchUserDto>());
        var normalized = term.ToUpperInvariant();
        var people = await db.Users.AsNoTracking()
            .Where(u => u.Id != id && u.NormalizedNickname.Contains(normalized))
            .Where(u => !db.UserBlocks.Any(b => (b.UserId == id && b.BlockedUserId == u.Id) || (b.UserId == u.Id && b.BlockedUserId == id)))
            .OrderBy(u => u.Nickname).ThenBy(u => u.Id).Take(20)
            .Select(u => new SearchUserDto(u.Id, u.Nickname,
                db.Friendships.Any(f => ((f.UserLowId == id && f.UserHighId == u.Id) || (f.UserLowId == u.Id && f.UserHighId == id)) && f.IsAccepted) ? "Friend"
                : db.Friendships.Any(f => ((f.UserLowId == id && f.UserHighId == u.Id) || (f.UserLowId == u.Id && f.UserHighId == id)) && f.RequestedById == id) ? "Outgoing"
                : db.Friendships.Any(f => (f.UserLowId == id && f.UserHighId == u.Id) || (f.UserLowId == u.Id && f.UserHighId == id)) ? "Incoming" : "None",
                u.AvatarFileName == null ? null : $"/api/profile/avatar/{u.Id}?v={u.AvatarFileName}"))
            .ToListAsync(ct);
        return TypedResults.Ok(people);
    }

    // Serializable transactions protect reverse requests and block/invite races. The EF execution
    // strategy is required because SQL Server retries are enabled in this application's DbContext.
    private static Task<IResult> Atomic(AppDbContext db, Func<Task<IResult>> action, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        });

    private static async Task<IResult> Change(Guid targetId, string action, ClaimsPrincipal user, AppDbContext db,
        SocialEvents events, TimeProvider clock, CancellationToken ct)
    {
        var id = user.GetUserId();
        if (id == targetId) return Problem("Bu əməliyyatı özünüzə tətbiq edə bilməzsiniz.");
        if (action is not ("request" or "accept" or "reject" or "remove" or "block" or "unblock")) return TypedResults.NotFound();
        var result = await Atomic(db, async () =>
        {
            if (!await db.Users.AnyAsync(u => u.Id == targetId, ct)) return TypedResults.NotFound();
            var relation = await Pair(db, id, targetId).SingleOrDefaultAsync(ct);
            if (action == "unblock")
            {
                await db.UserBlocks.Where(b => b.UserId == id && b.BlockedUserId == targetId).ExecuteDeleteAsync(ct);
                return TypedResults.NoContent();
            }
            if (action == "block")
            {
                if (!await db.UserBlocks.AnyAsync(b => b.UserId == id && b.BlockedUserId == targetId, ct))
                    db.UserBlocks.Add(new UserBlock { UserId = id, BlockedUserId = targetId });
                if (relation is not null) db.Friendships.Remove(relation);
                await ResolvePending(db, id, targetId, "Unavailable", clock.GetUtcNow(), ct);
                return TypedResults.NoContent();
            }
            if (await Blocked(db, id, targetId, ct)) return TypedResults.NotFound();
            if (action == "request")
            {
                if (relation is not null) return Problem("Dostluq və ya gözləyən sorğu artıq mövcuddur.", 409);
                db.Friendships.Add(new Friendship
                {
                    UserLowId = id.CompareTo(targetId) < 0 ? id : targetId,
                    UserHighId = id.CompareTo(targetId) < 0 ? targetId : id,
                    RequestedById = id, CreatedAt = clock.GetUtcNow(),
                });
                db.SocialNotifications.Add(new SocialNotification { UserId = targetId, ActorId = id, Kind = "FriendRequest", CreatedAt = clock.GetUtcNow() });
                return TypedResults.NoContent();
            }
            if (relation is null) return TypedResults.NotFound();
            if (action is "accept" or "reject")
            {
                if (relation.IsAccepted || relation.RequestedById == id) return TypedResults.NotFound();
                if (action == "accept")
                {
                    relation.IsAccepted = true;
                    db.SocialNotifications.Add(new SocialNotification { UserId = targetId, ActorId = id,
                        Kind = "FriendAccepted", Status = "Accepted", CreatedAt = clock.GetUtcNow() });
                }
                else db.Friendships.Remove(relation);
                await ResolvePending(db, id, targetId, action == "accept" ? "Accepted" : "Rejected", clock.GetUtcNow(), ct, requestsOnly: true);
            }
            else
            {
                db.Friendships.Remove(relation);
                await ResolvePending(db, id, targetId, "Unavailable", clock.GetUtcNow(), ct);
            }
            return TypedResults.NoContent();
        }, ct);
        await Task.WhenAll(events.ChangedAsync(id, ct), events.ChangedAsync(targetId, ct));
        return result;
    }

    private static Task<int> ResolvePending(AppDbContext db, Guid a, Guid b, string status, DateTimeOffset now,
        CancellationToken ct, bool requestsOnly = false) => db.SocialNotifications
        .Where(n => n.Status == "Pending" && ((n.UserId == a && n.ActorId == b) || (n.UserId == b && n.ActorId == a)))
        .Where(n => !requestsOnly || n.Kind == "FriendRequest")
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, status).SetProperty(n => n.ReadAt, now), ct);

    private static async Task<IResult> Lobbies(ClaimsPrincipal user, BattleService battles, CancellationToken ct) =>
        TypedResults.Ok(await battles.InvitableLobbiesAsync(user.GetUserId(), ct));

    private static async Task<IResult> RoomFriends(string code, string? q, ClaimsPrincipal user, AppDbContext db,
        BattleService battles, SocialPresence presence, TimeProvider clock, CancellationToken ct)
    {
        var id = user.GetUserId();
        var roster = await battles.InvitationRosterAsync(code, id, ct);
        if (roster is null) return TypedResults.NotFound();
        var term = q?.Trim() ?? string.Empty;
        if (term.Length > 100) return Problem("Axtarış mətni çox uzundur.");
        var normalized = term.ToUpperInvariant();
        var now = clock.GetUtcNow();
        var people = await db.Users.AsNoTracking()
            .Where(u => db.Friendships.Any(f => f.IsAccepted && ((f.UserLowId == id && f.UserHighId == u.Id) || (f.UserLowId == u.Id && f.UserHighId == id))))
            .Where(u => term == "" || u.NormalizedNickname.Contains(normalized))
            .OrderBy(u => u.Nickname).ThenBy(u => u.Id)
            .Select(u => new RoomFriendDto(u.Id, u.Nickname, false, false,
                db.SocialNotifications.Any(n => n.ActorId == id && n.UserId == u.Id && n.RoomCode == code
                    && n.Kind == "BattleInvite" && n.Status == "Pending" && n.ExpiresAt > now),
                u.AvatarFileName == null ? null : $"/api/profile/avatar/{u.Id}?v={u.AvatarFileName}"))
            .ToListAsync(ct);
        return TypedResults.Ok(people.Select(p => p with { IsOnline = presence.IsOnline(p.Id), IsInRoom = roster.ParticipantIds.Contains(p.Id) })
            .OrderByDescending(p => p.IsOnline).ThenBy(p => p.DisplayName).ToList());
    }

    private static async Task<IResult> Invite(Guid targetId, InviteFriendRequest request, ClaimsPrincipal user,
        AppDbContext db, BattleService battles, SocialEvents events, TimeProvider clock, IOptions<SocialOptions> options,
        SocialPresence presence, HttpContext context, CancellationToken ct)
    {
        var id = user.GetUserId();
        if (id == targetId) return Problem("Özünüzü dəvət edə bilməzsiniz.");
        var result = await Atomic(db, async () =>
        {
            if (!await Pair(db, id, targetId).AnyAsync(f => f.IsAccepted, ct) || await Blocked(db, id, targetId, ct))
                return TypedResults.NotFound();
            if (context.GetEndpoint()?.Metadata.GetMetadata<OnlineRoomInvitation>() is not null)
            {
                if (!presence.IsOnline(targetId)) return Problem("Dostunuz hazırda oflayndır.", 409);
                var roster = await battles.InvitationRosterAsync(request.RoomCode, id, ct);
                if (roster is null) return Problem("Otaq dəvətlər üçün açıq deyil.", 409);
                if (roster.ParticipantIds.Contains(targetId)) return Problem("Dostunuz artıq otaqdadır.", 409);
            }
            var lobby = await battles.InvitationLobbyAsync(request.RoomCode, id, requireMember: true, ct);
            if (lobby is null) return Problem("Dəvət üçün qoşulduğunuz açıq lobby seçin.", 409);
            if (await Blocked(db, lobby.HostUserId, targetId, ct)) return TypedResults.NotFound();
            if (await battles.InvitationLobbyAsync(lobby.Code, targetId, requireMember: false, ct) is null)
                return Problem("Bu istifadəçi otağa qoşula bilməz.", 409);
            var now = clock.GetUtcNow();
            if (await db.SocialNotifications.AnyAsync(n => n.UserId == targetId && n.ActorId == id && n.Kind == "BattleInvite"
                && n.RoomCode == lobby.Code && n.Status == "Pending" && n.ExpiresAt > now, ct))
                return Problem("Bu otağa dəvət artıq göndərilib.", 409);
            db.SocialNotifications.Add(new SocialNotification
            {
                UserId = targetId, ActorId = id, Kind = "BattleInvite", RoomCode = lobby.Code,
                HostName = await db.Users.Where(u => u.Id == lobby.HostUserId).Select(u => u.Nickname).SingleAsync(ct),
                DocumentName = lobby.DocumentName, QuestionCount = lobby.QuestionCount,
                CreatedAt = now, ExpiresAt = now.AddMinutes(Math.Clamp(options.Value.InvitationMinutes, 1, 60)),
            });
            return TypedResults.NoContent();
        }, ct);
        await events.ChangedAsync(targetId, ct);
        return result;
    }

    private static async Task<IResult> Notifications(ClaimsPrincipal user, AppDbContext db, BattleService battles, TimeProvider clock, CancellationToken ct)
    {
        var id = user.GetUserId();
        var unread = await db.SocialNotifications.AsNoTracking().CountAsync(n => n.UserId == id && n.ReadAt == null, ct);
        var items = await db.SocialNotifications.AsNoTracking().Where(n => n.UserId == id)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(100)
            .Join(db.Users, n => n.ActorId, a => a.Id, (n, a) => new NotificationDto(n.Id, n.ActorId, a.Nickname,
                n.Kind, n.Status, n.RoomCode, n.HostName, n.DocumentName, n.QuestionCount, n.CreatedAt, n.ExpiresAt, n.ReadAt != null)).ToListAsync(ct);
        var now = clock.GetUtcNow();
        for (var i = 0; i < items.Count; i++)
        {
            var n = items[i];
            if (n.Kind == "BattleInvite" && n.Status == "Pending")
                items[i] = n with { Status = n.ExpiresAt <= now ? "Expired"
                    : await battles.InvitationLobbyAsync(n.RoomCode!, id, false, ct) is null ? "Unavailable" : "Pending" };
        }
        return TypedResults.Ok(new NotificationsDto(unread, items));
    }

    private static async Task<IResult> DeleteNotification(Guid id, ClaimsPrincipal user, AppDbContext db, SocialEvents events, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var count = await db.SocialNotifications.Where(n => n.UserId == userId && n.Id == id).ExecuteDeleteAsync(ct);
        if (count == 0) return TypedResults.NotFound();
        await events.ChangedAsync(userId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteNotifications(bool? readOnly, ClaimsPrincipal user, AppDbContext db, SocialEvents events, CancellationToken ct)
    {
        var userId = user.GetUserId();
        await db.SocialNotifications.Where(n => n.UserId == userId && (readOnly != true || n.ReadAt != null)).ExecuteDeleteAsync(ct);
        await events.ChangedAsync(userId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ReadAll(ClaimsPrincipal user, AppDbContext db, SocialEvents events, TimeProvider clock, CancellationToken ct)
    {
        var id = user.GetUserId();
        await db.SocialNotifications.Where(n => n.UserId == id && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, clock.GetUtcNow()), ct);
        await events.ChangedAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Read(Guid id, ClaimsPrincipal user, AppDbContext db, SocialEvents events, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var count = await db.SocialNotifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, clock.GetUtcNow()), ct);
        if (count == 0) return TypedResults.NotFound();
        await events.ChangedAsync(userId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Respond(Guid id, string action, ClaimsPrincipal user, AppDbContext db,
        BattleService battles, SocialEvents events, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (action is not ("accept" or "reject")) return TypedResults.NotFound();
        var result = await Atomic(db, async () =>
        {
            var n = await db.SocialNotifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.Kind == "BattleInvite", ct);
            if (n is null) return TypedResults.NotFound();
            if (n.Status != "Pending") return Problem("Bu dəvət artıq cavablandırılıb.", 409);
            n.ReadAt = clock.GetUtcNow();
            if (action == "reject") { n.Status = "Rejected"; return TypedResults.NoContent(); }
            if (n.ExpiresAt <= clock.GetUtcNow()) { n.Status = "Expired"; return Problem("Dəvətin vaxtı bitib.", 409); }
            if (!await Pair(db, userId, n.ActorId).AnyAsync(f => f.IsAccepted, ct) || await Blocked(db, userId, n.ActorId, ct))
            { n.Status = "Unavailable"; return Problem("Bu dəvət artıq etibarlı deyil.", 409); }
            var lobby = await battles.InvitationLobbyAsync(n.RoomCode!, userId, false, ct);
            if (lobby is null || await Blocked(db, userId, lobby.HostUserId, ct))
            { n.Status = "Unavailable"; return Problem("Otaq doludur, bağlanıb və ya yarış artıq başlayıb.", 409); }
            n.Status = "Accepted";
            return TypedResults.Ok(new AcceptedInvitationDto(lobby.Code));
        }, ct);
        await events.ChangedAsync(userId, ct);
        return result;
    }

    private static IResult Problem(string detail, int statusCode = 400) => TypedResults.Problem(detail, statusCode: statusCode);
}

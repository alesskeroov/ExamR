using System.Text.Json;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Battles;

public sealed partial class BattleService
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private sealed record ParticipantState(Guid UserId, string DisplayName, string? AvatarUrl, DateTimeOffset JoinedAt, bool IsMuted,
        int Score, int CorrectCount, long CorrectTimeMs, Guid? CurrentAnswer, TimeSpan CurrentElapsed, int? LastPoints, bool? LastCorrect);
    private sealed record RoomState(BattleStatus Status, int CurrentIndex, DateTimeOffset QuestionStartedAt,
        DateTimeOffset Deadline, DateTimeOffset RevealEndsAt, List<ExamQuestion> Questions,
        List<ParticipantState> Participants, List<ChatMessageDto> Messages);

    private async Task PersistAsync(BattleRoom room, CancellationToken ct = default, IReadOnlyList<BattleResult>? results = null)
    {
        if (results is null) { await PersistCoreAsync(room, ct, null); return; }
        using var strategyScope = scopes.CreateScope();
        var strategyDb = strategyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await strategyDb.Database.CreateExecutionStrategy().ExecuteAsync(() => PersistCoreAsync(room, ct, results));
    }

    private async Task PersistCoreAsync(BattleRoom room, CancellationToken ct, IReadOnlyList<BattleResult>? results)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = results is null ? null : await db.Database.BeginTransactionAsync(ct);
        if (results is not null)
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '15s'; SELECT pg_advisory_xact_lock(hashtextextended('ExamR.BattleRankSettlement', 0));", ct);
        var stored = await db.StoredBattleRooms.Include(r => r.Members).SingleOrDefaultAsync(r => r.Id == room.BattleId, ct);
        if (stored is null)
        {
            if (room.Version.Length != 0) throw new HubException("Otaq tapılmadı.");
            stored = new StoredBattleRoom { Id = room.BattleId, Code = room.Code, Name = room.Name,
                CreatedById = room.CreatedById, CreatedAt = room.CreatedAt };
            db.StoredBattleRooms.Add(stored);
        }
        else
        {
            if (results is not null && await db.BattleResults.AnyAsync(r => r.BattleId == room.BattleId, ct))
            {
                room.Version = stored.Version;
                if (transaction is not null) await transaction.CommitAsync(ct);
                return;
            }
            db.Entry(stored).Property(r => r.Version).OriginalValue = room.Version;
        }

        stored.HostUserId = room.HostUserId;
        stored.DocumentId = room.Settings.DocumentId;
        stored.DocumentName = room.Settings.DocumentName;
        stored.RangeFrom = room.Settings.RangeFrom;
        stored.RangeTo = room.Settings.RangeTo;
        stored.QuestionCount = room.Settings.QuestionCount;
        stored.SecondsPerQuestion = room.Settings.SecondsPerQuestion;
        stored.Status = room.Status == BattleStatus.Lobby ? RoomStatus.Waiting
            : room.Status is BattleStatus.Question or BattleStatus.Reveal ? RoomStatus.InProgress : RoomStatus.Completed;
        stored.IsClosed = room.Status == BattleStatus.Closed;
        stored.UpdatedAt = clock.GetUtcNow();
        stored.StateJson = JsonSerializer.Serialize(new RoomState(room.Status, room.CurrentIndex, room.QuestionStartedAt,
            room.Deadline, room.RevealEndsAt, room.Questions,
            room.Participants.Values.Select(p => new ParticipantState(p.UserId, p.DisplayName, p.AvatarUrl, p.JoinedAt, p.IsMuted,
                p.Score, p.CorrectCount, p.CorrectTimeMs, p.CurrentAnswer, p.CurrentElapsed, p.LastPoints, p.LastCorrect)).ToList(),
            room.RecentMessages.ToList()));
        foreach (var id in room.Participants.Keys.Concat(room.Kicked).Distinct())
        {
            var member = stored.Members.SingleOrDefault(m => m.UserId == id);
            if (member is null) stored.Members.Add(member = new StoredBattleMember { RoomId = room.BattleId, UserId = id });
            member.IsKicked = room.Kicked.Contains(id);
        }
        if (results is not null && !await db.BattleResults.AnyAsync(r => r.BattleId == room.BattleId, ct))
        {
            await BattleRankEngine.ApplyAsync(db, results, scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BattleRankOptions>>().Value, ct);
            db.BattleResults.AddRange(results);
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new HubException("Otaq başqa əməliyyatla dəyişib. Səhifəni yeniləyin."); }
        if (transaction is not null) await transaction.CommitAsync(ct);
        room.Version = stored.Version;
    }

    private async Task<BattleRoom?> FindStoredAsync(string code, CancellationToken ct = default)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (_rooms.TryGetValue(code, out var cached)) return cached;
        await _loadGate.WaitAsync(ct);
        try
        {
            if (_rooms.TryGetValue(code, out cached)) return cached;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.StoredBattleRooms.AsNoTracking().Include(r => r.Members).SingleOrDefaultAsync(r => r.Code == code, ct);
            if (stored is null) return null;
            var data = JsonSerializer.Deserialize<RoomState>(stored.StateJson)
                ?? throw new InvalidOperationException("Battle recovery snapshot is missing.");
            var room = new BattleRoom(stored.Code, stored.HostUserId,
                new BattleSettings(stored.DocumentId, stored.DocumentName, stored.RangeFrom, stored.RangeTo, stored.QuestionCount, stored.SecondsPerQuestion), stored.CreatedAt)
            {
                BattleId = stored.Id, CreatedById = stored.CreatedById, Name = stored.Name, Version = stored.Version,
                Status = data.Status, CurrentIndex = data.CurrentIndex, QuestionStartedAt = data.QuestionStartedAt,
                Deadline = data.Deadline, RevealEndsAt = data.RevealEndsAt, Questions = data.Questions, LastActivityAt = stored.UpdatedAt,
            };
            foreach (var p in data.Participants)
                room.Participants[p.UserId] = new BattleParticipant(p.UserId, p.DisplayName, p.JoinedAt, p.AvatarUrl)
                {
                    IsMuted = p.IsMuted, Score = p.Score, CorrectCount = p.CorrectCount, CorrectTimeMs = p.CorrectTimeMs,
                    CurrentAnswer = p.CurrentAnswer, CurrentElapsed = p.CurrentElapsed, LastPoints = p.LastPoints, LastCorrect = p.LastCorrect,
                };
            foreach (var member in stored.Members.Where(m => m.IsKicked)) room.Kicked.Add(member.UserId);
            foreach (var message in data.Messages) room.RecentMessages.AddLast(message);
            _rooms[code] = room;
            if (room.Status is BattleStatus.Question or BattleStatus.Reveal) _ = Task.Run(() => RunGameAsync(room, resume: true));
            return room;
        }
        finally { _loadGate.Release(); }
    }

    private async Task<BattleRoom> RequireRoomAsync(string code, CancellationToken ct = default) =>
        await FindStoredAsync(code, ct) ?? throw new HubException("Otaq tapılmadı. Kodu yoxlayın.");

    public async Task RestoreActiveRoomsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var codes = await db.StoredBattleRooms.AsNoTracking().Where(r => r.Status != RoomStatus.Completed && !r.IsClosed).Select(r => r.Code).ToListAsync(ct);
        foreach (var code in codes)
        {
            if (await FindStoredAsync(code, ct) is { } room) ScheduleOccupancyCheck(room, room.ConnectionRevision);
        }
    }

    public async Task<string> CreateAsync(Guid hostId, string hostName, string name, BattleSettings settings, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string code;
        do { code = System.Security.Cryptography.RandomNumberGenerator.GetString(CodeAlphabet, CodeLength); }
        while (await db.StoredBattleRooms.AnyAsync(r => r.Code == code, ct));
        var room = new BattleRoom(code, hostId, settings, clock.GetUtcNow()) { Name = name };
        room.Participants[hostId] = new BattleParticipant(hostId, hostName, clock.GetUtcNow());
        await PersistAsync(room, ct);
        _rooms[code] = room;
        ScheduleOccupancyCheck(room, room.ConnectionRevision);
        return code;
    }

    // Called under the room gate. A brief grace period is used only for transport loss, so
    // a refresh/reconnect does not delete a room or unnecessarily transfer its host.
    private async Task<bool> ReconcileOccupancyAsync(BattleRoom room, CancellationToken ct = default)
    {
        if (room.Status == BattleStatus.Closed && room.HasConnections) return false;
        var hostId = room.ConnectedHostId;
        if (hostId is null)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.StoredBattleRooms.SingleOrDefaultAsync(r => r.Id == room.BattleId, ct);
            if (stored is not null)
            {
                var invitations = await db.SocialNotifications.Where(n => n.RoomCode == room.Code && n.Kind == "BattleInvite" && n.Status == "Pending").ToListAsync(ct);
                foreach (var invitation in invitations) invitation.Status = "Unavailable";
                db.Entry(stored).Property(r => r.Version).OriginalValue = room.Version;
                db.StoredBattleRooms.Remove(stored);
                await db.SaveChangesAsync(ct);
            }
            room.Status = BattleStatus.Closed;
            room.Advance.TrySetResult();
            _rooms.TryRemove(room.Code, out _);
            return true;
        }
        if (hostId != room.HostUserId)
        {
            var oldHost = room.HostUserId;
            var target = room.Participants[hostId.Value];
            var wasMuted = target.IsMuted;
            room.HostUserId = hostId.Value;
            target.IsMuted = false;
            try { await PersistAsync(room, ct); }
            catch { room.HostUserId = oldHost; target.IsMuted = wasMuted; throw; }
        }
        return false;
    }

    private void ScheduleOccupancyCheck(BattleRoom room, long revision)
    {
        _ = CheckAsync();
        async Task CheckAsync()
        {
            var ct = lifetime.ApplicationStopping;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                BattleStateDto state;
                bool deleted;
                await room.Gate.WaitAsync(ct);
                try
                {
                    if (room.ConnectionRevision != revision || !_rooms.TryGetValue(room.Code, out var current) || !ReferenceEquals(current, room)) return;
                    deleted = await ReconcileOccupancyAsync(room, ct);
                    state = Snapshot(room);
                }
                finally { room.Gate.Release(); }
                if (deleted) await hub.Clients.Group(room.Group).RoomClosed("Otaqda iştirakçı qalmadığı üçün otaq silindi.");
                else await hub.Clients.Group(room.Group).RoomUpdated(state);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Checking occupancy for battle {Code} failed", room.Code); }
        }
    }

    public async Task TransferHostAsync(string code, Guid hostId, Guid targetId, CancellationToken ct = default)
    {
        var room = await RequireRoomAsync(code, ct);
        BattleStateDto state;
        await room.Gate.WaitAsync(ct);
        try
        {
            RequireHost(room, hostId);
            if (!room.IsActive) throw new HubException("Bu otaqda host dəyişdirilə bilməz.");
            var target = RequireOtherMember(room, hostId, targetId);
            var wasMuted = target.IsMuted;
            room.HostUserId = targetId;
            target.IsMuted = false;
            try { await PersistAsync(room, ct); }
            catch { room.HostUserId = hostId; target.IsMuted = wasMuted; throw; }
            state = Snapshot(room);
        }
        finally { room.Gate.Release(); }
        await hub.Clients.Group(room.Group).RoomUpdated(state);
    }

    public async Task CloseRoomAsync(string code, Guid hostId, CancellationToken ct = default)
    {
        var room = await RequireRoomAsync(code, ct);
        await room.Gate.WaitAsync(ct);
        try
        {
            RequireHost(room, hostId);
            var previous = room.Status;
            room.Status = BattleStatus.Closed;
            try { await PersistAsync(room, ct); }
            catch { room.Status = previous; throw; }
            if (!room.HasConnections) await ReconcileOccupancyAsync(room, ct);
            room.Advance.TrySetResult();
        }
        finally { room.Gate.Release(); }
        await hub.Clients.Group(room.Group).RoomClosed("Host otağı bağladı.");
    }

    public async Task<BattleInviteRosterDto?> InvitationRosterAsync(string code, Guid userId, CancellationToken ct)
    {
        var room = await FindStoredAsync(code, ct);
        if (room is null) return null;
        await room.Gate.WaitAsync(ct);
        try
        {
            if (room.Status != BattleStatus.Lobby || !room.Participants.ContainsKey(userId) || room.Kicked.Contains(userId)) return null;
            return new BattleInviteRosterDto(room.Participants.Keys.ToList());
        }
        finally { room.Gate.Release(); }
    }
}

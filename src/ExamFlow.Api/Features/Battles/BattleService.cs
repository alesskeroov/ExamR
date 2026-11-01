using ExamFlow.Api.Features.Documents;
using System.Collections.Concurrent;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Battles;

/// <summary>
/// Owns all live battle rooms (in memory, single server) and runs their game loops.
/// The server alone decides timing, accepts answers and computes points; correct answers are only
/// broadcast after a question has closed.
/// </summary>
public sealed partial class BattleService(
    IHubContext<BattleHub, IBattleClient> hub,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IHostApplicationLifetime lifetime,
    ILogger<BattleService> logger)
{
    public const int MaxQuestions = 50;
    public const int MinSecondsPerQuestion = 15;
    public const int MaxSecondsPerQuestion = 30;
    public const int MaxMessageLength = 300;

    private const int ChatBurst = 5;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I
    private const int CodeLength = 6;
    private static readonly TimeSpan ChatWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RevealDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan AnswerGrace = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FinishedLifetime = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, BattleRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public async Task<BattleLobbyDto?> InvitationLobbyAsync(string code, Guid userId, bool requireMember, CancellationToken ct)
    {
        var room = await FindStoredAsync(code, ct);
        if (room is null) return null;
        await room.Gate.WaitAsync(ct);
        try
        {
            if (room.Status != BattleStatus.Lobby || room.Kicked.Contains(userId)) return null;
            if (requireMember && !room.Participants.ContainsKey(userId) && room.HostUserId != userId) return null;
            if (!room.Participants.ContainsKey(userId) && room.Participants.Count >= BattleRoom.MaxParticipants) return null;
            return new BattleLobbyDto(room.Code, room.HostUserId,
                room.Participants.GetValueOrDefault(room.HostUserId)?.DisplayName ?? "Otaq sahibi",
                room.Settings.DocumentName, room.Settings.QuestionCount);
        }
        finally { room.Gate.Release(); }
    }

    public async Task<IReadOnlyList<BattleLobbyDto>> InvitableLobbiesAsync(Guid userId, CancellationToken ct)
    {
        var lobbies = new List<BattleLobbyDto>();
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var codes = await db.StoredBattleRooms.AsNoTracking().Where(r => r.Status == RoomStatus.Waiting && !r.IsClosed
            && r.Members.Any(m => m.UserId == userId && !m.IsKicked)).Select(r => r.Code).ToListAsync(ct);
        foreach (var code in codes)
            if (await InvitationLobbyAsync(code, userId, requireMember: true, ct) is { } lobby) lobbies.Add(lobby);
        return lobbies;
    }

    public async Task<BattleStateDto> JoinAsync(string code, Guid userId, string displayName, string connectionId, string? avatarUrl = null)
    {
        var room = await RequireRoomAsync(code);
        // Join the group first so no broadcast is missed between the snapshot and the subscription.
        await hub.Groups.AddToGroupAsync(connectionId, room.Group);

        BattleStateDto mine, everyone;
        await room.Gate.WaitAsync();
        try
        {
            if (room.Status == BattleStatus.Closed)
            {
                throw new HubException("Otaq bağlanıb.");
            }

            if (room.Kicked.Contains(userId))
            {
                throw new HubException("Host sizi bu otaqdan çıxarıb.");
            }

            if (!room.Participants.TryGetValue(userId, out var participant))
            {
                if (room.Status != BattleStatus.Lobby)
                {
                    throw new HubException("Yarış artıq başlayıb.");
                }

                if (room.Participants.Count >= BattleRoom.MaxParticipants)
                {
                    throw new HubException($"Otaq doludur ({BattleRoom.MaxParticipants} nəfər).");
                }

                participant = new BattleParticipant(userId, displayName, clock.GetUtcNow(), avatarUrl);
                room.Participants[userId] = participant;
            }
            participant.SetProfile(displayName, avatarUrl);

            participant.Connections.Add(connectionId);
            room.ConnectionRevision++;
            room.LastActivityAt = clock.GetUtcNow();
            await PersistAsync(room);
            mine = Snapshot(room, participant, includeMessages: true);
            everyone = Snapshot(room);
        }
        catch
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, room.Group);
            throw;
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Clients.Group(room.Group).RoomUpdated(everyone);
        return mine;
    }

    /// <summary>Detach this connection, then promote a connected host or delete an empty room.</summary>
    public async Task LeaveAsync(string code, Guid userId, string connectionId)
    {
        var room = await RequireRoomAsync(code);
        BattleStateDto state;
        bool deleted;
        await room.Gate.WaitAsync();
        try
        {
            if (!room.Participants.TryGetValue(userId, out var participant))
            {
                return;
            }

            participant.Connections.Remove(connectionId);
            room.ConnectionRevision++;
            room.LastActivityAt = clock.GetUtcNow();
            deleted = await ReconcileOccupancyAsync(room);
            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Groups.RemoveFromGroupAsync(connectionId, room.Group);
        if (deleted) await hub.Clients.Group(room.Group).RoomClosed("Otaqda iştirakçı qalmadığı üçün otaq silindi.");
        else await hub.Clients.Group(room.Group).RoomUpdated(state);
    }

    /// <summary>Connection lost: the player stays in the room as offline and can rejoin (SignalR reconnect).</summary>
    public async Task DisconnectAsync(string connectionId)
    {
        foreach (var room in _rooms.Values)
        {
            BattleStateDto? state = null;
            long revision = 0;
            await room.Gate.WaitAsync();
            try
            {
                var participant = room.Participants.Values.FirstOrDefault(p => p.Connections.Remove(connectionId));
                if (participant is not null)
                {
                    revision = ++room.ConnectionRevision;
                    room.LastActivityAt = clock.GetUtcNow();
                    state = Snapshot(room);
                }
            }
            finally
            {
                room.Gate.Release();
            }

            if (state is not null)
            {
                await hub.Clients.Group(room.Group).RoomUpdated(state);
                ScheduleOccupancyCheck(room, revision);
            }
        }
    }

    public async Task StartAsync(string code, Guid userId)
    {
        var room = await RequireRoomAsync(code);
        await room.Gate.WaitAsync();
        try
        {
            RequireHost(room, userId);
            if (room.Status != BattleStatus.Lobby)
            {
                throw new HubException("Yarış artıq başlayıb.");
            }

            if (room.Participants.Values.Count(p => p.IsConnected) < BattleRoom.MinParticipants)
            {
                throw new HubException($"Başlamaq üçün ən azı {BattleRoom.MinParticipants} onlayn iştirakçı lazımdır.");
            }

            room.Questions = await LoadQuestionsAsync(room.Settings);
            BeginQuestion(room, 0, TimeSpan.FromSeconds(room.Settings.SecondsPerQuestion));
            try { await PersistAsync(room); }
            catch { room.Status = BattleStatus.Lobby; room.CurrentIndex = -1; room.Questions = []; throw; }
        }
        finally
        {
            room.Gate.Release();
        }

        _ = Task.Run(() => RunGameAsync(room, resume: true));
    }

    public async Task AnswerAsync(string code, Guid userId, int questionIndex, Guid optionId)
    {
        var room = await RequireRoomAsync(code);
        BattleStateDto state;
        TaskCompletionSource? everyoneAnswered = null;

        await room.Gate.WaitAsync();
        try
        {
            var participant = RequireMember(room, userId);
            var now = clock.GetUtcNow();
            var question = room.CurrentQuestion;
            if (room.Status != BattleStatus.Question || question is null || questionIndex != room.CurrentIndex || now > room.Deadline + AnswerGrace)
            {
                throw new HubException("Bu sualın vaxtı bitib.");
            }

            if (participant.CurrentAnswer is not null)
            {
                throw new HubException("Bu suala artıq cavab vermisiniz.");
            }

            if (question.Snapshot.Options.All(o => o.OptionId != optionId))
            {
                throw new HubException("Variant bu suala aid deyil.");
            }

            participant.CurrentAnswer = optionId;
            var elapsed = now - room.QuestionStartedAt;
            participant.CurrentElapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
            room.LastActivityAt = now;
            await PersistAsync(room);

            if (EveryoneAnswered(room))
            {
                everyoneAnswered = room.Advance;
            }

            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Clients.Group(room.Group).RoomUpdated(state);
        everyoneAnswered?.TrySetResult();
    }

    /// <summary>Host control: close the current question now, or skip the leaderboard pause.</summary>
    public async Task AdvanceAsync(string code, Guid userId)
    {
        var room = await RequireRoomAsync(code);
        TaskCompletionSource advance;
        await room.Gate.WaitAsync();
        try
        {
            RequireHost(room, userId);
            if (room.Status is not (BattleStatus.Question or BattleStatus.Reveal))
            {
                return;
            }

            advance = room.Advance;
        }
        finally
        {
            room.Gate.Release();
        }

        advance.TrySetResult();
    }

    public async Task SendMessageAsync(string code, Guid userId, string text)
    {
        var room = await RequireRoomAsync(code);
        var clean = BattleScoring.CleanMessage(text ?? string.Empty);
        if (clean.Length == 0)
        {
            throw new HubException("Mesaj boşdur.");
        }

        if (clean.Length > MaxMessageLength)
        {
            throw new HubException($"Mesaj {MaxMessageLength} simvoldan uzun ola bilməz.");
        }

        ChatMessageDto message;
        await room.Gate.WaitAsync();
        try
        {
            var participant = RequireMember(room, userId);
            if (room.Status == BattleStatus.Closed) throw new HubException("Otaq bağlanıb.");
            if (participant.IsMuted)
            {
                throw new HubException("Host sizi səssiz rejimə keçirib.");
            }

            var now = clock.GetUtcNow();
            while (participant.RecentChat.TryPeek(out var sentAt) && now - sentAt > ChatWindow)
            {
                participant.RecentChat.Dequeue();
            }

            if (participant.RecentChat.Count >= ChatBurst)
            {
                throw new HubException("Çox tez-tez mesaj göndərirsiniz. Bir neçə saniyə gözləyin.");
            }

            participant.RecentChat.Enqueue(now);
            message = new ChatMessageDto(Guid.NewGuid(), userId, participant.DisplayName, clean, now, participant.AvatarUrl);
            room.RecentMessages.AddLast(message);
            if (room.RecentMessages.Count > BattleRoom.MaxRecentMessages)
            {
                room.RecentMessages.RemoveFirst();
            }

            room.LastActivityAt = now;
            await PersistAsync(room);
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Clients.Group(room.Group).ChatMessage(message);
    }

    public async Task SetMutedAsync(string code, Guid hostId, Guid targetId, bool muted)
    {
        var room = await RequireRoomAsync(code);
        BattleStateDto state;
        await room.Gate.WaitAsync();
        try
        {
            RequireHost(room, hostId);
            RequireOtherMember(room, hostId, targetId).IsMuted = muted;
            await PersistAsync(room);
            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Clients.Group(room.Group).RoomUpdated(state);
    }

    public async Task KickAsync(string code, Guid hostId, Guid targetId)
    {
        var room = await RequireRoomAsync(code);
        BattleStateDto state;
        string[] connections;
        TaskCompletionSource? everyoneAnswered = null;

        await room.Gate.WaitAsync();
        try
        {
            RequireHost(room, hostId);
            var target = RequireOtherMember(room, hostId, targetId);
            connections = [.. target.Connections];
            room.Participants.Remove(targetId);
            room.Kicked.Add(targetId);
            await PersistAsync(room);
            if (room.Status == BattleStatus.Question && EveryoneAnswered(room))
            {
                everyoneAnswered = room.Advance;
            }

            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        foreach (var connection in connections)
        {
            await hub.Clients.Client(connection).Kicked("Host sizi otaqdan çıxardı.");
            await hub.Groups.RemoveFromGroupAsync(connection, room.Group);
        }

        await hub.Clients.Group(room.Group).RoomUpdated(state);
        everyoneAnswered?.TrySetResult();
    }

    /// <summary>Evicts only completed local caches; durable rooms are never expired by connection state.</summary>
    public async Task CleanupAsync()
    {
        var now = clock.GetUtcNow();
        foreach (var room in _rooms.Values)
        {
            await room.Gate.WaitAsync();
            try
            {
                if (room.Status is BattleStatus.Finished or BattleStatus.Closed && !room.HasConnections && now - room.LastActivityAt > FinishedLifetime)
                    _rooms.TryRemove(room.Code, out _);
            }
            finally { room.Gate.Release(); }
        }
    }

    private async Task RunGameAsync(BattleRoom room, bool resume = false)
    {
        var ct = lifetime.ApplicationStopping;
        try
        {
            var limit = TimeSpan.FromSeconds(room.Settings.SecondsPerQuestion);
            for (var index = Math.Max(room.CurrentIndex, 0); index < room.Questions.Count; index++)
            {
                TaskCompletionSource advance;
                BattleStateDto state;
                bool alreadyRevealed;

                await room.Gate.WaitAsync(ct);
                try
                {
                    if (room.Status == BattleStatus.Closed)
                    {
                        return;
                    }

                    if (!resume || index != room.CurrentIndex)
                    {
                        BeginQuestion(room, index, limit);
                        await PersistAsync(room, ct);
                    }
                    alreadyRevealed = room.Status == BattleStatus.Reveal;
                    advance = room.Advance;
                    state = Snapshot(room);
                }
                finally
                {
                    room.Gate.Release();
                }

                await hub.Clients.Group(room.Group).RoomUpdated(state);
                if (!alreadyRevealed)
                {
                    await Task.WhenAny(Task.Delay(Remaining(room.Deadline), ct), advance.Task);
                    await room.Gate.WaitAsync(ct);
                    try
                    {
                        if (room.Status == BattleStatus.Closed) return;
                        ScoreCurrentQuestion(room, limit);
                        room.Status = BattleStatus.Reveal;
                        room.RevealEndsAt = clock.GetUtcNow() + RevealDuration;
                        advance = room.Advance = NewSignal();
                        await PersistAsync(room, ct);
                        state = Snapshot(room);
                    }
                    finally { room.Gate.Release(); }
                    await hub.Clients.Group(room.Group).RoomUpdated(state);
                }
                await Task.WhenAny(Task.Delay(Remaining(room.RevealEndsAt), ct), advance.Task);
                ct.ThrowIfCancellationRequested();
                resume = false;
            }

            await FinishAsync(room, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Battle {Code} failed", room.Code);
            await CloseAsync(room, "Texniki xəta səbəbindən yarış dayandırıldı.");
        }
    }

    private void BeginQuestion(BattleRoom room, int index, TimeSpan limit)
    {
        var now = clock.GetUtcNow();
        room.Status = BattleStatus.Question;
        room.CurrentIndex = index;
        room.QuestionStartedAt = now;
        room.Deadline = now + limit;
        foreach (var participant in room.Participants.Values)
        {
            participant.CurrentAnswer = null;
            participant.CurrentElapsed = TimeSpan.Zero;
        }
        room.Advance = NewSignal();
        room.LastActivityAt = now;
    }

    private TimeSpan Remaining(DateTimeOffset until)
    {
        var now = clock.GetUtcNow();
        return until > now ? until - now : TimeSpan.Zero;
    }

    private async Task FinishAsync(BattleRoom room, CancellationToken ct)
    {
        BattleStateDto state;
        List<BattleResult> results;

        await room.Gate.WaitAsync(ct);
        try
        {
            if (room.Status == BattleStatus.Closed) return;
            var now = clock.GetUtcNow();
            room.Status = BattleStatus.Finished;
            room.LastActivityAt = now;
            var ranked = BattleScoring.Rank(room.Participants.Values);
            results = ranked.Select((p, i) => new BattleResult
            {
                BattleId = room.BattleId,
                RoomCode = room.Code,
                UserId = p.UserId,
                DocumentId = room.Settings.DocumentId,
                RangeFrom = room.Settings.RangeFrom,
                RangeTo = room.Settings.RangeTo,
                QuestionCount = room.Questions.Count,
                ParticipantCount = ranked.Count,
                Rank = i + 1,
                Score = p.Score,
                CorrectCount = p.CorrectCount,
                PlayedAt = now,
            }).ToList();
            await PersistAsync(room, ct, results);
            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Clients.Group(room.Group).RoomUpdated(state);
    }

    private static void ScoreCurrentQuestion(BattleRoom room, TimeSpan limit)
    {
        var correctOptionId = room.CurrentQuestion?.CorrectOptionId;
        foreach (var participant in room.Participants.Values)
        {
            var correct = correctOptionId is not null && participant.CurrentAnswer == correctOptionId;
            var points = BattleScoring.Points(correct, participant.CurrentElapsed, limit);
            participant.Score += points;
            participant.LastPoints = points;
            participant.LastCorrect = correct;
            if (correct)
            {
                participant.CorrectCount++;
                participant.CorrectTimeMs += (long)participant.CurrentElapsed.TotalMilliseconds;
            }
        }
    }

    private async Task<List<ExamQuestion>> LoadQuestionsAsync(BattleSettings settings)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Documents.AsNoTracking().Where(d => d.Id == settings.DocumentId)
            .AnyAsync(d => !d.ShareWithEveryone && !db.DocumentUserShares.Any(s => s.DocumentId == d.Id)
                && !db.DocumentGroupShares.Any(s => s.DocumentId == d.Id)
                && !db.UserRoles.Any(r => r.UserId == d.UploadedByUserId && db.Roles.Any(role => role.Id == r.RoleId && role.NormalizedName == "ADMIN"))))
            throw new HubException("Bu sənəd Battle üçün əlçatan deyil.");

        // Only questions with a known correct answer can be scored.
        var candidates = (await ExamEndpoints.Candidates(db, settings.DocumentId, settings.RangeFrom, settings.RangeTo, default))
            .Where(c => c.IsGradable)
            .ToList();
        var pickedIds = ExamEngine.PickQuestions(candidates, settings.QuestionCount, Random.Shared);
        if (pickedIds.Count < settings.QuestionCount)
        {
            throw new HubException("Seçilmiş aralıqda kifayət qədər sual qalmayıb.");
        }

        var questions = await db.Questions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => pickedIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id);

        return pickedIds.Select((id, position) => ExamEndpoints.Freeze(questions[id], position)).ToList();
    }

    private async Task CloseAsync(BattleRoom room, string message)
    {
        TaskCompletionSource advance;
        await room.Gate.WaitAsync();
        try
        {
            room.Status = BattleStatus.Closed;
            await PersistAsync(room);
            advance = room.Advance;
        }
        finally
        {
            room.Gate.Release();
        }

        advance.TrySetResult(); // Wakes a running game loop so it can stop.
        await hub.Clients.Group(room.Group).RoomClosed(message);
    }

    private static BattleParticipant RequireMember(BattleRoom room, Guid userId) =>
        room.Participants.TryGetValue(userId, out var participant)
            ? participant
            : throw new HubException("Siz bu otağın iştirakçısı deyilsiniz.");

    private static void RequireHost(BattleRoom room, Guid userId)
    {
        if (room.HostUserId != userId)
        {
            throw new HubException("Bu əməliyyatı yalnız host edə bilər.");
        }
    }

    private static BattleParticipant RequireOtherMember(BattleRoom room, Guid hostId, Guid targetId)
    {
        if (targetId == hostId)
        {
            throw new HubException("Host özünə bu əməliyyatı tətbiq edə bilməz.");
        }

        return RequireMember(room, targetId);
    }

    /// <summary>All online players (and anyone who already answered) have answered.</summary>
    private static bool EveryoneAnswered(BattleRoom room)
    {
        var players = room.Participants.Values.Where(p => p.IsConnected || p.CurrentAnswer is not null).ToList();
        return players.Count > 0 && players.All(p => p.CurrentAnswer is not null);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private BattleStateDto Snapshot(BattleRoom room, BattleParticipant? me = null, bool includeMessages = false)
    {
        var inQuestion = room.Status == BattleStatus.Question;
        var ordered = room.Status == BattleStatus.Lobby
            ? room.Participants.Values.OrderBy(p => p.JoinedAt).ToList()
            : BattleScoring.Rank(room.Participants.Values);

        var participants = ordered.Select((p, i) => new BattleParticipantDto(
            p.UserId, p.DisplayName, p.UserId == room.HostUserId, p.IsConnected, p.IsMuted,
            p.Score, p.CorrectCount, i + 1, p.LastPoints, p.LastCorrect, inQuestion && p.CurrentAnswer is not null, p.AvatarUrl)).ToList();

        var current = room.CurrentQuestion;
        var question = room.Status is BattleStatus.Question or BattleStatus.Reveal && current is not null
            ? new BattleQuestionDto(
                room.CurrentIndex,
                room.Questions.Count,
                current.Snapshot.Text,
                current.Snapshot.Options.Select(o => new ExamOptionDto(o.OptionId, o.Label, o.Text)).ToList(),
                room.Deadline,
                room.Settings.SecondsPerQuestion)
            : null;

        var reveal = room.Status == BattleStatus.Reveal && current is not null
            ? new BattleRevealDto(room.CurrentIndex, current.CorrectOptionId)
            : null;

        return new BattleStateDto(
            room.Code,
            room.HostUserId,
            room.Status,
            room.Settings,
            participants,
            question,
            reveal,
            inQuestion ? room.Participants.Values.Count(p => p.CurrentAnswer is not null) : 0,
            clock.GetUtcNow(),
            question is not null ? me?.CurrentAnswer : null,
            includeMessages ? [.. room.RecentMessages] : null,
            me?.UserId,
            room.Name);
    }
}

public sealed class BattleCleanupService(BattleService battles, ILogger<BattleCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await battles.RestoreActiveRoomsAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await battles.CleanupAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Battle room cleanup failed");
            }
        }
    }
}

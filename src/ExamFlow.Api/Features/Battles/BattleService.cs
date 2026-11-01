using System.Collections.Concurrent;
using System.Security.Cryptography;
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
public sealed class BattleService(
    IHubContext<BattleHub, IBattleClient> hub,
    IServiceScopeFactory scopes,
    TimeProvider clock,
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
    private static readonly TimeSpan IdleLobbyLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FinishedLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxRoomLifetime = TimeSpan.FromHours(4);

    private readonly ConcurrentDictionary<string, BattleRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public BattleRoom? FindActiveHostedRoom(Guid hostId) =>
        _rooms.Values.FirstOrDefault(r => r.HostUserId == hostId && r.IsActive);

    public string Create(Guid hostId, BattleSettings settings)
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            if (_rooms.TryAdd(code, new BattleRoom(code, hostId, settings, clock.GetUtcNow())))
            {
                return code;
            }
        }
    }

    public async Task<BattleStateDto> JoinAsync(string code, Guid userId, string displayName, string connectionId)
    {
        var room = Find(code);
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

                participant = new BattleParticipant(userId, displayName, clock.GetUtcNow());
                room.Participants[userId] = participant;
            }

            participant.Connections.Add(connectionId);
            room.LastActivityAt = clock.GetUtcNow();
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

    /// <summary>Leaving the lobby removes the player (the host leaving closes the room); during a game the player just goes offline.</summary>
    public async Task LeaveAsync(string code, Guid userId, string connectionId)
    {
        var room = Find(code);
        BattleStateDto? state = null;
        var closed = false;
        string[] connections = [connectionId];

        await room.Gate.WaitAsync();
        try
        {
            if (!room.Participants.TryGetValue(userId, out var participant))
            {
                return;
            }

            if (room.Status == BattleStatus.Lobby)
            {
                connections = [.. participant.Connections];
                room.Participants.Remove(userId);
                closed = userId == room.HostUserId;
                if (closed)
                {
                    room.Status = BattleStatus.Closed;
                }
            }
            else
            {
                participant.Connections.Remove(connectionId);
            }

            room.LastActivityAt = clock.GetUtcNow();
            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        foreach (var connection in connections)
        {
            await hub.Groups.RemoveFromGroupAsync(connection, room.Group);
        }

        if (closed)
        {
            await CloseAsync(room, "Host otağı bağladı.");
        }
        else
        {
            await hub.Clients.Group(room.Group).RoomUpdated(state!);
        }
    }

    /// <summary>Connection lost: the player stays in the room as offline and can rejoin (SignalR reconnect).</summary>
    public async Task DisconnectAsync(string connectionId)
    {
        foreach (var room in _rooms.Values)
        {
            BattleStateDto? state = null;
            await room.Gate.WaitAsync();
            try
            {
                var participant = room.Participants.Values.FirstOrDefault(p => p.Connections.Remove(connectionId));
                if (participant is not null)
                {
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
            }
        }
    }

    public async Task StartAsync(string code, Guid userId)
    {
        var room = Find(code);
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

            // Leave the lobby now so nobody joins while the questions load.
            room.Status = BattleStatus.Question;
        }
        finally
        {
            room.Gate.Release();
        }

        List<ExamQuestion> questions;
        try
        {
            questions = await LoadQuestionsAsync(room.Settings);
        }
        catch (Exception ex)
        {
            await room.Gate.WaitAsync();
            room.Status = BattleStatus.Lobby;
            room.Gate.Release();
            if (ex is HubException)
            {
                throw;
            }

            logger.LogError(ex, "Loading questions for battle {Code} failed", room.Code);
            throw new HubException("Suallar yüklənmədi.");
        }

        await room.Gate.WaitAsync();
        room.Questions = questions;
        room.Gate.Release();

        _ = Task.Run(() => RunGameAsync(room));
    }

    public async Task AnswerAsync(string code, Guid userId, int questionIndex, Guid optionId)
    {
        var room = Find(code);
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
        var room = Find(code);
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
        var room = Find(code);
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
            message = new ChatMessageDto(Guid.NewGuid(), userId, participant.DisplayName, clean, now);
            room.RecentMessages.AddLast(message);
            if (room.RecentMessages.Count > BattleRoom.MaxRecentMessages)
            {
                room.RecentMessages.RemoveFirst();
            }

            room.LastActivityAt = now;
        }
        finally
        {
            room.Gate.Release();
        }

        await hub.Clients.Group(room.Group).ChatMessage(message);
    }

    public async Task SetMutedAsync(string code, Guid hostId, Guid targetId, bool muted)
    {
        var room = Find(code);
        BattleStateDto state;
        await room.Gate.WaitAsync();
        try
        {
            RequireHost(room, hostId);
            RequireOtherMember(room, hostId, targetId).IsMuted = muted;
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
        var room = Find(code);
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

    /// <summary>Removes idle, finished and very old rooms. Called periodically by <see cref="BattleCleanupService"/>.</summary>
    public async Task CleanupAsync()
    {
        var now = clock.GetUtcNow();
        foreach (var room in _rooms.Values)
        {
            var expired =
                now - room.CreatedAt > MaxRoomLifetime
                || (room.Status is BattleStatus.Finished or BattleStatus.Closed && now - room.LastActivityAt > FinishedLifetime)
                || (room.Status == BattleStatus.Lobby && !room.HasConnections && now - room.LastActivityAt > IdleLobbyLifetime);

            if (expired)
            {
                await CloseAsync(room, "Otağın müddəti bitdi.");
            }
        }
    }

    private async Task RunGameAsync(BattleRoom room)
    {
        try
        {
            var limit = TimeSpan.FromSeconds(room.Settings.SecondsPerQuestion);
            for (var index = 0; index < room.Questions.Count; index++)
            {
                TaskCompletionSource advance;
                BattleStateDto state;

                await room.Gate.WaitAsync();
                try
                {
                    if (room.Status == BattleStatus.Closed)
                    {
                        return;
                    }

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

                    advance = room.Advance = NewSignal();
                    room.LastActivityAt = now;
                    state = Snapshot(room);
                }
                finally
                {
                    room.Gate.Release();
                }

                await hub.Clients.Group(room.Group).RoomUpdated(state);
                await Task.WhenAny(Task.Delay(limit), advance.Task);

                await room.Gate.WaitAsync();
                try
                {
                    if (room.Status == BattleStatus.Closed)
                    {
                        return;
                    }

                    ScoreCurrentQuestion(room, limit);
                    room.Status = BattleStatus.Reveal;
                    advance = room.Advance = NewSignal();
                    state = Snapshot(room);
                }
                finally
                {
                    room.Gate.Release();
                }

                await hub.Clients.Group(room.Group).RoomUpdated(state);
                await Task.WhenAny(Task.Delay(RevealDuration), advance.Task);
            }

            await FinishAsync(room);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Battle {Code} failed", room.Code);
            await CloseAsync(room, "Texniki xəta səbəbindən yarış dayandırıldı.");
        }
    }

    private async Task FinishAsync(BattleRoom room)
    {
        BattleStateDto state;
        List<BattleResult> results;

        await room.Gate.WaitAsync();
        try
        {
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
            state = Snapshot(room);
        }
        finally
        {
            room.Gate.Release();
        }

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BattleResults.AddRange(results);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Results are still shown live; only the history entry is lost.
            logger.LogError(ex, "Saving results of battle {Code} failed", room.Code);
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
            if (room.Status is not BattleStatus.Finished)
            {
                room.Status = BattleStatus.Closed;
            }

            advance = room.Advance;
        }
        finally
        {
            room.Gate.Release();
        }

        _rooms.TryRemove(room.Code, out _);
        advance.TrySetResult(); // Wakes a running game loop so it can stop.
        await hub.Clients.Group(room.Group).RoomClosed(message);
    }

    private BattleRoom Find(string code) =>
        _rooms.TryGetValue(code?.Trim() ?? string.Empty, out var room) ? room : throw new HubException("Otaq tapılmadı. Kodu yoxlayın.");

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
            p.Score, p.CorrectCount, i + 1, p.LastPoints, p.LastCorrect, inQuestion && p.CurrentAnswer is not null)).ToList();

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
            includeMessages ? [.. room.RecentMessages] : null);
    }
}

public sealed class BattleCleanupService(BattleService battles, ILogger<BattleCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

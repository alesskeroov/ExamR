using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ExamFlow.Api.Features.Battles;

/// <summary>
/// Real-time endpoint (/hubs/battle). Authenticated with the same JWT as the REST API; every method
/// checks room membership / host rights in <see cref="BattleService"/>. Errors reach the client as HubException messages.
/// </summary>
[Authorize]
public sealed class BattleHub(BattleService battles) : Hub<IBattleClient>
{
    public const string Path = $"{AuthenticationSetup.HubPathPrefix}/battle";

    private Guid UserId => Context.User!.GetUserId();

    private string DisplayName => Context.User!.FindFirst(JwtClaims.Name)?.Value is { Length: > 0 } name ? name : "İstifadəçi";

    /// <summary>Joins (or rejoins after a reconnect) and returns the full room state for this user.</summary>
    public Task<BattleStateDto> JoinRoom(string code) => battles.JoinAsync(code, UserId, DisplayName, Context.ConnectionId);

    public Task LeaveRoom(string code) => battles.LeaveAsync(code, UserId, Context.ConnectionId);

    public Task StartGame(string code) => battles.StartAsync(code, UserId);

    public Task SubmitAnswer(string code, int questionIndex, Guid optionId) => battles.AnswerAsync(code, UserId, questionIndex, optionId);

    public Task Advance(string code) => battles.AdvanceAsync(code, UserId);

    public Task SendMessage(string code, string text) => battles.SendMessageAsync(code, UserId, text);

    public Task SetMuted(string code, Guid userId, bool muted) => battles.SetMutedAsync(code, UserId, userId, muted);

    public Task Kick(string code, Guid userId) => battles.KickAsync(code, UserId, userId);

    public override Task OnDisconnectedAsync(Exception? exception) => battles.DisconnectAsync(Context.ConnectionId);
}

using System.Net;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Feedback;

public static class FeedbackEmail
{
    public static EmailMessage Build(FeedbackMessage message, string recipient)
    {
        var kind = message.Kind == "problem" ? "Problem" : "Təklif";
        var text = $"Yeni müraciət: {kind}\nMüraciət: {message.Id}\nİstifadəçi: {message.SenderName}\nE-poçt: {message.SenderEmail}\n"
            + $"Sənəd: {message.DocumentName ?? "—"}\nBölmə: {message.PagePath}\nTarix (UTC): {message.CreatedAt:u}\n\n{message.Text}";
        return new(recipient, $"[ExamR] {kind} · {message.Id}",
            $"<div style=\"white-space:pre-wrap;font-family:sans-serif\">{WebUtility.HtmlEncode(text)}</div>", text);
    }

    public static TimeSpan RetryDelay(int attempts) => TimeSpan.FromMinutes(attempts switch
    {
        <= 1 => 1, 2 => 5, 3 => 15, 4 => 60, _ => 360,
    });
}

public sealed class FeedbackDelivery(AppDbContext db, IEmailTransport transport, IOptions<FeedbackOptions> options,
    TimeProvider clock, ILogger<FeedbackDelivery> logger)
{
    public async Task<TimeSpan> RunAsync(CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var now = clock.GetUtcNow();
            var message = await db.FeedbackMessages.AsNoTracking().Where(x => x.SentAt == null && x.NextAttemptAt <= now)
                .OrderBy(x => x.NextAttemptAt).FirstOrDefaultAsync(ct);
            if (message is null) break;
            var lease = Guid.NewGuid();
            // An atomic lease prevents two workers from delivering the same row together; a crash expires it.
            var claimed = await db.FeedbackMessages.Where(x => x.Id == message.Id && x.SentAt == null && x.NextAttemptAt <= now)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.LeaseId, lease)
                    .SetProperty(x => x.NextAttemptAt, now.AddMinutes(2))
                    .SetProperty(x => x.Attempts, x => x.Attempts < 30 ? x.Attempts + 1 : 30), ct);
            if (claimed == 0) continue;
            var sent = false;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                sent = await transport.SendAsync(FeedbackEmail.Build(message, options.Value.Recipient), timeout.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Do not log the submitted text, sender, or transport response (it can contain personal data).
                logger.LogWarning("Feedback {Id} delivery failed ({ErrorType}); retry scheduled", message.Id, ex.GetType().Name);
            }
            var completed = clock.GetUtcNow();
            var next = completed + FeedbackEmail.RetryDelay(message.Attempts + 1);
            await db.FeedbackMessages.Where(x => x.Id == message.Id && x.LeaseId == lease)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.SentAt, sent ? completed : (DateTimeOffset?)null)
                    .SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.LeaseId, (Guid?)null), ct);
        }

        var nextAttempt = await db.FeedbackMessages.AsNoTracking().Where(x => x.SentAt == null)
            .Select(x => (DateTimeOffset?)x.NextAttemptAt).MinAsync(ct);
        var remaining = nextAttempt - clock.GetUtcNow();
        return remaining is null ? Timeout.InfiniteTimeSpan : remaining > TimeSpan.Zero ? remaining.Value : TimeSpan.Zero;
    }
}

public sealed class FeedbackSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    public void Notify() { try { _signal.Release(); } catch (SemaphoreFullException) { } }
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}

public sealed class FeedbackWorker(IServiceScopeFactory scopes, FeedbackSignal signal, DatabaseStartup startup,
    ILogger<FeedbackWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Preserve DB-free cold starts. Once normal traffic opens the DB, recover unsent feedback.
            await startup.Ready.WaitAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMinutes(1);
                try
                {
                    using var scope = scopes.CreateScope();
                    delay = await scope.ServiceProvider.GetRequiredService<FeedbackDelivery>().RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning("Feedback worker failed ({ErrorType}); will retry", ex.GetType().Name); }
                // No pending messages: no idle SQL polling that would keep Neon awake.
                await signal.WaitAsync(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

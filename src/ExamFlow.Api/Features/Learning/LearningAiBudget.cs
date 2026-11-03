using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Learning;

public enum BudgetDecision { Reserved, BudgetExhausted, DailyLimit, Busy }

public sealed record AiBudgetStatus(string Model, decimal TotalBudgetUsd, decimal SpentUsd, decimal ReservedUsd, decimal RemainingUsd,
    int RequestsToday, int DailyRequestsPerUser, long InputTokens, long OutputTokens, bool Enabled);

public static class AiCost
{
    public static decimal Of(ChatModelUsage usage, LearningAiOptions o, string? model = null) =>
        model is not null && model == o.LightModel && model != o.Model
            ? (Math.Max(0, usage.InputTokens - usage.CachedInputTokens) * o.LightInputUsdPerMillion
               + usage.CachedInputTokens * o.LightCachedInputUsdPerMillion
               + usage.OutputTokens * o.LightOutputUsdPerMillion) / 1_000_000m
            : (Math.Max(0, usage.InputTokens - usage.CachedInputTokens) * o.InputUsdPerMillion
               + usage.CachedInputTokens * o.CachedInputUsdPerMillion
               + usage.OutputTokens * o.OutputUsdPerMillion) / 1_000_000m;

    /// <summary>Worst case for one call: the whole prompt uncached plus every allowed output token.</summary>
    public static decimal Reserve(int estimatedInputTokens, LearningAiOptions o, int? maxOutputTokens = null, string? model = null) =>
        Of(new ChatModelUsage(estimatedInputTokens, 0, maxOutputTokens ?? o.MaxOutputTokens), o, model);
}

/// <summary>
/// Reserves money before a model call and settles it after. An app lock serialises the check-and-insert across
/// parallel requests (and API instances), so two calls cannot both spend the last cents.
/// </summary>
public sealed class LearningAiBudget(AppDbContext db, IOptions<LearningAiOptions> options, TimeProvider clock)
{
    // A reservation older than this belongs to a crashed request and stops holding budget.
    private static readonly TimeSpan StaleReservation = TimeSpan.FromMinutes(10);

    public async Task<(BudgetDecision Decision, LearningAiUsage? Usage)> ReserveAsync(Guid userId, Guid? sessionId, decimal amount, CancellationToken ct, string? model = null)
    {
        var o = options.Value;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A retried attempt must not insert the previous attempt's row again; other tracked changes are kept.
            foreach (var stale in db.ChangeTracker.Entries<LearningAiUsage>().Where(e => e.State == EntityState.Added).ToList())
                stale.State = EntityState.Detached;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = 'learning-ai-budget', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000", ct);
            var now = clock.GetUtcNow();
            var fresh = now - StaleReservation;
            var usage = db.Set<LearningAiUsage>();

            if (await usage.AnyAsync(u => u.UserId == userId && u.Status == LearningUsageStatus.Reserved && u.CreatedAt > fresh, ct))
                return (BudgetDecision.Busy, (LearningAiUsage?)null);
            var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            if (await usage.CountAsync(u => u.UserId == userId && u.CreatedAt >= dayStart && u.Status != LearningUsageStatus.Released, ct) >= o.DailyRequestsPerUser)
                return (BudgetDecision.DailyLimit, (LearningAiUsage?)null);
            if (await CommittedAsync(fresh, ct) + amount > o.TotalBudgetUsd)
                return (BudgetDecision.BudgetExhausted, (LearningAiUsage?)null);

            var row = new LearningAiUsage
            {
                Id = Guid.NewGuid(), UserId = userId, SessionId = sessionId, Model = model ?? o.Model, Status = LearningUsageStatus.Reserved,
                ReservedUsd = amount, CreatedAt = now,
            };
            usage.Add(row);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (BudgetDecision.Reserved, (LearningAiUsage?)row);
        });
    }

    /// <summary>Records real usage. Without usage data (stream cut off) the estimate is charged, never zero.</summary>
    public async Task SettleAsync(Guid usageId, Guid? messageId, ChatModelUsage? actual, ChatModelUsage estimate, bool failedBeforeOutput)
    {
        var row = await db.Set<LearningAiUsage>().FirstAsync(u => u.Id == usageId, CancellationToken.None);
        var used = actual ?? estimate;
        row.MessageId = messageId;
        row.Status = failedBeforeOutput && actual is null ? LearningUsageStatus.Released : actual is null ? LearningUsageStatus.Failed : LearningUsageStatus.Completed;
        if (row.Status != LearningUsageStatus.Released)
        {
            row.InputTokens = used.InputTokens;
            row.CachedInputTokens = used.CachedInputTokens;
            row.OutputTokens = used.OutputTokens;
            row.CostUsd = AiCost.Of(used, options.Value, row.Model);
        }
        row.CompletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(CancellationToken.None);
    }

    public async Task<AiBudgetStatus> StatusAsync(Guid userId, CancellationToken ct)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var fresh = now - StaleReservation;
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var usage = db.Set<LearningAiUsage>().AsNoTracking();
        var spent = await usage.Where(u => u.Status == LearningUsageStatus.Completed || u.Status == LearningUsageStatus.Failed).SumAsync(u => (decimal?)u.CostUsd, ct) ?? 0;
        var reserved = await usage.Where(u => u.Status == LearningUsageStatus.Reserved && u.CreatedAt > fresh).SumAsync(u => (decimal?)u.ReservedUsd, ct) ?? 0;
        var tokens = await usage.GroupBy(_ => 1).Select(g => new { In = g.Sum(u => (long)u.InputTokens), Out = g.Sum(u => (long)u.OutputTokens) }).FirstOrDefaultAsync(ct);
        var today = await usage.CountAsync(u => u.UserId == userId && u.CreatedAt >= dayStart && u.Status != LearningUsageStatus.Released, ct);
        return new AiBudgetStatus(o.Model, o.TotalBudgetUsd, Math.Round(spent, 6), Math.Round(reserved, 6), Math.Max(0, Math.Round(o.TotalBudgetUsd - spent - reserved, 6)),
            today, o.DailyRequestsPerUser, tokens?.In ?? 0, tokens?.Out ?? 0, o.Enabled);
    }

    private async Task<decimal> CommittedAsync(DateTimeOffset fresh, CancellationToken ct)
    {
        var usage = db.Set<LearningAiUsage>();
        var spent = await usage.Where(u => u.Status == LearningUsageStatus.Completed || u.Status == LearningUsageStatus.Failed).SumAsync(u => (decimal?)u.CostUsd, ct) ?? 0;
        var held = await usage.Where(u => u.Status == LearningUsageStatus.Reserved && u.CreatedAt > fresh).SumAsync(u => (decimal?)u.ReservedUsd, ct) ?? 0;
        return spent + held;
    }
}

using System.Security.Claims;
using ExamFlow.Api.Features.Learning;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Usage;

public sealed class UsageLimiter(AppDbContext db, IOptions<UsageOptions> options, TimeProvider clock)
{
    public async Task<UsageCheck> CheckAsync(Guid userId, bool isAdmin, UsageKind kind, CancellationToken ct)
    {
        var custom = await db.UserUsageLimits.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var now = clock.GetUtcNow();
        var start = UsageQuota.MonthStart(now);
        var end = UsageQuota.NextReset(now);
        var used = kind == UsageKind.AiRequest
            ? await db.Set<LearningAiUsage>().AsNoTracking().CountAsync(x => x.UserId == userId && x.CountsTowardsMonthlyQuota && x.CreatedAt >= start && x.CreatedAt < end && x.Status != LearningUsageStatus.Released, ct)
            : await db.UsageEvents.AsNoTracking().CountAsync(x => x.UserId == userId && x.Kind == kind && x.OccurredAt >= start && x.OccurredAt < end, ct);
        var quota = isAdmin ? PlanQuota.From(options.Value) : (await QuotasAsync([userId], ct))[userId];
        return new(kind, used, UsageQuota.EffectiveLimit(kind, custom, quota, isAdmin), end);
    }

    /// <summary>Limits of each user's active package, or of the Free package when none is active.</summary>
    public async Task<Dictionary<Guid, PlanQuota>> QuotasAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ai = options.Value.MonthlyAiRequests;
        var active = await db.Subscriptions.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId) && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)
            .OrderBy(s => s.StartsAt)
            .Select(s => new { s.UserId, Quota = new PlanQuota(s.Plan.MonthlyUploads, s.Plan.MonthlyExams, s.Plan.MonthlyBattles, s.Plan.MonthlyDimTests, s.Plan.MonthlyGuides, null) })
            .ToListAsync(ct);
        var free = await db.Plans.AsNoTracking().Where(p => p.IsFree)
            .Select(p => new PlanQuota(p.MonthlyUploads, p.MonthlyExams, p.MonthlyBattles, p.MonthlyDimTests, p.MonthlyGuides, null)).FirstOrDefaultAsync(ct);
        var fallback = free is null ? PlanQuota.From(options.Value) : free with { AiRequests = ai };
        var result = userIds.Distinct().ToDictionary(id => id, _ => fallback);
        foreach (var row in active) result[row.UserId] = row.Quota with { AiRequests = ai };
        return result;
    }

    public async Task LockAsync(Guid userId, UsageKind kind, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Usage consumption requires a resource transaction.");
        var key = $"usage:{userId}:{kind}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    public async Task<IResult?> TryConsumeAsync(ClaimsPrincipal user, UsageKind kind, Guid? resourceId, CancellationToken ct)
    {
        await LockAsync(user.GetUserId(), kind, ct);
        var check = await CheckAsync(user.GetUserId(), user.IsAdmin(), kind, ct);
        if (!check.Allowed) return check.Problem();
        if (kind == UsageKind.AiRequest) throw new InvalidOperationException("AI usage is recorded by its reservation.");
        db.UsageEvents.Add(new UsageEvent { UserId = user.GetUserId(), Kind = kind, ResourceId = resourceId, OccurredAt = clock.GetUtcNow() });
        return null;
    }

    // Removal is tracked so it commits atomically with the document's Failed status.
    public async Task RefundAsync(Guid resourceId, UsageKind kind, CancellationToken ct)
    {
        var rows = await db.UsageEvents.Where(x => x.ResourceId == resourceId && x.Kind == kind).ToListAsync(ct);
        db.UsageEvents.RemoveRange(rows);
    }

    public async Task<UsageDto> SummaryAsync(Guid userId, bool admin, CancellationToken ct) =>
        (await SummariesAsync([userId], admin ? [userId] : [], ct))[userId];

    public async Task<Dictionary<Guid, UsageDto>> SummariesAsync(IReadOnlyCollection<Guid> ids, IReadOnlyCollection<Guid> admins, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var start = UsageQuota.MonthStart(now);
        var end = UsageQuota.NextReset(now);
        var custom = await db.UserUsageLimits.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var events = await db.UsageEvents.AsNoTracking().Where(x => ids.Contains(x.UserId) && x.OccurredAt >= start && x.OccurredAt < end)
            .GroupBy(x => new { x.UserId, x.Kind }).Select(g => new { g.Key.UserId, g.Key.Kind, Used = g.Count() }).ToListAsync(ct);
        var ai = await db.Set<LearningAiUsage>().AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.CountsTowardsMonthlyQuota && x.CreatedAt >= start && x.CreatedAt < end && x.Status != LearningUsageStatus.Released)
            .GroupBy(x => x.UserId).Select(g => new { UserId = g.Key, Used = g.Count() }).ToDictionaryAsync(x => x.UserId, x => x.Used, ct);
        var quotas = await QuotasAsync(ids, ct);
        return ids.Distinct().ToDictionary(id => id, id =>
        {
            var isAdmin = admins.Contains(id);
            var items = Enum.GetValues<UsageKind>().Select(kind => new UsageItemDto(kind,
                kind == UsageKind.AiRequest ? ai.GetValueOrDefault(id) : events.FirstOrDefault(x => x.UserId == id && x.Kind == kind)?.Used ?? 0,
                UsageQuota.EffectiveLimit(kind, custom.GetValueOrDefault(id), quotas[id], isAdmin))).ToList();
            // AI is switched off in the product, so its quota does not decide whether the account is "unlimited".
            return new UsageDto(end, items.Where(x => x.Kind != UsageKind.AiRequest).All(x => x.Limit is null), items);
        });
    }
}

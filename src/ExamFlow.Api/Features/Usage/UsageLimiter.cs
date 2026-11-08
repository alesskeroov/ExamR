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
        return new(kind, used, UsageQuota.EffectiveLimit(kind, custom, options.Value, isAdmin), end);
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

    public async Task<UsageDto> SummaryAsync(Guid userId, bool admin, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var start = UsageQuota.MonthStart(now);
        var end = UsageQuota.NextReset(now);
        var custom = await db.UserUsageLimits.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var counts = await db.UsageEvents.AsNoTracking().Where(x => x.UserId == userId && x.OccurredAt >= start && x.OccurredAt < end)
            .GroupBy(x => x.Kind).Select(g => new { Kind = g.Key, Used = g.Count() }).ToDictionaryAsync(x => x.Kind, x => x.Used, ct);
        counts[UsageKind.AiRequest] = await db.Set<LearningAiUsage>().AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.CountsTowardsMonthlyQuota && x.CreatedAt >= start && x.CreatedAt < end && x.Status != LearningUsageStatus.Released, ct);
        var items = Enum.GetValues<UsageKind>().Select(kind => new UsageItemDto(kind, counts.GetValueOrDefault(kind), UsageQuota.EffectiveLimit(kind, custom, options.Value, admin))).ToList();
        return new(end, items.All(x => x.Limit is null), items);
    }
}

using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Billing;

/// <summary>Writes to a user's periods. Callers open the transaction; every write takes the user's billing lock first.</summary>
public sealed class BillingService(AppDbContext db, TimeProvider clock)
{
    public async Task LockAsync(Guid userId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Billing changes require a transaction.");
        var key = $"billing:{userId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    /// <summary>Periods that are running or scheduled (not cancelled, not ended), oldest first.</summary>
    public Task<List<Subscription>> LiveAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.CancelledAt == null && s.EndsAt > now)
            .OrderBy(s => s.StartsAt).ToListAsync(ct);

    public static List<PeriodItem> Chain(IEnumerable<Subscription> live) =>
        live.Select(s => new PeriodItem(s.PlanId, s.Plan.Price, s.Plan.DurationDays, s.StartsAt, s.EndsAt)).ToList();

    public static PlanChange Decide(IReadOnlyList<Subscription> live, Plan target, int periods, int? bonusOverride, DateTimeOffset now)
    {
        var change = BillingRules.Decide(Chain(live), new PlanTarget(target.Id, target.Price, target.DurationDays), periods, 0, now);
        if (bonusOverride is not { } bonus) return change;
        return change with { EndsAt = change.EndsAt - TimeSpan.FromDays(change.BonusDays) + TimeSpan.FromDays(bonus), BonusDays = bonus };
    }

    /// <summary>Buying a package (approved payment): extend, schedule after a dearer package, or upgrade now with bonus days.</summary>
    public async Task<PlanChange> PurchaseAsync(Guid userId, Plan target, int periods, int? bonusOverride, Guid paymentId, Guid adminId, string? note, CancellationToken ct)
    {
        await LockAsync(userId, ct);
        var now = clock.GetUtcNow();
        var live = await LiveAsync(userId, now, ct);
        var change = Decide(live, target, periods, bonusOverride, now);
        if (change.Kind == PlanChangeKind.Upgrade) Stop(live, now, adminId, cancelled: false);
        Add(userId, target.Id, change.StartsAt, change.EndsAt, SubscriptionSource.Payment, paymentId, adminId, note, now);
        return change;
    }

    /// <summary>Admin assignment: replaces whatever the user has, starting now (no bonus).</summary>
    public async Task AssignAsync(Guid userId, Plan target, int periods, int extraDays, Guid adminId, string? note, CancellationToken ct)
    {
        await LockAsync(userId, ct);
        var now = clock.GetUtcNow();
        Stop(await LiveAsync(userId, now, ct), now, adminId, cancelled: false);
        Add(userId, target.Id, now, now.AddDays(target.DurationDays * periods + extraDays), SubscriptionSource.Manual, null, adminId, note, now);
    }

    /// <summary>Adds days to the end of the user's last period. False when the user has no package.</summary>
    public async Task<bool> ExtendAsync(Guid userId, int days, CancellationToken ct)
    {
        await LockAsync(userId, ct);
        var live = await LiveAsync(userId, clock.GetUtcNow(), ct);
        if (live.Count == 0) return false;
        live[^1].EndsAt = live[^1].EndsAt.AddDays(days);
        return true;
    }

    /// <summary>Ends the running period now and cancels scheduled ones. False when the user has no package.</summary>
    public async Task<bool> CancelAsync(Guid userId, Guid adminId, CancellationToken ct)
    {
        await LockAsync(userId, ct);
        var now = clock.GetUtcNow();
        var live = await LiveAsync(userId, now, ct);
        if (live.Count == 0) return false;
        Stop(live, now, adminId, cancelled: true);
        return true;
    }

    private static void Stop(IEnumerable<Subscription> live, DateTimeOffset now, Guid adminId, bool cancelled)
    {
        foreach (var s in live)
        {
            if (s.StartsAt < now)
            {
                // The used part stays in the history as it was.
                s.EndsAt = now;
                if (!cancelled) continue;
            }
            s.CancelledAt = now;
            s.CancelledByUserId = adminId;
        }
    }

    private void Add(Guid userId, Guid planId, DateTimeOffset start, DateTimeOffset end, SubscriptionSource source, Guid? paymentId,
        Guid adminId, string? note, DateTimeOffset now) =>
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId, PlanId = planId, StartsAt = start, EndsAt = end, Source = source, PaymentRequestId = paymentId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), CreatedAt = now, CreatedByUserId = adminId,
        });

    /// <summary>Current package per user (Free when none) and what follows it.</summary>
    public async Task<Dictionary<Guid, CurrentPlanDto>> CurrentAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var live = await db.Subscriptions.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId) && s.CancelledAt == null && s.EndsAt > now)
            .OrderBy(s => s.StartsAt)
            .Select(s => new { s.UserId, s.PlanId, s.Plan.Name, s.StartsAt, s.EndsAt })
            .ToListAsync(ct);
        var free = await db.Plans.AsNoTracking().Where(p => p.IsFree).Select(p => new { p.Id, p.Name }).FirstOrDefaultAsync(ct);
        var byUser = live.ToLookup(x => x.UserId);
        return userIds.Distinct().ToDictionary(id => id, id =>
        {
            var periods = byUser[id].ToList();
            var active = periods.FirstOrDefault(p => p.StartsAt <= now);
            if (active is null)
            {
                var first = periods.FirstOrDefault();
                return new CurrentPlanDto(free?.Id, free?.Name ?? "Free", true, null,
                    first is null ? null : new NextPlanDto(first.PlanId, first.Name, first.StartsAt, first.EndsAt));
            }
            // Back-to-back periods of the same package read as one.
            var end = active.EndsAt;
            var rest = periods.SkipWhile(p => p != active).Skip(1).ToList();
            var i = 0;
            for (; i < rest.Count && rest[i].PlanId == active.PlanId && rest[i].StartsAt <= end; i++) end = rest[i].EndsAt > end ? rest[i].EndsAt : end;
            var next = i < rest.Count ? rest[i] : null;
            return new CurrentPlanDto(active.PlanId, active.Name, false, end,
                next is null ? null : new NextPlanDto(next.PlanId, next.Name, next.StartsAt, next.EndsAt));
        });
    }
}

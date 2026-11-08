using System.Security.Claims;
using ExamFlow.Api.Features.Learning;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Usage;

public static class UsageEndpoints
{
    public static void MapUsageEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/usage", async (ClaimsPrincipal user, UsageLimiter limiter, CancellationToken ct) =>
            TypedResults.Ok(await limiter.SummaryAsync(user.GetUserId(), user.IsAdmin(), ct))).RequireAuthorization();
        var admin = api.MapGroup("/admin/usage/users").RequireAuthorization(p => p.RequireRole(Roles.Admin));
        admin.MapGet("/", List);
        admin.MapGet("/{userId:guid}", Get);
        admin.MapPut("/{userId:guid}", Update).Validate<UpdateUsageRequest>();
        admin.MapDelete("/{userId:guid}", Delete);
    }

    private static async Task<IResult> List(AppDbContext db, UsageLimiter limiter, IOptions<UsageOptions> options, TimeProvider clock, CancellationToken ct, string? q = null, int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToUpperInvariant();
            users = users.Where(x => x.NormalizedNickname!.Contains(term) || x.NormalizedEmail!.Contains(term));
        }
        var total = await users.CountAsync(ct);
        var people = await users.OrderBy(x => x.Nickname).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
            .Select(x => new { x.Id, x.Nickname, x.Email }).ToListAsync(ct);
        var ids = people.Select(x => x.Id).ToArray();
        var admins = await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                            where ids.Contains(ur.UserId) && r.Name == Roles.Admin select ur.UserId).ToListAsync(ct);
        var custom = await db.UserUsageLimits.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var start = UsageQuota.MonthStart(clock.GetUtcNow());
        var end = UsageQuota.NextReset(clock.GetUtcNow());
        var events = await db.UsageEvents.AsNoTracking().Where(x => ids.Contains(x.UserId) && x.OccurredAt >= start && x.OccurredAt < end)
            .GroupBy(x => new { x.UserId, x.Kind }).Select(g => new { g.Key.UserId, g.Key.Kind, Used = g.Count() }).ToListAsync(ct);
        var ai = await db.Set<LearningAiUsage>().AsNoTracking().Where(x => ids.Contains(x.UserId) && x.CountsTowardsMonthlyQuota && x.CreatedAt >= start && x.CreatedAt < end && x.Status != LearningUsageStatus.Released)
            .GroupBy(x => x.UserId).Select(g => new { UserId = g.Key, Used = g.Count() }).ToDictionaryAsync(x => x.UserId, x => x.Used, ct);
        var items = people.Select(person =>
        {
            var isAdmin = admins.Contains(person.Id);
            var limits = custom.GetValueOrDefault(person.Id);
            var counts = Enum.GetValues<UsageKind>().Select(kind => new UsageItemDto(kind,
                kind == UsageKind.AiRequest ? ai.GetValueOrDefault(person.Id) : events.FirstOrDefault(x => x.UserId == person.Id && x.Kind == kind)?.Used ?? 0,
                UsageQuota.EffectiveLimit(kind, limits, options.Value, isAdmin))).ToList();
            return new AdminUsageDto(person.Id, person.Nickname, person.Email, isAdmin, new(end, counts.All(x => x.Limit is null), counts), limits, options.Value);
        }).ToList();
        return TypedResults.Ok(new PagedResult<AdminUsageDto>(items, page, 20, total));
    }

    private static async Task<AdminUsageDto?> Detail(Guid id, AppDbContext db, UsageLimiter limiter, UsageOptions options, CancellationToken ct)
    {
        var person = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Nickname, x.Email }).SingleOrDefaultAsync(ct);
        if (person is null) return null;
        var admin = await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                           where ur.UserId == id && r.Name == Roles.Admin select ur).AnyAsync(ct);
        var custom = await db.UserUsageLimits.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id, ct);
        return new(id, person.Nickname, person.Email, admin, await limiter.SummaryAsync(id, admin, ct), custom, options);
    }

    private static async Task<IResult> Get(Guid userId, AppDbContext db, UsageLimiter limiter, IOptions<UsageOptions> options, CancellationToken ct) =>
        await Detail(userId, db, limiter, options.Value, ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound();

    private static async Task<IResult> Update(Guid userId, UpdateUsageRequest request, ClaimsPrincipal user, AppDbContext db, UsageLimiter limiter, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct)) return TypedResults.NotFound();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            foreach (var kind in Enum.GetValues<UsageKind>()) await limiter.LockAsync(userId, kind, ct);
            var row = await db.UserUsageLimits.SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (row is null) db.UserUsageLimits.Add(row = new() { UserId = userId });
            row.MonthlyUploads = request.MonthlyUploads;
            row.MonthlyExams = request.MonthlyExams;
            row.MonthlyBattles = request.MonthlyBattles;
            row.MonthlyAiRequests = request.MonthlyAiRequests;
            row.Unlimited = request.Unlimited;
            row.UpdatedAt = clock.GetUtcNow();
            row.UpdatedByUserId = user.GetUserId();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (IResult)TypedResults.NoContent();
        });
    }

    private static async Task<IResult> Delete(Guid userId, AppDbContext db, UsageLimiter limiter, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct)) return TypedResults.NotFound();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            foreach (var kind in Enum.GetValues<UsageKind>()) await limiter.LockAsync(userId, kind, ct);
            await db.UserUsageLimits.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            return (IResult)TypedResults.NoContent();
        });
    }
}

using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Battles;

public static class BattleRankEndpoints
{
    public static void MapBattleRankEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/battle-rank").RequireAuthorization();
        group.MapGet("/me", async (ClaimsPrincipal user, AppDbContext db, IOptions<BattleRankOptions> options, CancellationToken ct) =>
            TypedResults.Ok(BattleRankEngine.Describe(await db.BattleRatings.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == user.GetUserId(), ct), options.Value)));
        group.MapGet("/players", async (string ids, AppDbContext db, IOptions<BattleRankOptions> options, CancellationToken ct) =>
        {
            var parts = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 10 || parts.Any(p => !Guid.TryParse(p, out _)))
                return (IResult)TypedResults.Problem("İştirakçı siyahısı etibarsızdır.", statusCode: 400);
            var userIds = parts.Select(Guid.Parse).Distinct().ToList();
            var activeIds = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && !u.IsDeleted).Select(u => u.Id).ToListAsync(ct);
            var profiles = await db.BattleRatings.AsNoTracking().Where(r => activeIds.Contains(r.UserId)).ToDictionaryAsync(r => r.UserId, ct);
            return TypedResults.Ok(activeIds.Select(id => new { UserId = id, Rank = BattleRankEngine.Describe(profiles.GetValueOrDefault(id), options.Value) }));
        });
        group.MapGet("/leaderboard", async (AppDbContext db, IOptions<BattleRankOptions> options, CancellationToken ct) =>
        {
            var rows = await db.BattleRatings.AsNoTracking().Where(r => !r.User.IsDeleted && r.TotalRankedBattles > 0)
                .OrderByDescending(r => r.CurrentRating).ThenByDescending(r => r.Wins).ThenBy(r => r.UserId).Take(100)
                .Select(r => new { r.UserId, r.User.Nickname, AvatarUrl = r.User.AvatarFileName == null ? null : $"/api/profile/avatar/{r.UserId}?v={r.User.AvatarFileName}", Profile = r }).ToListAsync(ct);
            return TypedResults.Ok(rows.Select(r => new { r.UserId, r.Nickname, r.AvatarUrl, Rank = BattleRankEngine.Describe(r.Profile, options.Value) }));
        });
    }
}

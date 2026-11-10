using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Nameplates;

public static class NameplateEndpoints
{
    public static void MapNameplateEndpoints(this RouteGroupBuilder api)
    {
        var mine = api.MapGroup("/profile/nameplate").RequireAuthorization();
        mine.MapGet("", GetMine);
        mine.MapPut("", Select).Validate<SelectNameplateRequest>();
        var admin = api.MapGroup("/admin/nameplates").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/{userId:guid}", GetUser);
        admin.MapPut("/{userId:guid}/{id}", SetGrant).Validate<NameplateGrantRequest>();
    }

    private static Task<IResult> GetMine(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        Read(principal.GetUserId(), db, ct);

    private static Task<IResult> GetUser(Guid userId, AppDbContext db, CancellationToken ct) => Read(userId, db, ct);

    private static async Task<IResult> Read(Guid userId, AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new { u.Nameplate, u.NameplateGrants }).SingleOrDefaultAsync(ct);
        if (user is null) return TypedResults.NotFound();
        var isAdmin = await IsAdmin(userId, db, ct);
        return TypedResults.Ok(NameplateCatalog.State(user.Nameplate, isAdmin, user.NameplateGrants));
    }

    private static async Task<IResult> Select(SelectNameplateRequest request, ClaimsPrincipal principal,
        AppDbContext db, CancellationToken ct)
    {
        var item = NameplateCatalog.Find(request.Id);
        if (request.Id is not null && item is null)
            return TypedResults.Problem("Belə profil effekti yoxdur.", statusCode: 400);
        var userId = principal.GetUserId();
        var permission = item?.Permission ?? 0;
        var adminRoles = db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id);
        var stamp = Guid.NewGuid().ToString();
        // Check permission in the write itself: a concurrent revoke cannot be bypassed by a stale read.
        var updated = await db.Users.Where(u => u.Id == userId && !u.IsDeleted &&
                (permission == 0 || (u.NameplateGrants & permission) != 0 ||
                 db.UserRoles.Any(r => r.UserId == u.Id && adminRoles.Contains(r.RoleId))))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Nameplate, request.Id)
                .SetProperty(u => u.ConcurrencyStamp, stamp), ct);
        if (updated == 0)
            return TypedResults.Problem("Bu effekt üçün admin icazəsi lazımdır.", statusCode: 403);
        return await Read(userId, db, ct);
    }

    private static async Task<IResult> SetGrant(Guid userId, string id, NameplateGrantRequest request,
        AppDbContext db, CancellationToken ct)
    {
        var item = NameplateCatalog.Find(id);
        if (item is null || item.Permission == 0)
            return TypedResults.Problem("Yalnız xüsusi effektlərin icazəsi dəyişdirilə bilər.", statusCode: 400);
        var permission = item.Permission;
        var grant = request.Granted == true;
        var adminRoles = db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id);
        var stamp = Guid.NewGuid().ToString();
        // One atomic update preserves other grants and clears a revoked active effect for ordinary users.
        var updated = await db.Users.Where(u => u.Id == userId && !u.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.NameplateGrants, u => grant ? u.NameplateGrants | permission : u.NameplateGrants & ~permission)
                .SetProperty(u => u.Nameplate, u => !grant && u.Nameplate == id &&
                    !db.UserRoles.Any(r => r.UserId == u.Id && adminRoles.Contains(r.RoleId)) ? null : u.Nameplate)
                .SetProperty(u => u.ConcurrencyStamp, stamp), ct);
        return updated == 0 ? TypedResults.NotFound() : await Read(userId, db, ct);
    }

    private static Task<bool> IsAdmin(Guid userId, AppDbContext db, CancellationToken ct) =>
        db.UserRoles.AnyAsync(ur => ur.UserId == userId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"), ct);
}

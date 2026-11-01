using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Auth;

/// <summary>Who granted or revoked the Admin role, for whom and when.</summary>
public sealed class AdminRoleAudit
{
    public long Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid TargetUserId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AdminRoleAuditConfiguration : IEntityTypeConfiguration<AdminRoleAudit>
{
    public void Configure(EntityTypeBuilder<AdminRoleAudit> builder)
    {
        builder.ToTable("AdminRoleAudits");
        builder.Property(a => a.Action).HasMaxLength(16);
        builder.HasIndex(a => a.TargetUserId);
    }
}

public sealed record AdminUserDto(Guid Id, string Nickname, string Email, string? AvatarUrl, bool IsAdmin);

public static class AdminEndpoints
{
    // Serializes grant/revoke inside this process so two admins cannot remove each other as the last ones.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admins = api.MapGroup("/admin/admins").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        admins.MapGet("/", List);
        admins.MapGet("/users", Search);
        admins.MapPost("/{userId:guid}", Grant);
        admins.MapDelete("/{userId:guid}", Revoke);
    }

    private static IQueryable<AdminUserDto> Project(IQueryable<AppUser> users, AppDbContext db, Guid adminRoleId) =>
        users.OrderBy(u => u.Nickname).ThenBy(u => u.Id).Select(u => new AdminUserDto(u.Id, u.Nickname, u.Email!,
            u.AvatarFileName == null ? null : $"/api/profile/avatar/{u.Id}?v={u.AvatarFileName}",
            db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId)));

    private static async Task<Guid> AdminRoleId(AppDbContext db, CancellationToken ct) =>
        await db.Roles.Where(r => r.NormalizedName == "ADMIN").Select(r => r.Id).FirstOrDefaultAsync(ct);

    private static async Task<IResult> List(AppDbContext db, CancellationToken ct)
    {
        var roleId = await AdminRoleId(db, ct);
        return TypedResults.Ok(await Project(db.Users.AsNoTracking().Where(u => db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == roleId)), db, roleId).ToListAsync(ct));
    }

    private static async Task<IResult> Search(string? q, AppDbContext db, CancellationToken ct)
    {
        var term = q?.Trim().ToUpperInvariant() ?? "";
        if (term.Length is < 2 or > 256) return TypedResults.Ok(Array.Empty<AdminUserDto>());
        var roleId = await AdminRoleId(db, ct);
        return TypedResults.Ok(await Project(db.Users.AsNoTracking()
            .Where(u => u.NormalizedNickname.Contains(term) || u.NormalizedEmail!.Contains(term)), db, roleId).Take(30).ToListAsync(ct));
    }

    private static Task<IResult> Grant(Guid userId, ClaimsPrincipal actor, UserManager<AppUser> users, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        Change(userId, grant: true, actor, users, db, clock, null, ct);

    private static Task<IResult> Revoke(Guid userId, ClaimsPrincipal actor, UserManager<AppUser> users, AppDbContext db, TimeProvider clock, IConfiguration configuration, CancellationToken ct) =>
        Change(userId, grant: false, actor, users, db, clock, configuration, ct);

    private static async Task<IResult> Change(Guid userId, bool grant, ClaimsPrincipal actor, UserManager<AppUser> users, AppDbContext db,
        TimeProvider clock, IConfiguration? configuration, CancellationToken ct)
    {
        var actorId = actor.GetUserId();
        if (!grant && userId == actorId) return TypedResults.Problem("Öz admin rolunuzu geri ala bilməzsiniz.", statusCode: 400);
        await Gate.WaitAsync(ct);
        try
        {
            var user = await users.FindByIdAsync(userId.ToString());
            if (user is null) return TypedResults.Problem("İstifadəçi tapılmadı.", statusCode: 404);
            var isAdmin = await users.IsInRoleAsync(user, Roles.Admin);
            if (grant == isAdmin) return TypedResults.NoContent();
            if (!grant)
            {
                if (ConfiguredAdmins.IsConfigured(configuration!, user))
                    return TypedResults.Problem("Bu hesabın admin rolu server konfiqurasiyası ilə qorunur.", statusCode: 409);
                if ((await users.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
                    return TypedResults.Problem("Sonuncu adminin rolunu geri almaq olmaz.", statusCode: 409);
            }
            var result = grant ? await users.AddToRoleAsync(user, Roles.Admin) : await users.RemoveFromRoleAsync(user, Roles.Admin);
            if (!result.Succeeded) return TypedResults.Problem("Rol dəyişdirilmədi. Yenidən cəhd edin.", statusCode: 500);
            db.AdminRoleAudits.Add(new AdminRoleAudit { ActorId = actorId, TargetUserId = userId, Action = grant ? "Grant" : "Revoke", CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }
        finally { Gate.Release(); }
    }
}

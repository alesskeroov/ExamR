using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Friends;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExamFlow.Api.Features.Groups;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this RouteGroupBuilder api)
    {
        var groups = api.MapGroup("/groups").RequireAuthorization();
        groups.MapGet("/", ListMine);
        groups.MapGet("/{id:guid}", GetGroup);
        groups.MapGet("/{id:guid}/members", Members);
        groups.MapGet("/{id:guid}/documents", Documents);
        var admin = api.MapGroup("/admin/groups").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        admin.MapGet("/", ListAll);
        admin.MapPost("/", Create).Validate<SaveGroupRequest>();
        admin.MapPut("/{id:guid}", Update).Validate<SaveGroupRequest>();
        admin.MapDelete("/{id:guid}", Delete);
        admin.MapGet("/{id:guid}/users", SearchUsers);
        admin.MapPost("/{id:guid}/members", AddMembers).Validate<AddGroupMembersRequest>();
        admin.MapDelete("/{id:guid}/members/{userId:guid}", RemoveMember);
    }

    private static IQueryable<GroupDto> Project(IQueryable<StudyGroup> query) => query.OrderByDescending(g => g.CreatedAt)
        .Select(g => new GroupDto(g.Id, g.Name, g.Description, g.CreatedAt, g.Members.Count));
    private static IQueryable<StudyGroup> Visible(AppDbContext db, ClaimsPrincipal user) => db.StudyGroups.AsNoTracking()
        .Where(g => user.IsAdmin() || g.Members.Any(m => m.UserId == user.GetUserId()));
    private static async Task<IResult> ListMine(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await Project(db.StudyGroups.AsNoTracking().Where(g => g.Members.Any(m => m.UserId == user.GetUserId()))).ToListAsync(ct));
    private static async Task<IResult> ListAll(AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await Project(db.StudyGroups.AsNoTracking()).ToListAsync(ct));
    private static async Task<IResult> GetGroup(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var group = await Project(Visible(db, user).Where(g => g.Id == id)).FirstOrDefaultAsync(ct);
        return group is null ? NotFound() : TypedResults.Ok(group);
    }
    private static async Task<IResult> Members(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var visible = Visible(db, user);
        if (!await visible.AnyAsync(g => g.Id == id, ct)) return NotFound();
        return TypedResults.Ok(await db.StudyGroupMembers.AsNoTracking()
            .Where(m => m.GroupId == id && visible.Any(g => g.Id == m.GroupId))
            .OrderBy(m => m.User.Nickname).ThenBy(m => m.UserId)
            .Select(m => new GroupMemberDto(m.UserId, m.User.Nickname, m.User.AvatarFileName == null ? null
                : $"/api/profile/avatar/{m.UserId}?v={m.User.AvatarFileName}")).ToListAsync(ct));
    }
    private static async Task<IResult> Documents(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var visible = Visible(db, user);
        if (!await visible.AnyAsync(g => g.Id == id, ct)) return NotFound();
        return TypedResults.Ok(await db.DocumentGroupShares.AsNoTracking()
            .Where(s => s.GroupId == id && visible.Any(g => g.Id == s.GroupId))
            .Join(db.Documents.Where(d => d.Status == DocumentStatus.Completed), s => s.DocumentId, d => d.Id, (s, d) => d)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new GroupDocumentDto(d.Id, d.OriginalFileName, d.Questions.Count)).ToListAsync(ct));
    }
    private static IResult NotFound() => TypedResults.Problem("Qrup tapılmadı.", statusCode: 404);

    // Serialize membership changes; the unique indexes remain the final safeguard against races.
    private static async Task<IResult> Atomic(AppDbContext db, Func<Task<IResult>> action, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await action();
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return TypedResults.Problem("Bu ad və ya üzvlük artıq mövcuddur. Siyahını yeniləyin.", statusCode: 409);
        }
    }
    private static (string Name, string Key) Names(string name)
    {
        var trimmed = Regex.Replace(name.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ");
        return (trimmed, trimmed.ToUpper(CultureInfo.GetCultureInfo("az-Latn-AZ")));
    }
    private static Task<IResult> Create(SaveGroupRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        return Save(id, request, true, db, clock, ct);
    }
    private static Task<IResult> Update(Guid id, SaveGroupRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        Save(id, request, false, db, clock, ct);
    private static Task<IResult> Save(Guid id, SaveGroupRequest request, bool create, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        Atomic(db, async () =>
        {
            var (name, key) = Names(request.Name);
            if (name.Length is < 1 or > 120 || key.Length > 120)
                return TypedResults.Problem("Qrupun adı 1–120 simvol olmalıdır.", statusCode: 400);
            if (await db.StudyGroups.AnyAsync(g => g.NormalizedName == key && g.Id != id, ct))
                return TypedResults.Problem("Bu adda qrup artıq mövcuddur.", statusCode: 409);
            var group = create ? new StudyGroup { Id = id, CreatedAt = clock.GetUtcNow() }
                : await db.StudyGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
            if (group is null) return NotFound();
            group.Name = name;
            group.NormalizedName = key;
            group.Description = request.Description.Trim();
            if (create) db.StudyGroups.Add(group);
            return TypedResults.Ok(new { group.Id });
        }, ct);
    private static Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct) => Atomic(db, async () =>
    {
        var count = await db.StudyGroups.Where(g => g.Id == id).ExecuteDeleteAsync(ct);
        return count == 0 ? NotFound() : TypedResults.NoContent();
    }, ct);
    private static async Task<IResult> SearchUsers(Guid id, string? q, AppDbContext db, CancellationToken ct)
    {
        if (!await db.StudyGroups.AsNoTracking().AnyAsync(g => g.Id == id, ct)) return NotFound();
        var term = q?.Trim() ?? "";
        if (term.Length is < 2 or > 24) return TypedResults.Ok(Array.Empty<GroupSearchUserDto>());
        var normalized = term.ToUpperInvariant();
        return TypedResults.Ok(await db.Users.AsNoTracking().Where(u => u.NormalizedNickname.Contains(normalized))
            .OrderBy(u => u.Nickname).ThenBy(u => u.Id).Take(30)
            .Select(u => new GroupSearchUserDto(u.Id, u.Nickname, u.AvatarFileName == null ? null
                : $"/api/profile/avatar/{u.Id}?v={u.AvatarFileName}", db.StudyGroupMembers.Any(m => m.GroupId == id && m.UserId == u.Id))).ToListAsync(ct));
    }
    private static async Task<IResult> AddMembers(Guid id, AddGroupMembersRequest request, ClaimsPrincipal user, AppDbContext db,
        SocialEvents events, TimeProvider clock, ILoggerFactory loggers, CancellationToken ct)
    {
        var added = new List<Guid>();
        var actor = user.GetUserId();
        var result = await Atomic(db, async () =>
        {
            added.Clear();
            var group = await db.StudyGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
            if (group is null) return NotFound();
            var ids = request.UserIds.Distinct().ToArray();
            if (await db.Users.CountAsync(u => ids.Contains(u.Id), ct) != ids.Length)
                return TypedResults.Problem("Seçilmiş istifadəçilərdən biri tapılmadı. Axtarışı yeniləyin.", statusCode: 400);
            var existing = await db.StudyGroupMembers.Where(m => m.GroupId == id && ids.Contains(m.UserId)).Select(m => m.UserId).ToListAsync(ct);
            foreach (var target in ids.Except(existing).Order())
            {
                db.StudyGroupMembers.Add(new StudyGroupMember { GroupId = id, UserId = target });
                events.QueueGroupAdded(db, target, actor, group.Name, clock.GetUtcNow());
                added.Add(target);
            }
            return TypedResults.NoContent();
        }, ct);
        // Notifications are durable even when the live connection is temporarily unavailable.
        if (result is Microsoft.AspNetCore.Http.HttpResults.NoContent)
            foreach (var target in added)
                try { await events.ChangedAsync(target, ct); }
                catch (Exception ex) { loggers.CreateLogger("StudyGroups").LogWarning(ex, "Group notification refresh failed"); }
        return result;
    }
    private static Task<IResult> RemoveMember(Guid id, Guid userId, AppDbContext db, CancellationToken ct) => Atomic(db, async () =>
    {
        if (!await db.StudyGroups.AnyAsync(g => g.Id == id, ct)) return NotFound();
        await db.StudyGroupMembers.Where(m => m.GroupId == id && m.UserId == userId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }, ct);
}

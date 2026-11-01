using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Documents;

public static class DocumentSharingEndpoints
{
    public static void MapDocumentSharingEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/documents").RequireAuthorization(p => p.RequireRole(Roles.Admin));
        group.MapGet("/users", Search);
        group.MapGet("/{id:guid}/sharing", Get);
        group.MapPut("/{id:guid}/sharing", Save).Validate<SaveDocumentSharing>();
    }
    private static async Task<IResult> Search(string? q, AppDbContext db, CancellationToken ct)
    {
        var term = q?.Trim().ToUpperInvariant() ?? "";
        if (term.Length is < 2 or > 24) return TypedResults.Ok(Array.Empty<SharingUser>());
        return TypedResults.Ok(await db.Users.AsNoTracking().Where(u => u.NormalizedNickname.Contains(term))
            .OrderBy(u => u.Nickname).Take(30).Select(u => new SharingUser(u.Id, u.Nickname)).ToListAsync(ct));
    }
    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var document = await db.Documents.AsNoTracking().Where(d => d.Id == id && d.UploadedByUserId == user.GetUserId())
            .Select(d => new { d.ShareWithEveryone }).FirstOrDefaultAsync(ct);
        if (document is null) return Missing();
        var users = await db.DocumentUserShares.AsNoTracking().Where(s => s.DocumentId == id)
            .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new SharingUser(u.Id, u.Nickname)).ToListAsync(ct);
        var groups = await db.DocumentGroupShares.AsNoTracking().Where(s => s.DocumentId == id).Select(s => s.GroupId).ToListAsync(ct);
        return TypedResults.Ok(new { document.ShareWithEveryone, Users = users, GroupIds = groups });
    }
    private static Task<IResult> Save(Guid id, SaveDocumentSharing request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync<IResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UploadedByUserId == user.GetUserId(), ct);
            if (document is null) return Missing();
            var users = request.UserIds.Distinct().ToArray();
            var groups = request.GroupIds.Distinct().ToArray();
            if (await db.Users.CountAsync(u => users.Contains(u.Id), ct) != users.Length
                || await db.StudyGroups.CountAsync(g => groups.Contains(g.Id), ct) != groups.Length)
                return TypedResults.Problem("Seçilmiş istifadəçi və ya qrup artıq mövcud deyil. Siyahını yeniləyin.", statusCode: 400);
            document.ShareWithEveryone = request.ShareWithEveryone;
            await db.DocumentUserShares.Where(s => s.DocumentId == id).ExecuteDeleteAsync(ct);
            await db.DocumentGroupShares.Where(s => s.DocumentId == id).ExecuteDeleteAsync(ct);
            db.DocumentUserShares.AddRange(users.Select(userId => new DocumentUserShare { DocumentId = id, UserId = userId }));
            db.DocumentGroupShares.AddRange(groups.Select(groupId => new DocumentGroupShare { DocumentId = id, GroupId = groupId }));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return TypedResults.NoContent();
        });
    private static IResult Missing() => TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
}
public sealed record SharingUser(Guid Id, string Nickname);
public sealed record SaveDocumentSharing(bool ShareWithEveryone,
    [property: Required(ErrorMessage = "İstifadəçi siyahısı tələb olunur."), MaxLength(100, ErrorMessage = "Ən çox 100 istifadəçi seçin.")] Guid[] UserIds,
    [property: Required(ErrorMessage = "Qrup siyahısı tələb olunur."), MaxLength(100, ErrorMessage = "Ən çox 100 qrup seçin.")] Guid[] GroupIds);

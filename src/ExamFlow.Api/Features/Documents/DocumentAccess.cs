using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Documents;

public static class DocumentAccess
{
    public static IQueryable<UploadedDocument> Readable(AppDbContext db, Guid userId) => db.Documents.AsNoTracking()
        .Where(d => d.UploadedByUserId == userId || d.ShareWithEveryone
            || db.DocumentUserShares.Any(s => s.DocumentId == d.Id && s.UserId == userId)
            || db.DocumentGroupShares.Any(s => s.DocumentId == d.Id
                && db.StudyGroupMembers.Any(m => m.GroupId == s.GroupId && m.UserId == userId)));

    public static IQueryable<UploadedDocument> ForBattle(AppDbContext db, Guid userId) => db.Documents.AsNoTracking()
        .Where(d => d.UploadedByUserId == userId && !d.ShareWithEveryone
            && !db.DocumentUserShares.Any(s => s.DocumentId == d.Id) && !db.DocumentGroupShares.Any(s => s.DocumentId == d.Id)
            && !db.UserRoles.Any(r => r.UserId == d.UploadedByUserId && db.Roles.Any(role => role.Id == r.RoleId && role.NormalizedName == "ADMIN")));
}

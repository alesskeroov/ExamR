using System.Security.Claims;

namespace ExamFlow.Api.Infrastructure.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtClaims.UserId)
            ?? throw new InvalidOperationException("Authenticated user has no id claim."));

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    /// <summary>Ownership rule (AUTH-3): the uploader or an Admin may change a document and its questions.</summary>
    public static bool CanEdit(this ClaimsPrincipal user, Guid ownerId) => user.IsAdmin() || user.GetUserId() == ownerId;
}

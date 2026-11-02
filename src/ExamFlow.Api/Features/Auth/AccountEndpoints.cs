using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Email;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Auth;

public static class AccountEndpoints
{
    private const string DeletePurpose = "ExamR.DeleteAccount";
    public static void MapAccountEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/account").RequireAuthorization();
        group.MapPost("/password", ChangePassword).Validate<ChangePasswordRequest>().RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapPost("/deletion-link", DeletionLink).RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapDelete("/", DeleteAccount).Validate<DeleteAccountRequest>().RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
    }

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, ClaimsPrincipal principal,
        UserManager<AppUser> users, EmailQueue email, IConfiguration configuration, HttpResponse response)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null || user.IsDeleted) return TypedResults.Unauthorized();
        if (!await users.HasPasswordAsync(user))
            return TypedResults.Problem("Şifrə təyin etmək üçün email ünvanınıza bərpa linki göndərin.", statusCode: 400);
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return TypedResults.Problem("Şifrə dəyişdirilmədi. Cari şifrəni və yeni şifrə qaydalarını yoxlayın.", statusCode: 400);
        email.Enqueue(EmailTemplates.SecurityNotice(user.Email!, "Şifrəniz dəyişdirildi. Bu əməliyyatı siz etməmisinizsə, şifrənizi bərpa edin.", configuration["App:FrontendBaseUrl"] ?? "/"));
        // Identity changes SecurityStamp; all previous JWTs (including this session) are now invalid.
        AuthCookie.Delete(response);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeletionLink(ClaimsPrincipal principal, UserManager<AppUser> users,
        EmailQueue email, IConfiguration configuration)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null || user.IsDeleted) return TypedResults.Unauthorized();
        if (configuration["App:FrontendBaseUrl"] is not { Length: > 0 } frontend)
            return TypedResults.Problem("Təsdiq məktubu hazırda göndərilə bilmir.", statusCode: 503);
        var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, DeletePurpose);
        var link = $"{frontend.TrimEnd('/')}/profile?tab=4&deleteToken={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}&uid={user.Id}";
        email.Enqueue(EmailTemplates.AccountDeletion(user.Email!, link));
        return TypedResults.Ok(new MessageResponse("Hesabı bağlamaq üçün 30 dəqiqə etibarlı təsdiq linki email ünvanınıza göndərildi."));
    }

    private static async Task<IResult> DeleteAccount([FromBody] DeleteAccountRequest request, ClaimsPrincipal principal,
        UserManager<AppUser> users, AppDbContext db, HttpResponse response, CancellationToken ct)
    {
        if (request.Confirmation != "SİL") return TypedResults.Problem("Təsdiq üçün SİL yazın.", statusCode: 400);
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { return TypedResults.Problem("Təsdiq linki etibarsızdır.", statusCode: 400); }
        var id = principal.GetUserId();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null || user.IsDeleted) return TypedResults.Unauthorized();
            if (!await users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, DeletePurpose, token))
                return TypedResults.Problem("Təsdiq linki etibarsızdır və ya vaxtı bitib.", statusCode: 400);
            if (await users.IsInRoleAsync(user, Roles.Admin))
            {
                var otherAdmin = await (from role in db.Roles join membership in db.UserRoles on role.Id equals membership.RoleId
                    join other in db.Users on membership.UserId equals other.Id
                    where role.Name == Roles.Admin && other.Id != id && !other.IsDeleted select other.Id).AnyAsync(ct);
                if (!otherAdmin) return TypedResults.Problem("Son administrator hesabı bağlana bilməz. Əvvəlcə başqa administrator təyin edin.", statusCode: 409);
            }
            user.IsDeleted = true; user.DeletedAt = DateTimeOffset.UtcNow;
            user.SecurityStamp = Guid.NewGuid().ToString(); user.PasswordHash = null;
            user.DisplayName = "Silinmiş istifadəçi"; user.Nickname = $"deleted_{id:N}"[..24]; user.NormalizedNickname = user.Nickname.ToUpperInvariant();
            user.Bio = null; user.AvatarFileName = null; user.AvatarContentType = null;
            user.Email = $"deleted_{id:N}@deleted.invalid"; user.UserName = user.Email; user.EmailConfirmed = false;
            user.PhoneNumber = null; user.PhoneNumberConfirmed = false;
            var result = await users.UpdateAsync(user);
            if (!result.Succeeded) return TypedResults.Problem("Hesab bağlana bilmədi.", statusCode: 409);
            await transaction.CommitAsync(ct);
            AuthCookie.Delete(response);
            return TypedResults.NoContent();
        });
    }
}

public sealed class ChangePasswordRequest
{
    [Required, MaxLength(128)] public string CurrentPassword { get; init; } = "";
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; init; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "Yeni şifrələr uyğun gəlmir.")]
    public string ConfirmPassword { get; init; } = "";
}
public sealed class DeleteAccountRequest
{
    [Required, MaxLength(2048)] public string Token { get; init; } = "";
    [Required, MaxLength(3)] public string Confirmation { get; init; } = "";
}

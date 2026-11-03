using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Email;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;

namespace ExamFlow.Api.Features.Auth;

public static class AuthEndpoints
{
    /// <summary>Same answer whether or not the email exists, so accounts cannot be discovered.</summary>
    private const string ForgotPasswordMessage =
        "Əgər bu email ilə hesab varsa, şifrəni yeniləmək üçün link göndərildi. Link 30 dəqiqə etibarlıdır.";

    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        group.MapPost("/register", Register).Validate<RegisterRequest>().RequireRateLimiting(AuthenticationSetup.AuthRateLimit);
        group.MapGet("/confirm-admin", ConfirmAdmin).RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapPost("/login", Login).Validate<LoginRequest>().RequireRateLimiting(AuthenticationSetup.AuthRateLimit);
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPut("/profile", UpdateProfile).Validate<UpdateProfileRequest>().RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPassword).Validate<ForgotPasswordRequest>()
            .RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapPost("/reset-password", ResetPassword).Validate<ResetPasswordRequest>()
            .RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapPost("/confirm-email", ConfirmEmail).Validate<ConfirmEmailRequest>()
            .RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapPost("/resend-confirmation", ResendConfirmation).Validate<ForgotPasswordRequest>()
            .RequireRateLimiting(AuthenticationSetup.VerificationRateLimit);
    }

    private const string ResendMessage = "Əgər bu email ilə təsdiqlənməmiş hesab varsa, təsdiq linki göndərildi.";

    // Links always point at the configured frontend and carry only server-generated, URL-safe values (no open redirect).
    private static string? FrontendLink(IConfiguration configuration, string path) =>
        configuration["App:FrontendBaseUrl"] is { Length: > 0 } frontend ? $"{frontend.TrimEnd('/')}{path}" : null;

    private static async Task<string?> VerifyLink(UserManager<AppUser> users, IConfiguration configuration, AppUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        return FrontendLink(configuration, $"/verify-email?uid={user.Id}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}");
    }

    private static async Task<IResult> ConfirmEmail(ConfirmEmailRequest request, UserManager<AppUser> users, EmailQueue email, IConfiguration configuration,
        TokenService tokens, HttpResponse response)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.IsDeleted) return InvalidVerifyLink();
        // Already confirmed: no session is issued here, the link cannot be reused to sign in.
        if (user.EmailConfirmed) return TypedResults.Ok(new ConfirmEmailResponse("Email artıq təsdiqlənib.", null));
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { return InvalidVerifyLink(); }
        // Validates signature, 24-hour lifetime and the security stamp; a confirmed account rejects reuse above.
        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return InvalidVerifyLink();
        // Sent only on the unconfirmed → confirmed transition: a confirmed account returns early above, and a parallel
        // second click fails ConfirmEmailAsync on the concurrency stamp, so the welcome goes out once.
        if (FrontendLink(configuration, "/") is { } appUrl) email.Enqueue(EmailTemplates.Welcome(user.Email!, user.DisplayName, appUrl));
        // The single-use link proves ownership of the address, so the first confirmation also signs the user in
        // (browser-session cookie, as without Remember Me).
        var session = await IssueToken(user, users, tokens, configuration, response);
        return TypedResults.Ok(new ConfirmEmailResponse("Email ünvanınız təsdiqləndi.", session.User));
    }

    private static IResult InvalidVerifyLink() =>
        TypedResults.Problem("Təsdiq linki etibarsızdır və ya vaxtı bitib. Yeni link tələb et.", statusCode: StatusCodes.Status400BadRequest);

    // Same answer whether or not the account exists, so the endpoint cannot be used to discover registered emails.
    private static async Task<IResult> ResendConfirmation(ForgotPasswordRequest request, UserManager<AppUser> users, EmailQueue email, IConfiguration configuration)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is { EmailConfirmed: false, IsDeleted: false } && await VerifyLink(users, configuration, user) is { } link)
            email.Enqueue(EmailTemplates.Verify(user.Email!, user.DisplayName, link));
        return TypedResults.Ok(new MessageResponse(ResendMessage));
    }

    private static async Task<IResult> Register(RegisterRequest request, UserManager<AppUser> users, IConfiguration configuration, EmailQueue email)
    {
        if (await users.FindByEmailAsync(request.Email.Trim()) is not null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Bu e-mail ilə artıq hesab var. Daxil olun və ya şifrənizi bərpa edin."],
            });

        var fallbackNickname = $"user{Guid.NewGuid():N}"[..16];
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
            Nickname = fallbackNickname,
            NormalizedNickname = fallbackNickname.ToUpperInvariant(),
        };

        var created = await users.CreateAsync(user, request.Password); // Identity stores a salted PBKDF2 hash.
        if (!created.Succeeded)
        {
            return IdentityErrors(created);
        }

        await users.AddToRoleAsync(user, Roles.User);
        await ConfiguredAdmins.SendConfirmationAsync(users, configuration, email, user);
        // No session yet: the account is usable only after the emailed link confirms the address. Queued, so a
        // provider outage never fails the registration; the user can request the link again.
        if (await VerifyLink(users, configuration, user) is { } link) email.Enqueue(EmailTemplates.Verify(user.Email!, user.DisplayName, link));
        return TypedResults.Ok(new RegisterResponse(user.Email!, "Təsdiqləmə linki e-mail ünvanınıza göndərildi."));
    }

    private static async Task<IResult> Login(
        LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, TokenService tokens, IConfiguration configuration, EmailQueue email, HttpResponse response)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || user.IsDeleted)
        {
            return InvalidCredentials();
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.Problem("Çoxlu uğursuz cəhd. Bir neçə dəqiqə sonra yenidən yoxlayın.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (!result.Succeeded) return InvalidCredentials();
        // Confirmed address required (Email:RequireConfirmedEmail, on by default). Google sign-in already proves ownership
        // of the address, so Google-linked accounts pass without changing their stored EmailConfirmed flag.
        if (configuration.GetValue("Email:RequireConfirmedEmail", true) && !user.EmailConfirmed
            && !(await users.GetLoginsAsync(user)).Any(l => l.LoginProvider == GoogleAuthEndpoints.Scheme))
            return TypedResults.Problem("E-mail ünvanınız hələ təsdiqlənməyib. Gələnlər qutusundakı linkə keçin və ya yeni link istəyin.",
                statusCode: StatusCodes.Status403Forbidden, extensions: new Dictionary<string, object?> { ["code"] = "email_not_confirmed" });
        await ConfiguredAdmins.SendConfirmationAsync(users, configuration, email, user);
        return TypedResults.Ok(await IssueToken(user, users, tokens, configuration, response, request.RememberMe));
    }

    private static async Task<IResult> ConfirmAdmin(Guid userId, string token, UserManager<AppUser> users, IConfiguration configuration)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !ConfiguredAdmins.IsConfigured(configuration, user)) return InvalidResetLink();
        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { return InvalidResetLink(); }
        var result = await users.ConfirmEmailAsync(user, decoded);
        if (!result.Succeeded) return InvalidResetLink();
        await ConfiguredAdmins.EnsureAsync(users, configuration, user);
        return TypedResults.Content("""<html lang="az"><meta charset="utf-8"><p>Email təsdiqləndi. Admin rolunu aktivləşdirmək üçün hesabdan çıxıb yenidən daxil olun.</p><a href="/">ExamR-ə qayıt</a></html>""", "text/html; charset=utf-8");
    }

    /// <summary>JWTs cannot be deleted, so logout rotates the security stamp: every token issued before is rejected.</summary>
    private static async Task<IResult> Logout(ClaimsPrincipal principal, UserManager<AppUser> users, HttpResponse response)
    {
        AuthCookie.Delete(response);
        if (await users.GetUserAsync(principal) is { } user)
        {
            await users.UpdateSecurityStampAsync(user);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await ToUser(user, users));
    }

    private static async Task<IResult> UpdateProfile(UpdateProfileRequest request, ClaimsPrincipal principal,
        UserManager<AppUser> users, CancellationToken ct)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null || user.IsDeleted) return TypedResults.Unauthorized();
        var nickname = request.Nickname.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(nickname, "^[A-Za-z0-9_]{3,24}$"))
            return TypedResults.Problem("Nickname 3–24 simvol olmalı; yalnız hərf, rəqəm və _ istifadə edin.", statusCode: 400);
        var normalized = nickname.ToUpperInvariant();
        if (await users.Users.AnyAsync(u => u.Id != user.Id && u.NormalizedNickname == normalized, ct))
            return TypedResults.Problem("Bu nickname artıq istifadə olunur.", statusCode: 409);
        user.Nickname = nickname;
        user.NormalizedNickname = normalized;
        if (request.Bio is not null) user.Bio = request.Bio.Trim();
        IdentityResult result;
        try { result = await users.UpdateAsync(user); }
        catch (DbUpdateException) { return TypedResults.Problem("Bu nickname artıq istifadə olunur.", statusCode: 409); }
        if (!result.Succeeded) return TypedResults.Problem("Profil yadda saxlanmadı.", statusCode: 400);
        return TypedResults.Ok(await ToUser(user, users));
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request, UserManager<AppUser> users, EmailQueue email,
        IConfiguration configuration, ILogger<ForgotPasswordRequest> logger)
    {
        var user = await users.FindByEmailAsync(request.Email);
        var frontend = configuration["App:FrontendBaseUrl"];

        if (user is not null && string.IsNullOrWhiteSpace(frontend))
        {
            logger.LogError("App:FrontendBaseUrl is not configured; password reset email was not sent");
        }
        else if (user is { IsDeleted: false })
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = $"{frontend!.TrimEnd('/')}/reset-password?uid={user.Id}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}";
            email.Enqueue(EmailTemplates.PasswordReset(user.Email!, user.DisplayName, link));
        }

        return TypedResults.Ok(new MessageResponse(ForgotPasswordMessage));
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, UserManager<AppUser> users, EmailQueue email, IConfiguration configuration)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return InvalidResetLink();
        }

        if (user is null || user.IsDeleted)
        {
            return InvalidResetLink();
        }

        // Validates the token (signature, 30-minute lifetime, security stamp) and the new password rules.
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? InvalidResetLink()
                : IdentityErrors(result);
        }

        // A successful reset also lifts a lockout caused by wrong passwords.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        email.Enqueue(EmailTemplates.SecurityNotice(user.Email!, "Şifrəniz bərpa edildi. Bu əməliyyatı siz etməmisinizsə, hesabınızı yoxlayın.", configuration["App:FrontendBaseUrl"] ?? "/"));
        return TypedResults.NoContent();
    }

    private static async Task<AuthResponse> IssueToken(AppUser user, UserManager<AppUser> users, TokenService tokens, IConfiguration configuration, HttpResponse response, bool rememberMe = false)
    {
        await ConfiguredAdmins.EnsureAsync(users, configuration, user);
        var roles = await users.GetRolesAsync(user);
        var token = tokens.Create(user, roles, rememberMe);
        // The JWT travels only in the HttpOnly cookie; the body carries no token for scripts to read.
        AuthCookie.Append(response, token, rememberMe);
        return new AuthResponse(token.ExpiresAt, await ToUser(user, users));
    }

    private static async Task<UserResponse> ToUser(AppUser user, UserManager<AppUser> users) =>
        new(user.Id, user.Email!, user.DisplayName, user.Nickname, AvatarUrl(user), (await users.GetRolesAsync(user)).ToArray(),
            (await users.GetLoginsAsync(user)).Any(login => login.LoginProvider == GoogleAuthEndpoints.Scheme), user.EmailConfirmed, user.Bio, await users.HasPasswordAsync(user));

    private static string? AvatarUrl(AppUser user) => user.AvatarFileName is null ? null : $"/api/profile/avatar/{user.Id}?v={user.AvatarFileName}";

    private static IResult IdentityErrors(IdentityResult result) =>
        TypedResults.ValidationProblem(result.Errors
            .GroupBy(e => e.Code.Contains("Password") ? "password" : e.Code.Contains("Email") || e.Code.Contains("UserName") ? "email" : string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

    private static IResult InvalidCredentials() =>
        TypedResults.Problem("Email və ya şifrə yanlışdır.", statusCode: StatusCodes.Status401Unauthorized);

    private static IResult InvalidResetLink() =>
        TypedResults.Problem("Link etibarsızdır və ya vaxtı bitib. Yeni link tələb edin.", statusCode: StatusCodes.Status400BadRequest);
}

public sealed record RegisterResponse(string Email, string Message);
public sealed record ConfirmEmailResponse(string Message, UserResponse? User);

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required, MaxLength(2048)]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    [MaxLength(160, ErrorMessage = "Bio maksimum 160 simvol ola bilər.")]
    public string? Bio { get; init; }
    [Required, MinLength(3), MaxLength(24)]
    public string Nickname { get; init; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string Email, string DisplayName, string Nickname, string? AvatarUrl, string[] Roles, bool GoogleLinked = false, bool EmailConfirmed = false, string? Bio = null, bool HasPassword = true);

public sealed record AuthResponse(DateTimeOffset ExpiresAt, UserResponse User);

public sealed record MessageResponse(string Message);

public sealed class ConfirmEmailRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required, MaxLength(2048)]
    public string Token { get; init; } = string.Empty;
}

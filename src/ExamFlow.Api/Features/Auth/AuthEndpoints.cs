using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Email;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
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
        group.MapPost("/login", Login).Validate<LoginRequest>().RequireRateLimiting(AuthenticationSetup.AuthRateLimit);
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPassword).Validate<ForgotPasswordRequest>()
            .RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
        group.MapPost("/reset-password", ResetPassword).Validate<ResetPasswordRequest>()
            .RequireRateLimiting(AuthenticationSetup.PasswordResetRateLimit);
    }

    private static async Task<IResult> Register(RegisterRequest request, UserManager<AppUser> users, TokenService tokens)
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
        };

        var created = await users.CreateAsync(user, request.Password); // Identity stores a salted PBKDF2 hash.
        if (!created.Succeeded)
        {
            return IdentityErrors(created);
        }

        await users.AddToRoleAsync(user, Roles.User);
        return TypedResults.Ok(await IssueToken(user, users, tokens));
    }

    private static async Task<IResult> Login(
        LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, TokenService tokens)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.Problem("Çoxlu uğursuz cəhd. Bir neçə dəqiqə sonra yenidən yoxlayın.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        return result.Succeeded ? TypedResults.Ok(await IssueToken(user, users, tokens)) : InvalidCredentials();
    }

    /// <summary>JWTs cannot be deleted, so logout rotates the security stamp: every token issued before is rejected.</summary>
    private static async Task<IResult> Logout(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
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
        else if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = $"{frontend!.TrimEnd('/')}/reset-password?uid={user.Id}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}";
            email.Enqueue(new EmailMessage(user.Email!, "ExamFlow — şifrənin yenilənməsi", ResetEmailBody(user.DisplayName, link)));
        }

        return TypedResults.Ok(new MessageResponse(ForgotPasswordMessage));
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, UserManager<AppUser> users)
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

        if (user is null)
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
        return TypedResults.NoContent();
    }

    private static async Task<AuthResponse> IssueToken(AppUser user, UserManager<AppUser> users, TokenService tokens)
    {
        var roles = await users.GetRolesAsync(user);
        var token = tokens.Create(user, roles);
        return new AuthResponse(token.Token, token.ExpiresAt, new UserResponse(user.Id, user.Email!, user.DisplayName, roles.ToArray()));
    }

    private static async Task<UserResponse> ToUser(AppUser user, UserManager<AppUser> users) =>
        new(user.Id, user.Email!, user.DisplayName, (await users.GetRolesAsync(user)).ToArray());

    private static string ResetEmailBody(string displayName, string link)
    {
        var name = WebUtility.HtmlEncode(displayName);
        var href = WebUtility.HtmlEncode(link);
        return $"""
            <div style="font-family:Arial,sans-serif;font-size:15px;color:#0f172a;max-width:480px">
              <p>Salam, {name}!</p>
              <p>ExamFlow hesabınız üçün şifrənin yenilənməsi tələb olundu.</p>
              <p><a href="{href}" style="display:inline-block;background:#4f46e5;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">Yeni şifrə təyin et</a></p>
              <p style="color:#64748b">Link 30 dəqiqə etibarlıdır və yalnız bir dəfə istifadə oluna bilər. Bu tələbi siz etməmisinizsə, bu emaili nəzərə almayın.</p>
            </div>
            """;
    }

    private static IResult IdentityErrors(IdentityResult result) =>
        TypedResults.ValidationProblem(result.Errors
            .GroupBy(e => e.Code.Contains("Password") ? "password" : e.Code.Contains("Email") || e.Code.Contains("UserName") ? "email" : string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

    private static IResult InvalidCredentials() =>
        TypedResults.Problem("Email və ya şifrə yanlışdır.", statusCode: StatusCodes.Status401Unauthorized);

    private static IResult InvalidResetLink() =>
        TypedResults.Problem("Link etibarsızdır və ya vaxtı bitib. Yeni link tələb edin.", statusCode: StatusCodes.Status400BadRequest);
}

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

public sealed record UserResponse(Guid Id, string Email, string DisplayName, string[] Roles);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed record MessageResponse(string Message);

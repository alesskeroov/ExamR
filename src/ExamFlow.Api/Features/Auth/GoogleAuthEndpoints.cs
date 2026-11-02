using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ExamFlow.Api.Features.Auth;

/// <summary>
/// "Continue with Google" on top of Identity external logins. The Google handler performs the OAuth code flow
/// (state + PKCE + correlation cookie) and validates the code server-side; this class only maps the result to an
/// ExamR account and issues the usual HttpOnly session cookie.
/// </summary>
public static class GoogleAuthEndpoints
{
    public const string Scheme = "Google";
    private const string Remember = "examr.remember";
    private const string LinkUser = "examr.link";
    public const string LinkUserKey = LinkUser;

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]) && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    public static void MapGoogleAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth/google");
        group.MapGet("/", Start).RequireRateLimiting(AuthenticationSetup.AuthRateLimit);
        group.MapGet("/complete", Complete);
    }

    private static IResult Fail(string code, bool link = false) => TypedResults.Redirect(link ? $"/profile?google={code}" : $"/?authError={code}");

    private static IResult Start(ClaimsPrincipal user, IConfiguration configuration, bool rememberMe = false, bool link = false)
    {
        if (!IsConfigured(configuration)) return Fail("google_disabled", link);
        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/complete" };
        properties.Items[Remember] = rememberMe ? "1" : "0";
        if (link)
        {
            // Linking needs an existing ExamR session, i.e. the account owner has already proved who they are.
            if (user.Identity?.IsAuthenticated != true) return Fail("login_required", link: true);
            properties.Items[LinkUser] = user.GetUserId().ToString();
        }
        return TypedResults.Challenge(properties, [Scheme]);
    }

    private static async Task<IResult> Complete(HttpContext context, UserManager<AppUser> users, TokenService tokens)
    {
        var external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        var linkUserId = external.Properties?.Items.TryGetValue(LinkUser, out var id) == true ? id : null;
        var subject = external.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!external.Succeeded || string.IsNullOrEmpty(subject)) return Fail("google_failed", linkUserId is not null);

        var email = external.Principal!.FindFirstValue(ClaimTypes.Email);
        var verified = string.Equals(external.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        var owner = await users.FindByLoginAsync(Scheme, subject);

        if (linkUserId is not null)
        {
            // linkUserId comes from the protected OAuth state that an authenticated ExamR session created in this
            // browser (bound by the correlation cookie). The SameSite=Strict session cookie is not sent on the return
            // from Google, so it cannot be re-checked here.
            if (owner is not null) return Fail(owner.Id.ToString() == linkUserId ? "linked" : "google_in_use", link: true);
            var current = await users.FindByIdAsync(linkUserId);
            if (current is null || current.IsDeleted) return Fail("login_required", link: true);
            var linked = await users.AddLoginAsync(current, new UserLoginInfo(Scheme, subject, "Google"));
            return Fail(linked.Succeeded ? "linked" : "google_failed", link: true);
        }

        if (owner is null)
        {
            if (string.IsNullOrEmpty(email) || !verified) return Fail("google_unverified");
            // Never merge by email: the owner must sign in with the password and link Google from the profile page.
            if (await users.FindByEmailAsync(email) is not null) return Fail("account_exists");
            var nickname = $"user{Guid.NewGuid():N}"[..16];
            owner = new AppUser
            {
                UserName = email,
                Email = email,
                // Kept false so ConfiguredAdmins never promotes an account created through Google.
                EmailConfirmed = false,
                DisplayName = (external.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0]).Trim(),
                Nickname = nickname,
                NormalizedNickname = nickname.ToUpperInvariant(),
            };
            if (!(await users.CreateAsync(owner)).Succeeded) return Fail("google_failed");
            await users.AddToRoleAsync(owner, Roles.User);
            await users.AddLoginAsync(owner, new UserLoginInfo(Scheme, subject, "Google"));
        }

        if (owner.IsDeleted || await users.IsLockedOutAsync(owner)) return Fail("locked");
        var remember = external.Properties?.Items.TryGetValue(Remember, out var r) == true && r == "1";
        AuthCookie.Append(context.Response, tokens.Create(owner, await users.GetRolesAsync(owner), remember), remember);
        return TypedResults.Redirect("/");
    }
}

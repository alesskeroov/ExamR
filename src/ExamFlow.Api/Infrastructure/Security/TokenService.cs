using System.Security.Claims;
using System.Text;
using ExamFlow.Api.Features.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExamFlow.Api.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; set; } = "ExamFlow";
    public string Audience { get; set; } = "ExamFlow";

    /// <summary>Secret: user-secrets in development, environment variable Jwt__SigningKey in production.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
    public int RememberMeAccessTokenMinutes { get; set; } = 30 * 24 * 60;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>Short JWT claim names; the API reads these instead of the long ClaimTypes URIs.</summary>
public static class JwtClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = "name";
    public const string Role = "role";

    /// <summary>Identity security stamp. Changing it (logout, password reset) revokes all earlier tokens.</summary>
    public const string SecurityStamp = "sst";

    /// <summary>"1" when the session was created with Remember Me (persistent cookie).</summary>
    public const string RememberMe = "rm";
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Create(AppUser user, IEnumerable<string> roles, bool rememberMe = false)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(rememberMe ? settings.RememberMeAccessTokenMinutes : settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtClaims.UserId, user.Id.ToString()),
            new(JwtClaims.Email, user.Email ?? string.Empty),
            new(JwtClaims.Name, user.DisplayName),
            new(JwtClaims.SecurityStamp, user.SecurityStamp ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (rememberMe) claims.Add(new Claim(JwtClaims.RememberMe, "1"));
        claims.AddRange(roles.Select(role => new Claim(JwtClaims.Role, role)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(settings.Key(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}

/// <summary>The session cookie that carries the JWT. HttpOnly so scripts cannot read it.</summary>
public static class AuthCookie
{
    public const string Name = "examr.auth";

    // SameSite=Strict: the browser never attaches it to cross-site requests (CSRF), the UI is served same-origin via /api.
    public static void Append(HttpResponse response, AccessToken token, bool persistent) =>
        response.Cookies.Append(Name, token.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            // Without Expires it is a browser-session cookie and disappears when the browser closes.
            Expires = persistent ? token.ExpiresAt : null,
        });

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/" });
}

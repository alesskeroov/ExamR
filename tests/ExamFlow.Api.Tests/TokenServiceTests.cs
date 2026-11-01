using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExamFlow.Api.Tests;

public class TokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "ExamFlow",
        Audience = "ExamFlow",
        SigningKey = new string('k', 64),
        AccessTokenMinutes = 60,
    };

    private static readonly AppUser User = new()
    {
        Id = Guid.NewGuid(),
        Email = "user@examflow.test",
        DisplayName = "Test",
        SecurityStamp = "stamp-1",
    };

    private static TokenValidationParameters Validation(SymmetricSecurityKey key) => new()
    {
        ValidIssuer = Options.Issuer,
        ValidAudience = Options.Audience,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
    };

    [Fact]
    public async Task Token_is_signed_and_carries_user_roles_and_security_stamp()
    {
        var token = new TokenService(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System).Create(User, ["User", "Admin"]);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Validation(Options.Key()));

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(User.Id.ToString(), result.Claims[JwtClaims.UserId]);
        Assert.Equal("stamp-1", result.Claims[JwtClaims.SecurityStamp]);
        Assert.Equal(["User", "Admin"], result.ClaimsIdentity.FindAll(JwtClaims.Role).Select(c => c.Value));
    }

    [Fact]
    public void Token_expires_after_the_configured_minutes()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);

        var token = new TokenService(Microsoft.Extensions.Options.Options.Create(Options), clock).Create(User, []);

        Assert.Equal(now.AddMinutes(60), token.ExpiresAt);
        Assert.Equal(now.AddMinutes(60).ToUnixTimeSeconds(), new DateTimeOffset(new JsonWebToken(token.Token).ValidTo).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var token = new TokenService(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System).Create(User, []);
        var otherKey = new SymmetricSecurityKey(new byte[64]);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Validation(otherKey));

        Assert.False(result.IsValid);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

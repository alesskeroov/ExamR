using System.Security.Claims;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ExamFlow.Api.Tests;

public class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToRevoke_KeepsRoomForTheNewSessionAndEndsTheLeastRecentlyUsed()
    {
        Guid recent = Guid.NewGuid(), older = Guid.NewGuid(), oldest = Guid.NewGuid();
        var active = new[] { (oldest, Now.AddDays(-3)), (recent, Now.AddMinutes(-1)), (older, Now.AddHours(-2)) };

        Assert.Equal([older, oldest], UserSessions.ToRevoke(active, maxActive: 2));
        Assert.Equal([recent, older, oldest], UserSessions.ToRevoke(active, maxActive: 1));
        Assert.Empty(UserSessions.ToRevoke(active, maxActive: 4));
    }

    [Fact]
    public void ToRevoke_TreatsLimitBelowOneAsOne()
    {
        var only = Guid.NewGuid();
        Assert.Equal([only], UserSessions.ToRevoke([(only, Now)], maxActive: 0));
    }

    [Fact]
    public void SessionIdOf_PrefersSidAndFallsBackToJtiForLegacyTokens()
    {
        Guid sid = Guid.NewGuid(), jti = Guid.NewGuid();
        var current = new ClaimsPrincipal(new ClaimsIdentity([new(JwtClaims.SessionId, sid.ToString()), new(JwtRegisteredClaimNames.Jti, jti.ToString())]));
        var legacy = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Jti, jti.ToString())]));

        Assert.Equal((sid, false), UserSessions.SessionIdOf(current));
        Assert.Equal((jti, true), UserSessions.SessionIdOf(legacy));
        Assert.Null(UserSessions.SessionIdOf(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36", "Chrome · Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36 Edg/131.0", "Edge · Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari · iPhone")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Mobile Safari/537.36", "Chrome · Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14.5; rv:132.0) Gecko/20100101 Firefox/132.0", "Firefox · macOS")]
    [InlineData("", "Naməlum cihaz")]
    [InlineData(null, "Naməlum cihaz")]
    public void DeviceLabel_NamesBrowserAndSystem(string? userAgent, string expected) =>
        Assert.Equal(expected, UserSessions.DeviceLabel(userAgent));

    [Fact]
    public void Truncate_LimitsStoredUserAgent() =>
        Assert.Equal(UserSessions.MaxUserAgentLength, UserSessions.Truncate(new string('a', 1000)).Length);
}

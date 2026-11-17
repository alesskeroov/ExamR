using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ExamFlow.Api.Features.Auth;

/// <summary>One signed-in browser. Every JWT carries its id (<see cref="JwtClaims.SessionId"/>), so a session can be ended on its own.</summary>
public sealed class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>The user's security stamp at sign-in. After a password change or reset the session no longer counts as active.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.UserAgent).HasMaxLength(UserSessions.MaxUserAgentLength);
        builder.Property(x => x.SecurityStamp).HasMaxLength(256);
        builder.Property(x => x.RevokedReason).HasMaxLength(16);
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
    }
}

public sealed class DeviceSessionOptions
{
    public const string Section = "Sessions";

    /// <summary>How many browsers may be signed in to one account at the same time; a new sign-in ends the least recently used one.</summary>
    public int MaxActive { get; set; } = 2;
}

public static class SessionRevokeReasons
{
    /// <summary>Ended because the account signed in on more devices than <see cref="DeviceSessionOptions.MaxActive"/>.</summary>
    public const string Limit = "limit";
    /// <summary>Ended by the account owner from the devices list.</summary>
    public const string User = "user";
    public const string Logout = "logout";
}

public sealed record SessionCheck(Guid? SessionId, string? RevokedReason);
public sealed record SessionResponse(Guid Id, string Device, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool Current);
public sealed record SessionsResponse(int MaxActive, IReadOnlyList<SessionResponse> Sessions);

public static class UserSessions
{
    public const int MaxUserAgentLength = 256;

    /// <summary>LastSeenAt is written at most this often, so ordinary requests do not each cause a database write.</summary>
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan KeepEndedFor = TimeSpan.FromDays(30);

    /// <summary>
    /// The sessions to end so that, together with one new session, at most <paramref name="maxActive"/> remain:
    /// the least recently used go first.
    /// </summary>
    public static IReadOnlyList<Guid> ToRevoke(IEnumerable<(Guid Id, DateTimeOffset LastSeenAt)> active, int maxActive) =>
        active.OrderByDescending(s => s.LastSeenAt).Skip(Math.Max(maxActive, 1) - 1).Select(s => s.Id).ToList();

    /// <summary>
    /// Session id of a validated token. Tokens issued before sessions existed have no sid; their jti stands in for it,
    /// so they are adopted as a session on first use instead of signing everyone out.
    /// </summary>
    public static (Guid Id, bool Legacy)? SessionIdOf(ClaimsPrincipal principal)
    {
        if (Guid.TryParse(principal.FindFirst(JwtClaims.SessionId)?.Value, out var sid)) return (sid, false);
        return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var jti) ? (jti, true) : null;
    }

    public static IQueryable<UserSession> Active(this IQueryable<UserSession> sessions, Guid userId, string securityStamp, DateTimeOffset now) =>
        sessions.Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now && s.SecurityStamp == securityStamp);

    /// <summary>Records a new sign-in and ends the least recently used sessions beyond the limit.</summary>
    public static async Task StartAsync(AppDbContext db, AppUser user, Guid sessionId, DateTimeOffset expiresAt, string? userAgent,
        int maxActive, DateTimeOffset now, CancellationToken ct)
    {
        var stamp = user.SecurityStamp ?? string.Empty;
        var active = await db.UserSessions.Active(user.Id, stamp, now)
            .Select(s => new { s.Id, s.LastSeenAt }).ToListAsync(ct);
        var revoke = ToRevoke(active.Select(s => (s.Id, s.LastSeenAt)), maxActive);
        if (revoke.Count > 0)
        {
            await db.UserSessions.Where(s => revoke.Contains(s.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, now).SetProperty(s => s.RevokedReason, SessionRevokeReasons.Limit), ct);
        }

        // Ended and expired rows are only kept for a while; the table stays small without a background job.
        var cutoff = now - KeepEndedFor;
        await db.UserSessions.Where(s => s.UserId == user.Id && (s.ExpiresAt < cutoff || s.RevokedAt < cutoff)).ExecuteDeleteAsync(ct);

        db.UserSessions.Add(new UserSession
        {
            Id = sessionId,
            UserId = user.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAt,
            UserAgent = Truncate(userAgent),
            SecurityStamp = stamp,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Checks the session of an already signature-checked token. Returns the session id when it may be used, otherwise
    /// the reason it was ended (null when the token simply has no usable session).
    /// </summary>
    public static async Task<SessionCheck> ValidateAsync(AppDbContext db, AppUser user, ClaimsPrincipal principal, DateTime tokenValidTo,
        int maxActive, DateTimeOffset now, string? userAgent, CancellationToken ct)
    {
        if (SessionIdOf(principal) is not var (id, legacy)) return new(null, null);
        var session = await db.UserSessions.AsNoTracking().Where(s => s.Id == id && s.UserId == user.Id)
            .Select(s => new { s.RevokedAt, s.RevokedReason, s.LastSeenAt }).FirstOrDefaultAsync(ct);
        if (session is null)
        {
            if (!legacy) return new(null, null);
            try
            {
                await StartAsync(db, user, id, new DateTimeOffset(DateTime.SpecifyKind(tokenValidTo, DateTimeKind.Utc)), userAgent, maxActive, now, ct);
            }
            catch (DbUpdateException)
            {
                // A parallel request with the same legacy token adopted it first.
                db.ChangeTracker.Clear();
            }
            return new(id, null);
        }

        if (session.RevokedAt is not null) return new(null, session.RevokedReason);
        if (now - session.LastSeenAt > TouchInterval)
        {
            await db.UserSessions.Where(s => s.Id == id).ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSeenAt, now), ct);
        }
        return new(id, null);
    }

    /// <summary>Cheap check for live SignalR connections: false once the connection's session has been ended.</summary>
    public static async Task<bool> IsActiveAsync(AppDbContext db, Guid userId, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (SessionIdOf(principal) is not var (id, legacy)) return false;
        var revoked = await db.UserSessions.Where(s => s.Id == id && s.UserId == userId).Select(s => (bool?)(s.RevokedAt != null)).FirstOrDefaultAsync(ct);
        return revoked is null ? legacy : !revoked.Value;
    }

    /// <summary>Issues the session cookie for a fresh sign-in (password, Google, email confirmation).</summary>
    public static async Task<AccessToken> SignInAsync(HttpContext context, AppUser user, IEnumerable<string> roles, bool rememberMe)
    {
        var services = context.RequestServices;
        var sessionId = Guid.NewGuid();
        var token = services.GetRequiredService<TokenService>().Create(user, roles, sessionId, rememberMe);
        var max = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DeviceSessionOptions>>().Value.MaxActive;
        await StartAsync(services.GetRequiredService<AppDbContext>(), user, sessionId, token.ExpiresAt,
            context.Request.Headers.UserAgent.ToString(), max, services.GetRequiredService<TimeProvider>().GetUtcNow(), context.RequestAborted);
        AuthCookie.Append(context.Response, token, rememberMe);
        return token;
    }

    public static string Truncate(string? userAgent) =>
        string.IsNullOrEmpty(userAgent) ? string.Empty : userAgent.Length <= MaxUserAgentLength ? userAgent : userAgent[..MaxUserAgentLength];

    /// <summary>A short, readable device name such as "Chrome · Windows" for the devices list.</summary>
    public static string DeviceLabel(string? userAgent)
    {
        var ua = userAgent ?? string.Empty;
        var browser =
            ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge" :
            ua.Contains("OPR/", StringComparison.Ordinal) || ua.Contains("Opera", StringComparison.Ordinal) ? "Opera" :
            ua.Contains("YaBrowser/", StringComparison.Ordinal) ? "Yandex" :
            ua.Contains("SamsungBrowser/", StringComparison.Ordinal) ? "Samsung Internet" :
            ua.Contains("Firefox/", StringComparison.Ordinal) || ua.Contains("FxiOS/", StringComparison.Ordinal) ? "Firefox" :
            ua.Contains("Chrome/", StringComparison.Ordinal) || ua.Contains("CriOS/", StringComparison.Ordinal) ? "Chrome" :
            ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari" : null;
        var os =
            ua.Contains("Android", StringComparison.Ordinal) ? "Android" :
            ua.Contains("iPhone", StringComparison.Ordinal) ? "iPhone" :
            ua.Contains("iPad", StringComparison.Ordinal) ? "iPad" :
            ua.Contains("Windows", StringComparison.Ordinal) ? "Windows" :
            ua.Contains("Mac OS X", StringComparison.Ordinal) || ua.Contains("Macintosh", StringComparison.Ordinal) ? "macOS" :
            ua.Contains("CrOS", StringComparison.Ordinal) ? "ChromeOS" :
            ua.Contains("Linux", StringComparison.Ordinal) ? "Linux" : null;
        return (browser, os) switch
        {
            (null, null) => "Naməlum cihaz",
            (null, _) => os!,
            (_, null) => browser!,
            _ => $"{browser} · {os}",
        };
    }
}

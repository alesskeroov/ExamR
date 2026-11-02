using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ExamFlow.Api.Infrastructure.Security;

public static class AuthenticationSetup
{
    public const string AuthRateLimit = "auth";
    public const string PasswordResetRateLimit = "password-reset";
    public const string SocialRateLimit = "social";
    public const string HubPathPrefix = "/hubs";
    public const string CsrfHeader = "X-ExamR-Request";

    /// <summary>ASP.NET Core Identity (users, password hashing, lockout, reset tokens) + JWT bearer authentication.</summary>
    public static IServiceCollection AddJwtIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < JwtOptions.MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey is missing or shorter than {JwtOptions.MinSigningKeyBytes} bytes. " +
                "Development: dotnet user-secrets set \"Jwt:SigningKey\" \"<random 64+ chars>\". " +
                "Production: set the Jwt__SigningKey environment variable.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddSingleton<TokenService>();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.ClaimsIdentity.UserIdClaimType = JwtClaims.UserId;
                options.ClaimsIdentity.UserNameClaimType = JwtClaims.Name;
                options.ClaimsIdentity.RoleClaimType = JwtClaims.Role;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Password reset links are valid for 30 minutes and single-use (the security stamp changes on reset).
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.Key(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaims.Name,
                    RoleClaimType = JwtClaims.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on WebSocket requests, so SignalR sends the token
                    // as ?access_token=. Accept it only for hub endpoints.
                    OnMessageReceived = context =>
                    {
                        var request = context.HttpContext.Request;
                        var token = request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && request.Path.StartsWithSegments(HubPathPrefix))
                        {
                            context.Token = token;
                        }
                        else if (!request.Headers.ContainsKey("Authorization") && request.Cookies.TryGetValue(AuthCookie.Name, out var cookie))
                        {
                            // CSRF defence in depth on top of SameSite=Strict: state-changing API calls must carry a
                            // custom header, which cross-site forms cannot set. Hubs negotiate over POST without it.
                            var safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
                            if (safe || request.Path.StartsWithSegments(HubPathPrefix) || request.Headers.ContainsKey(CsrfHeader))
                            {
                                context.Token = cookie;
                                context.HttpContext.Items[AuthCookie.Name] = true;
                            }
                        }

                        return Task.CompletedTask;
                    },
                    // Reject tokens issued before the last logout / password reset.
                    OnTokenValidated = async context =>
                    {
                        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                        var userId = context.Principal?.FindFirst(JwtClaims.UserId)?.Value;
                        var stamp = context.Principal?.FindFirst(JwtClaims.SecurityStamp)?.Value;
                        var user = userId is null ? null : await users.FindByIdAsync(userId);
                        if (user is null || stamp is null || user.SecurityStamp != stamp)
                        {
                            context.Fail("Token has been revoked.");
                            return;
                        }

                        // Roles come from the database, not the token, so a granted or revoked Admin role applies immediately.
                        if (context.Principal!.Identity is ClaimsIdentity identity)
                        {
                            foreach (var claim in identity.FindAll(JwtClaims.Role).ToList()) identity.RemoveClaim(claim);
                            identity.AddClaims((await users.GetRolesAsync(user)).Select(role => new Claim(JwtClaims.Role, role)));
                        }

                        // Sliding refresh: past half its lifetime the cookie gets a fresh token with the same persistence.
                        // Revocation still works because every token carries the current security stamp.
                        if (context.HttpContext.Items.ContainsKey(AuthCookie.Name) && context.SecurityToken is { } issued
                            && issued.ValidTo - DateTime.UtcNow < (issued.ValidTo - issued.ValidFrom) / 2)
                        {
                            var remember = context.Principal.FindFirst(JwtClaims.RememberMe) is not null;
                            var fresh = context.HttpContext.RequestServices.GetRequiredService<TokenService>()
                                .Create(user, await users.GetRolesAsync(user), remember);
                            AuthCookie.Append(context.HttpContext.Response, fresh, remember);
                        }
                    },
                };
            })
            // Short-lived cookie that only carries the Google result to /api/auth/google/complete.
            .AddCookie(IdentityConstants.ExternalScheme, options =>
            {
                options.Cookie.Name = "examr.external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        if (GoogleAuthEndpoints.IsConfigured(configuration))
        {
            services.AddAuthentication().AddGoogle(GoogleAuthEndpoints.Scheme, options =>
            {
                options.ClientId = configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/api/auth/google/callback";
                options.UsePkce = true;
                // The return from accounts.google.com is a top-level GET, which Lax cookies survive (also over local http).
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });
        }

        services.AddAuthorization();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimit, context => PerIp(context, permits: 10, TimeSpan.FromMinutes(1)));
            options.AddPolicy(PasswordResetRateLimit, context => PerIp(context, permits: 5, TimeSpan.FromMinutes(15)));
            options.AddPolicy(SocialRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.GetUserId().ToString(), _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window });
}

using System.Text;
using System.Threading.RateLimiting;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ExamFlow.Api.Infrastructure.Security;

public static class AuthenticationSetup
{
    public const string AuthRateLimit = "auth";
    public const string PasswordResetRateLimit = "password-reset";
    public const string HubPathPrefix = "/hubs";

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
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(HubPathPrefix))
                        {
                            context.Token = token;
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
                        }
                    },
                };
            });

        services.AddAuthorization();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimit, context => PerIp(context, permits: 10, TimeSpan.FromMinutes(1)));
            options.AddPolicy(PasswordResetRateLimit, context => PerIp(context, permits: 5, TimeSpan.FromMinutes(15)));
        });

        return services;
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window });
}

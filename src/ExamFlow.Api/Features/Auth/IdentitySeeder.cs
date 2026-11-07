using System.Data.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Auth;

/// <summary>Idempotent startup seed: roles, plus the first Admin when Seed:Admin:* is configured.</summary>
public static class IdentitySeeder
{
    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        try
        {
            await SeedAsync(app, scope, logger);
        }
        catch (Exception ex) when (ex is DbException or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException)
        {
            // An unreachable database (firewall, paused, wrong connection string) must not kill the process: the
            // liveness probe keeps answering, /api/health reports 503 and requests that need SQL still fail loudly.
            // Roles and the configured admin are seeded on the next start once the database is reachable.
            logger.LogCritical(ex, "Database is not reachable at startup; identity seed skipped");
        }
    }

    private static async Task SeedAsync(WebApplication app, IServiceScope scope, ILogger logger)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogWarning("Database has pending migrations; skipping identity seed. Run: dotnet ef database update");
            return;
        }

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { Roles.Admin, Roles.User })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var configuredUsers = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var configuredEmail in ConfiguredAdmins.Emails(app.Configuration))
            if (await configuredUsers.FindByEmailAsync(configuredEmail) is { } configuredUser)
                await ConfiguredAdmins.EnsureAsync(configuredUsers, app.Configuration, configuredUser);

        var email = app.Configuration["Seed:Admin:Email"];
        var password = app.Configuration["Seed:Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var nickname = $"admin{Guid.NewGuid():N}"[..16];
        var admin = new AppUser { UserName = email, Email = email, DisplayName = "Admin", Nickname = nickname, NormalizedNickname = nickname.ToUpperInvariant() };
        var result = await users.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Admin seed failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRolesAsync(admin, [Roles.Admin, Roles.User]);
        logger.LogInformation("Seeded admin user {Email}", email);
    }
}

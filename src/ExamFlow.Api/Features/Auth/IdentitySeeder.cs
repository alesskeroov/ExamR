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

        var admin = new AppUser { UserName = email, Email = email, DisplayName = "Admin" };
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

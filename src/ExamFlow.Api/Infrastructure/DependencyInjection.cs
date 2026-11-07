using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Default' is not configured. " +
                "Development: dotnet user-secrets set \"ConnectionStrings:Default\" \"<value>\". " +
                "Production: set the ConnectionStrings__Default environment variable.");
        }

        // Startup line showing only where the API connects (server and database names, never credentials).
        try
        {
            var target = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            Console.WriteLine($"Database target: host '{target.Host}', database '{target.Database}', " +
                $"ssl {target.SslMode}, connect timeout {target.Timeout}s");
        }
        catch (ArgumentException)
        {
            Console.WriteLine("Database target: connection string could not be parsed");
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<DatabaseStartup>();
        services.AddSingleton<DatabaseStartupInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure())
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>(), sp.GetRequiredService<DatabaseStartupInterceptor>()));

        return services;
    }
}

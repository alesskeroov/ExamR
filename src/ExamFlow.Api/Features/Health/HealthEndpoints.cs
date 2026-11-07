using ExamFlow.Api.Infrastructure.Data;

namespace ExamFlow.Api.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this RouteGroupBuilder api)
    {
        // Liveness: the process answers (no database call), for container/platform restart probes.
        api.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
        // Readiness: also checks the database (and wakes a paused serverless database); returns 503 when SQL is down.
        // Never use it as a platform health check or uptime ping. /health is kept for the dashboard.
        api.MapGet("/health/ready", Ready);
        api.MapGet("/health", Ready);
    }

    private static async Task<IResult> Ready(AppDbContext db, CancellationToken ct)
    {
        var databaseConnected = await db.Database.CanConnectAsync(ct);
        var response = new HealthResponse(
            databaseConnected ? "Healthy" : "Degraded",
            databaseConnected,
            DateTimeOffset.UtcNow);

        return databaseConnected
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

public sealed record HealthResponse(string Status, bool DatabaseConnected, DateTimeOffset ServerTime);

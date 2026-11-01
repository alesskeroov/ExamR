using ExamFlow.Api.Infrastructure.Data;

namespace ExamFlow.Api.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        {
            var databaseConnected = await db.Database.CanConnectAsync(ct);
            var response = new HealthResponse(
                databaseConnected ? "Healthy" : "Degraded",
                databaseConnected,
                DateTimeOffset.UtcNow);

            return databaseConnected
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }
}

public sealed record HealthResponse(string Status, bool DatabaseConnected, DateTimeOffset ServerTime);

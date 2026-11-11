using System.Security.Claims;
using System.Threading.RateLimiting;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExamFlow.Api.Features.Feedback;

public static class FeedbackEndpoints
{
    public static IServiceCollection AddFeedback(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FeedbackOptions>().Bind(configuration.GetSection("Feedback")).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<FeedbackSignal>();
        services.AddScoped<FeedbackDelivery>();
        services.AddHostedService<FeedbackWorker>();
        services.AddRateLimiter(options => options.AddPolicy("feedback", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.GetUserId().ToString(), _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 3, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 })));
        return services;
    }

    public static void MapFeedbackEndpoints(this RouteGroupBuilder api) =>
        api.MapGroup("/feedback").RequireAuthorization()
            .MapPost("/", Submit).RequireRateLimiting("feedback").Validate<FeedbackRequest>();

    private static async Task<IResult> Submit(FeedbackRequest request, ClaimsPrincipal user, AppDbContext db,
        FeedbackSignal signal, TimeProvider clock, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty)
            return TypedResults.Problem("Müraciətin identifikatoru yanlışdır.", statusCode: 400);
        if (request.Text.Trim().Length < 5)
            return TypedResults.Problem("Müraciətin mətnini ən azı 5 simvolla yazın.", statusCode: 400);

        var userId = user.GetUserId();
        var submitted = db.FeedbackMessages.AsNoTracking()
            .Where(x => x.UserId == userId && x.RequestId == request.RequestId)
            .Select(x => new SavedFeedback(x.Id, x.Kind, x.Text, x.DocumentName, x.PagePath));
        var existing = await submitted.FirstOrDefaultAsync(ct);
        if (existing is not null) return Existing(existing, request);

        var sender = await db.Users.AsNoTracking().Where(x => x.Id == userId && !x.IsDeleted)
            .Select(x => new { x.DisplayName, x.Email }).SingleOrDefaultAsync(ct);
        if (sender is null) return TypedResults.NotFound();
        var now = clock.GetUtcNow();
        var message = new FeedbackMessage
        {
            Id = Guid.NewGuid(), UserId = userId, RequestId = request.RequestId, Kind = request.Kind,
            Text = request.Text.Trim(), DocumentName = request.DocumentName?.Trim(), PagePath = request.PagePath,
            SenderName = sender.DisplayName, SenderEmail = sender.Email ?? "", CreatedAt = now, NextAttemptAt = now,
        };
        db.FeedbackMessages.Add(message);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_FeedbackMessages_UserId_RequestId" })
        {
            // The same click can be retried after a lost response, including concurrently.
            db.ChangeTracker.Clear();
            var saved = await submitted.SingleAsync(ct);
            return Existing(saved, request);
        }
        signal.Notify(); // Only after the durable write; delivery survives a process restart.
        return TypedResults.Accepted((string?)null, new FeedbackAccepted(message.Id));
    }

    private sealed record SavedFeedback(Guid Id, string Kind, string Text, string? DocumentName, string PagePath);

    private static IResult Existing(SavedFeedback saved, FeedbackRequest request) =>
        saved.Kind == request.Kind && saved.Text == request.Text.Trim() && saved.DocumentName == request.DocumentName?.Trim()
            && saved.PagePath == request.PagePath
            ? TypedResults.Accepted((string?)null, new FeedbackAccepted(saved.Id))
            : TypedResults.Problem("Müraciətin məzmunu dəyişib. Formanı yenidən açıb göndərin.", statusCode: 409);
}

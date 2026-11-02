using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Focus;

/// <summary>One finished focus block (breaks are never stored, so they never count as focus time).</summary>
public sealed class FocusSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Activity { get; set; } = "";
    public int PlannedMinutes { get; set; }
    public int FocusSeconds { get; set; }
    public int Answered { get; set; }
    public int Correct { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
}

public sealed class FocusSessionConfiguration : IEntityTypeConfiguration<FocusSession>
{
    public void Configure(EntityTypeBuilder<FocusSession> builder)
    {
        builder.ToTable("FocusSessions");
        builder.Property(s => s.Activity).HasMaxLength(16);
        builder.HasIndex(s => new { s.UserId, s.EndedAt });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed record SaveFocusSessionRequest(
    [property: Required, RegularExpression("^(exam|questions|mistakes|study)$")] string Activity,
    [property: Range(15, 60)] int PlannedMinutes,
    [property: Range(0, 60 * 60)] int FocusSeconds,
    [property: Range(0, 10_000)] int Answered,
    [property: Range(0, 10_000)] int Correct,
    bool Completed,
    DateTimeOffset StartedAt);

public sealed record FocusDayDto(DateOnly Date, int FocusSeconds);
public sealed record FocusStatsDto(int TodaySeconds, int WeekSeconds, int CompletedSessions, IReadOnlyList<FocusDayDto> LastSevenDays);

public static class FocusEndpoints
{
    public static void MapFocusEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/focus").RequireAuthorization();
        group.MapPost("/sessions", Save).Validate<SaveFocusSessionRequest>();
        group.MapGet("/stats", Stats);
    }

    private static async Task<IResult> Save(SaveFocusSessionRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (request.Correct > request.Answered || request.FocusSeconds > request.PlannedMinutes * 60 + 5)
            return TypedResults.Problem("Sessiya məlumatları yanlışdır.", statusCode: 400);
        db.Add(new FocusSession
        {
            Id = Guid.NewGuid(), UserId = user.GetUserId(), Activity = request.Activity, PlannedMinutes = request.PlannedMinutes,
            FocusSeconds = request.FocusSeconds, Answered = request.Answered, Correct = request.Correct, Completed = request.Completed,
            StartedAt = request.StartedAt > now ? now : request.StartedAt, EndedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Days follow the browser's UTC offset (minutes, as returned by Date.getTimezoneOffset) so "today" matches the user.
    private static async Task<IResult> Stats(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct, int offset = 0)
    {
        var shift = TimeSpan.FromMinutes(-Math.Clamp(offset, -840, 840));
        var today = DateOnly.FromDateTime((clock.GetUtcNow() + shift).DateTime);
        var from = new DateTimeOffset(today.AddDays(-6).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) - shift;
        var userId = user.GetUserId();
        var rows = await db.Set<FocusSession>().AsNoTracking().Where(s => s.UserId == userId && s.EndedAt >= from)
            .Select(s => new { s.EndedAt, s.FocusSeconds }).ToListAsync(ct);
        var days = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6))
            .Select(d => new FocusDayDto(d, rows.Where(r => DateOnly.FromDateTime((r.EndedAt + shift).DateTime) == d).Sum(r => r.FocusSeconds))).ToList();
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var completed = await db.Set<FocusSession>().CountAsync(s => s.UserId == userId && s.Completed, ct);
        return TypedResults.Ok(new FocusStatsDto(days[^1].FocusSeconds, days.Where(d => d.Date >= weekStart).Sum(d => d.FocusSeconds), completed, days));
    }
}

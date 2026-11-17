using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Billing;

public static class BillingEndpoints
{
    public const string RequestRateLimit = "billing-requests";

    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
        services.AddScoped<BillingService>();
        services.AddRateLimiter(options => options.AddPolicy(RequestRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.GetUserId().ToString(), _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 })));
        return services;
    }

    public static void MapBillingEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/billing").RequireAuthorization();
        group.MapGet("/", Get);
        group.MapGet("/history", History);
        group.MapPost("/requests", Create).RequireRateLimiting(RequestRateLimit).Validate<CreatePaymentRequest>();
        group.MapDelete("/requests/{id:guid}", Cancel);
        api.MapBillingAdminEndpoints();
    }

    internal static readonly Expression<Func<Plan, PlanDto>> ToPlanDto = p => new PlanDto(
        p.Id, p.Name, p.Description, p.Price, p.DurationDays, p.IsFree, p.IsActive, p.IsFeatured, p.SortOrder,
        p.MonthlyUploads, p.MonthlyExams, p.MonthlyBattles, p.MonthlyDimTests);

    internal static PaymentRequestDto ToPaymentDto(PaymentRequest r, Plan plan, BillingSettings settings)
    {
        var days = plan.DurationDays * r.Periods;
        var message = BillingRules.RequestMessage(plan.Name, r.Code, days, r.Amount);
        return new(r.Id, r.Code, plan.Id, plan.Name, r.Periods, days, r.Amount, r.Channel, r.Status, r.AdminNote, r.CreatedAt, r.ReviewedAt,
            message, BillingRules.WhatsAppLink(settings.WhatsAppNumber, message));
    }

    internal static async Task<BillingSettings> SettingsAsync(AppDbContext db, CancellationToken ct) =>
        await db.BillingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new BillingSettings();

    internal static async Task<BillingHistoryDto> HistoryAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var settings = await SettingsAsync(db, ct);
        var subscriptions = await db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartsAt).Take(100)
            .Select(s => new SubscriptionDto(s.Id, s.PlanId, s.Plan.Name, s.StartsAt, s.EndsAt, s.Source, s.Note, s.CancelledAt, s.CreatedAt))
            .ToListAsync(ct);
        var payments = await db.PaymentRequests.AsNoTracking().Include(r => r.Plan).Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return new(subscriptions, payments.Select(r => ToPaymentDto(r, r.Plan, settings)).ToList());
    }

    private static async Task<IResult> Get(ClaimsPrincipal user, AppDbContext db, BillingService billing, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var settings = await SettingsAsync(db, ct);
        var plans = await db.Plans.AsNoTracking().Where(p => p.IsActive || p.IsFree).OrderBy(p => p.SortOrder).ThenBy(p => p.Price)
            .Select(ToPlanDto).ToListAsync(ct);
        var current = (await billing.CurrentAsync([userId], ct))[userId];
        var pending = await db.PaymentRequests.AsNoTracking().Include(r => r.Plan)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == PaymentStatus.Pending, ct);
        return TypedResults.Ok(new BillingDto(settings.Enabled, current, plans, BillingRules.WhatsAppLink(settings.WhatsAppNumber, null),
            BillingRules.InstagramLink(settings.InstagramUsername), settings.PaymentInstructions,
            pending is null ? null : ToPaymentDto(pending, pending.Plan, settings)));
    }

    private static async Task<IResult> History(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await HistoryAsync(db, user.GetUserId(), ct));

    private static async Task<IResult> Create(CreatePaymentRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var settings = await SettingsAsync(db, ct);
        if (!settings.Enabled) return TypedResults.Problem("Hazırda paket satışı dayandırılıb.", statusCode: 409);
        var plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlanId, ct);
        if (plan is null || plan.IsFree || !plan.IsActive) return TypedResults.Problem("Bu paket satışda deyil.", statusCode: 400);
        if (plan.DurationDays < 30 && request.Periods > 1)
            return TypedResults.Problem("Bu paket yalnız bir dövrlük alınır.", statusCode: 400);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var pending = await db.PaymentRequests.AsNoTracking().Include(r => r.Plan)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == PaymentStatus.Pending, ct);
            if (pending is not null)
            {
                if (pending.PlanId == plan.Id && pending.Periods == request.Periods)
                    return TypedResults.Ok(ToPaymentDto(pending, pending.Plan, settings));
                return TypedResults.Problem("Gözləyən ödəniş sorğunuz var. Yeni paket seçmək üçün əvvəlcə onu ləğv edin.", statusCode: 409,
                    extensions: new Dictionary<string, object?> { ["pendingId"] = pending.Id });
            }

            var row = new PaymentRequest
            {
                UserId = userId, PlanId = plan.Id, Code = BillingRules.NewCode(Random.Shared), Periods = request.Periods,
                Amount = plan.Price * request.Periods, Channel = request.Channel, Status = PaymentStatus.Pending, CreatedAt = clock.GetUtcNow(),
            };
            db.PaymentRequests.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/billing/requests/{row.Id}", ToPaymentDto(row, plan, settings));
            }
            catch (DbUpdateException)
            {
                // Code collision or a parallel click created the pending request: look again.
                db.ChangeTracker.Clear();
            }
        }
        return TypedResults.Problem("Sorğu yaradılmadı. Bir az sonra yenidən cəhd edin.", statusCode: 503);
    }

    private static async Task<IResult> Cancel(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var now = clock.GetUtcNow();
        // Conditional update: an admin approving at the same moment cannot be overwritten.
        var changed = await db.PaymentRequests.Where(r => r.Id == id && r.UserId == userId && r.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, PaymentStatus.Cancelled).SetProperty(r => r.ReviewedAt, now), ct);
        if (changed == 1) return TypedResults.NoContent();
        return await db.PaymentRequests.AnyAsync(r => r.Id == id && r.UserId == userId, ct)
            ? TypedResults.Problem("Bu sorğuya artıq baxılıb.", statusCode: 409)
            : TypedResults.Problem("Sorğu tapılmadı.", statusCode: 404);
    }
}

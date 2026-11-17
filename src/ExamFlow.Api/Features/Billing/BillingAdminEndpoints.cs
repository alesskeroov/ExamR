using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Usage;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Billing;

public static class BillingAdminEndpoints
{
    private const int PageSize = 25;

    public static void MapBillingAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin/billing").RequireAuthorization(p => p.RequireRole(Roles.Admin));
        admin.MapGet("/users", Users);
        admin.MapGet("/users/{userId:guid}", UserDetail);
        admin.MapPost("/users/bulk", Bulk).Validate<BulkRequest>();
        admin.MapGet("/requests", Requests);
        admin.MapGet("/requests/{id:guid}/preview", Preview);
        admin.MapPost("/requests/{id:guid}/approve", Approve).Validate<ApprovePaymentRequest>();
        admin.MapPost("/requests/{id:guid}/reject", Reject).Validate<RejectPaymentRequest>();
        admin.MapGet("/plans", Plans);
        admin.MapPost("/plans", CreatePlan).Validate<UpsertPlanRequest>();
        admin.MapPut("/plans/{id:guid}", UpdatePlan).Validate<UpsertPlanRequest>();
        admin.MapGet("/settings", GetSettings);
        admin.MapPut("/settings", UpdateSettings).Validate<UpdateBillingSettingsRequest>();
    }

    private static IResult Problem(int status, string detail) => TypedResults.Problem(detail, statusCode: status);

    // ---- Users ----

    private static async Task<IResult> Users(AppDbContext db, UsageLimiter limiter, BillingService billing, TimeProvider clock, CancellationToken ct,
        string? q = null, Guid? planId = null, string? status = null, int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        var now = clock.GetUtcNow();
        var soon = now.AddDays(7);
        var users = db.Users.AsNoTracking().Where(u => !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToUpperInvariant();
            users = users.Where(u => u.NormalizedNickname.Contains(term) || u.NormalizedEmail!.Contains(term));
        }

        var free = await db.Plans.AsNoTracking().Where(p => p.IsFree).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        if (planId is { } plan)
        {
            users = plan == free
                ? users.Where(u => !db.Subscriptions.Any(s => s.UserId == u.Id && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now))
                : users.Where(u => db.Subscriptions.Any(s => s.UserId == u.Id && s.PlanId == plan && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now));
        }
        users = status switch
        {
            "active" => users.Where(u => db.Subscriptions.Any(s => s.UserId == u.Id && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)),
            "expiring" => users.Where(u => db.Subscriptions.Any(s => s.UserId == u.Id && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)
                && !db.Subscriptions.Any(s => s.UserId == u.Id && s.CancelledAt == null && s.EndsAt > soon)),
            "expired" => users.Where(u => db.Subscriptions.Any(s => s.UserId == u.Id)
                && !db.Subscriptions.Any(s => s.UserId == u.Id && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)),
            "free" => users.Where(u => !db.Subscriptions.Any(s => s.UserId == u.Id && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)),
            "pending" => users.Where(u => db.PaymentRequests.Any(r => r.UserId == u.Id && r.Status == PaymentStatus.Pending)),
            "custom" => users.Where(u => db.UserUsageLimits.Any(l => l.UserId == u.Id)),
            _ => users,
        };

        var total = await users.CountAsync(ct);
        var ids = await users.OrderBy(u => u.Nickname).ThenBy(u => u.Id).Skip((page - 1) * PageSize).Take(PageSize).Select(u => u.Id).ToListAsync(ct);
        var items = await RowsAsync(db, limiter, billing, ids, ct);
        return TypedResults.Ok(new PagedResult<AdminBillingUserDto>(items, page, PageSize, total));
    }

    private static async Task<List<AdminBillingUserDto>> RowsAsync(AppDbContext db, UsageLimiter limiter, BillingService billing, List<Guid> ids, CancellationToken ct)
    {
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new { u.Nickname, u.Email }, ct);
        var admins = await AdminsAsync(db, ids, ct);
        var usage = await limiter.SummariesAsync(ids, admins, ct);
        var plans = await billing.CurrentAsync(ids, ct);
        var custom = await db.UserUsageLimits.AsNoTracking().Where(l => ids.Contains(l.UserId))
            .ToDictionaryAsync(l => l.UserId, l => new CustomLimitsDto(l.MonthlyUploads, l.MonthlyExams, l.MonthlyBattles, l.MonthlyDimTests, l.Unlimited), ct);
        var pending = await db.PaymentRequests.AsNoTracking().Where(r => ids.Contains(r.UserId) && r.Status == PaymentStatus.Pending)
            .Select(r => r.UserId).ToListAsync(ct);
        return ids.Where(people.ContainsKey).Select(id => new AdminBillingUserDto(id, people[id].Nickname, people[id].Email, admins.Contains(id),
            plans[id], usage[id], custom.GetValueOrDefault(id), pending.Contains(id))).ToList();
    }

    private static Task<List<Guid>> AdminsAsync(AppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (from ur in db.UserRoles.AsNoTracking()
         join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
         where ids.Contains(ur.UserId) && r.Name == Roles.Admin
         select ur.UserId).ToListAsync(ct);

    private static async Task<IResult> UserDetail(Guid userId, AppDbContext db, UsageLimiter limiter, BillingService billing, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && !u.IsDeleted, ct)) return Problem(404, "İstifadəçi tapılmadı.");
        var row = (await RowsAsync(db, limiter, billing, [userId], ct)).Single();
        return TypedResults.Ok(new AdminBillingUserDetailDto(row, await BillingEndpoints.HistoryAsync(db, userId, ct)));
    }

    private static async Task<IResult> Bulk(BulkRequest request, ClaimsPrincipal user, AppDbContext db, UsageLimiter limiter, BillingService billing,
        TimeProvider clock, CancellationToken ct)
    {
        var ids = request.UserIds.Distinct().ToList();
        var found = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && !u.IsDeleted).Select(u => u.Id).ToListAsync(ct);
        if (found.Count != ids.Count) return Problem(400, $"{ids.Count - found.Count} istifadəçi tapılmadı və ya hesabı silinib. Heç bir dəyişiklik edilmədi.");

        Plan? plan = null;
        if (request.Action == BulkAction.AssignPlan)
        {
            plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlanId, ct);
            if (plan is null) return Problem(400, "Paket seçin.");
            if (plan.IsFree) return Problem(400, "Free paketə keçirmək üçün «Paketi dayandır» əməliyyatını seçin.");
        }
        if (request.Action == BulkAction.Extend && request.Days is null) return Problem(400, "Neçə gün əlavə ediləcəyini yazın.");

        var admins = await AdminsAsync(db, ids, ct);
        var adminId = user.GetUserId();
        var now = clock.GetUtcNow();
        var results = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var items = new List<BulkItemDto>();
            foreach (var id in ids)
            {
                string? skipped = null;
                switch (request.Action)
                {
                    case BulkAction.AssignPlan when admins.Contains(id):
                        skipped = "Admin hesabı onsuz da limitsizdir.";
                        break;
                    case BulkAction.AssignPlan:
                        await billing.AssignAsync(id, plan!, request.Periods ?? 1, request.Days ?? 0, adminId, request.Note, ct);
                        break;
                    case BulkAction.Extend:
                        if (!await billing.ExtendAsync(id, request.Days!.Value, ct)) skipped = "Aktiv paketi yoxdur.";
                        break;
                    case BulkAction.CancelPlan:
                        if (!await billing.CancelAsync(id, adminId, ct)) skipped = "Aktiv paketi yoxdur.";
                        break;
                    default:
                        await SetLimitsAsync(db, limiter, id, request, adminId, now, ct);
                        break;
                }
                items.Add(new BulkItemDto(id, skipped is null, skipped));
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return items;
        });
        return TypedResults.Ok(new BulkResultDto(results.Count(x => x.Updated), results.Count(x => !x.Updated), results));
    }

    private static async Task SetLimitsAsync(AppDbContext db, UsageLimiter limiter, Guid userId, BulkRequest request, Guid adminId,
        DateTimeOffset now, CancellationToken ct)
    {
        // Same locks as the consumers, so a limit never changes in the middle of a usage check.
        foreach (var kind in Enum.GetValues<UsageKind>()) await limiter.LockAsync(userId, kind, ct);
        var row = await db.UserUsageLimits.SingleOrDefaultAsync(l => l.UserId == userId, ct);
        if (request.Action == BulkAction.ClearLimits)
        {
            if (row is not null) db.UserUsageLimits.Remove(row);
            return;
        }
        if (row is null) db.UserUsageLimits.Add(row = new UserUsageLimit { UserId = userId });
        if (request.Action == BulkAction.SetUnlimited)
        {
            row.Unlimited = true;
        }
        else
        {
            row.MonthlyUploads = request.MonthlyUploads;
            row.MonthlyExams = request.MonthlyExams;
            row.MonthlyBattles = request.MonthlyBattles;
            row.MonthlyDimTests = request.MonthlyDimTests;
            row.Unlimited = false;
        }
        row.UpdatedAt = now;
        row.UpdatedByUserId = adminId;
    }

    // ---- Payment requests ----

    private static async Task<IResult> Requests(AppDbContext db, CancellationToken ct, string? status = "Pending", string? q = null, int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        var settings = await BillingEndpoints.SettingsAsync(db, ct);
        var rows = db.PaymentRequests.AsNoTracking();
        PaymentStatus? filter = Enum.TryParse<PaymentStatus>(status, true, out var parsed) ? parsed : null;
        if (filter is { } f) rows = rows.Where(r => r.Status == f);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToUpperInvariant();
            rows = rows.Where(r => r.Code.Contains(term) || db.Users.Any(u => u.Id == r.UserId && (u.NormalizedNickname.Contains(term) || u.NormalizedEmail!.Contains(term))));
        }
        var total = await rows.CountAsync(ct);
        // The oldest open request is handled first; reviewed ones read newest first.
        var ordered = filter == PaymentStatus.Pending ? rows.OrderBy(r => r.CreatedAt) : rows.OrderByDescending(r => r.CreatedAt);
        var pageRows = await ordered.Skip((page - 1) * PageSize).Take(PageSize).Include(r => r.Plan).ToListAsync(ct);
        var userIds = pageRows.Select(r => r.UserId).Concat(pageRows.Where(r => r.ReviewedByUserId != null).Select(r => r.ReviewedByUserId!.Value)).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new { u.Nickname, u.Email }, ct);
        var items = pageRows.Select(r => new AdminPaymentDto(r.UserId, people.GetValueOrDefault(r.UserId)?.Nickname ?? "—", people.GetValueOrDefault(r.UserId)?.Email,
            BillingEndpoints.ToPaymentDto(r, r.Plan, settings),
            r.ReviewedByUserId is { } by ? people.GetValueOrDefault(by)?.Nickname : null)).ToList();
        return TypedResults.Ok(new PagedResult<AdminPaymentDto>(items, page, PageSize, total));
    }

    private static async Task<IResult> Preview(Guid id, AppDbContext db, BillingService billing, TimeProvider clock, CancellationToken ct,
        int? periods = null, int? bonusDays = null)
    {
        var request = await db.PaymentRequests.AsNoTracking().Include(r => r.Plan).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Problem(404, "Sorğu tapılmadı.");
        var now = clock.GetUtcNow();
        var live = await billing.LiveAsync(request.UserId, now, ct);
        var change = BillingService.Decide(live, request.Plan, Math.Clamp(periods ?? request.Periods, 1, BillingRules.MaxPeriods),
            bonusDays is { } b ? Math.Clamp(b, 0, 365) : null, now);
        return TypedResults.Ok(new PlanChangePreviewDto(change.Kind, live.FirstOrDefault(s => s.StartsAt <= now)?.Plan.Name,
            change.StartsAt, change.EndsAt, change.BonusDays));
    }

    private static async Task<IResult> Approve(Guid id, ApprovePaymentRequest body, ClaimsPrincipal user, AppDbContext db, BillingService billing,
        TimeProvider clock, CancellationToken ct)
    {
        var userId = await db.PaymentRequests.AsNoTracking().Where(r => r.Id == id).Select(r => (Guid?)r.UserId).FirstOrDefaultAsync(ct);
        if (userId is null) return Problem(404, "Sorğu tapılmadı.");
        var adminId = user.GetUserId();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await billing.LockAsync(userId.Value, ct);
            var request = await db.PaymentRequests.AsNoTracking().Include(r => r.Plan).FirstAsync(r => r.Id == id, ct);
            if (request.Status != PaymentStatus.Pending) return Problem(409, "Bu sorğuya artıq baxılıb.");
            var now = clock.GetUtcNow();
            var periods = body.Periods ?? request.Periods;
            var amount = periods == request.Periods ? request.Amount : request.Plan.Price * periods;
            // Conditional update: a user cancelling at the same moment wins or loses cleanly, never both.
            var changed = await db.PaymentRequests.Where(r => r.Id == id && r.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, PaymentStatus.Approved).SetProperty(r => r.Periods, periods).SetProperty(r => r.Amount, amount)
                    .SetProperty(r => r.ReviewedAt, now).SetProperty(r => r.ReviewedByUserId, adminId)
                    .SetProperty(r => r.AdminNote, string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim()), ct);
            if (changed == 0) return Problem(409, "Bu sorğuya artıq baxılıb.");
            var change = await billing.PurchaseAsync(userId.Value, request.Plan, periods, body.BonusDays, id, adminId, body.Note, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (IResult)TypedResults.Ok(new PlanChangePreviewDto(change.Kind, null, change.StartsAt, change.EndsAt, change.BonusDays));
        });
    }

    private static async Task<IResult> Reject(Guid id, RejectPaymentRequest body, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var adminId = user.GetUserId();
        var note = string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim();
        var changed = await db.PaymentRequests.Where(r => r.Id == id && r.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, PaymentStatus.Rejected).SetProperty(r => r.ReviewedAt, now)
                .SetProperty(r => r.ReviewedByUserId, adminId).SetProperty(r => r.AdminNote, note), ct);
        if (changed == 1) return TypedResults.NoContent();
        return await db.PaymentRequests.AnyAsync(r => r.Id == id, ct) ? Problem(409, "Bu sorğuya artıq baxılıb.") : Problem(404, "Sorğu tapılmadı.");
    }

    // ---- Plans ----

    private static async Task<IResult> Plans(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var plans = await db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Price).Select(BillingEndpoints.ToPlanDto).ToListAsync(ct);
        var counts = await db.Subscriptions.AsNoTracking().Where(s => s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)
            .GroupBy(s => s.PlanId).Select(g => new { g.Key, Users = g.Select(s => s.UserId).Distinct().Count() }).ToDictionaryAsync(x => x.Key, x => x.Users, ct);
        var paid = counts.Values.Sum();
        var people = await db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted, ct);
        return TypedResults.Ok(plans.Select(p => new AdminPlanDto(p, p.IsFree ? Math.Max(0, people - paid) : counts.GetValueOrDefault(p.Id))).ToList());
    }

    private static async Task<IResult> CreatePlan(UpsertPlanRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var plan = new Plan();
        Apply(plan, request, user.GetUserId(), clock.GetUtcNow());
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/billing/plans/{plan.Id}", BillingEndpoints.ToPlanDto.Compile()(plan));
    }

    private static async Task<IResult> UpdatePlan(Guid id, UpsertPlanRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return Problem(404, "Paket tapılmadı.");
        Apply(plan, request, user.GetUserId(), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(BillingEndpoints.ToPlanDto.Compile()(plan));
    }

    private static void Apply(Plan plan, UpsertPlanRequest request, Guid adminId, DateTimeOffset now)
    {
        plan.Name = request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.SortOrder = request.SortOrder;
        plan.IsFeatured = request.IsFeatured;
        plan.MonthlyUploads = request.MonthlyUploads;
        plan.MonthlyExams = request.MonthlyExams;
        plan.MonthlyBattles = request.MonthlyBattles;
        plan.MonthlyDimTests = request.MonthlyDimTests;
        // Free is what everyone without a package has: always free, always available.
        plan.Price = plan.IsFree ? 0 : decimal.Round(request.Price, 2);
        plan.DurationDays = request.DurationDays;
        plan.IsActive = plan.IsFree || request.IsActive;
        plan.UpdatedAt = now;
        plan.UpdatedByUserId = adminId;
    }

    // ---- Settings ----

    private static async Task<IResult> GetSettings(AppDbContext db, CancellationToken ct)
    {
        var s = await BillingEndpoints.SettingsAsync(db, ct);
        return TypedResults.Ok(new BillingSettingsDto(s.Enabled, s.WhatsAppNumber, s.InstagramUsername, s.PaymentInstructions, s.UpdatedAt));
    }

    private static async Task<IResult> UpdateSettings(UpdateBillingSettingsRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var s = await db.BillingSettings.FirstOrDefaultAsync(ct);
        if (s is null) db.BillingSettings.Add(s = new BillingSettings { Id = 1 });
        s.Enabled = request.Enabled;
        s.WhatsAppNumber = Blank(request.WhatsAppNumber);
        s.InstagramUsername = Blank(request.InstagramUsername?.TrimStart('@'));
        s.PaymentInstructions = Blank(request.PaymentInstructions);
        if (s.Enabled && s.WhatsAppNumber is null && s.InstagramUsername is null)
            return Problem(400, "Satışı açmaq üçün WhatsApp nömrəsi və ya Instagram hesabı yazın.");
        s.UpdatedAt = clock.GetUtcNow();
        s.UpdatedByUserId = user.GetUserId();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new BillingSettingsDto(s.Enabled, s.WhatsAppNumber, s.InstagramUsername, s.PaymentInstructions, s.UpdatedAt));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

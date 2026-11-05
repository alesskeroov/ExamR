using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Learning;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Explanations;

public sealed record ExplanationAiStatusDto(bool Enabled, string Provider, bool RealCalls, bool ApiKeyConfigured, string TheoryModel, string MathModel,
    decimal MonthlyBudgetUsd, decimal MonthSpentUsd, decimal RemainingUsd, int MaxQuestionsPerJob);

/// <summary>What a run would do now: <see cref="Estimate"/> covers the next <c>MaxQuestionsPerJob</c> questions only.</summary>
public sealed record ExplanationEstimateDto(Guid DocumentId, int Pending, ExplanationEstimate Estimate, decimal RemainingUsd, bool WithinBudget, bool HasActiveJob);

public sealed record ExplanationJobDto(Guid Id, Guid DocumentId, ExplanationJobStatus Status, string Provider, int Total, int Done, int Failed,
    int Requests, int InputTokens, int OutputTokens, decimal EstimatedUsd, decimal SpentUsd, string? LastError,
    IReadOnlyList<Guid> FailedQuestionIds, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record ExplanationDocumentDto(Guid Id, string Name, int Questions);

/// <summary>An explanation as admins see it. <see cref="Shared"/>: written for the same question in another PDF and reused here.</summary>
public sealed record AdminExplanationDto(Guid Id, ExplanationStatus Status, ExplanationQuestionType QuestionType, string Explanation, string? ReviewReason,
    string? Model, decimal CostUsd, bool Shared, DateTimeOffset UpdatedAt, string RowVersion);

public sealed record AdminExplanationOptionDto(Guid Id, string Label, string Text, bool IsCorrect);

/// <summary><see cref="Explainable"/> is false when the answer key is unknown (nothing safe to explain).</summary>
public sealed record AdminExplanationQuestionDto(Guid QuestionId, int SourceNumber, string Text, IReadOnlyList<AdminExplanationOptionDto> Options,
    bool Explainable, bool FailedLastRun, AdminExplanationDto? Explanation, IReadOnlyList<VisualDto> Visuals);

public sealed record ExplanationCountsDto(int Total, int NotExplainable, int Missing, int Draft, int Review, int Approved, int Rejected, int Failed);

public sealed record AdminExplanationPageDto(IReadOnlyList<AdminExplanationQuestionDto> Items, int Page, int PageSize, int TotalCount, ExplanationCountsDto Counts);

public sealed record BulkApproveResultDto(int Approved, int Skipped);

public sealed class SaveExplanationRequest
{
    [Required, StringLength(ExplanationConfiguration.MaxExplanationLength, MinimumLength = ExplanationEngine.MinExplanationLength)]
    public string Explanation { get; init; } = "";

    [Required]
    public ExplanationQuestionType? QuestionType { get; init; }

    /// <summary>Version the admin edited; a newer save by someone else returns 409 instead of being overwritten.</summary>
    public string? RowVersion { get; init; }

    /// <summary>Save and approve in one step.</summary>
    public bool Approve { get; init; }
}

public sealed class ApproveExplanationsRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public List<Guid> Ids { get; init; } = [];
}

public sealed class StartExplanationJobRequest
{
    /// <summary>The estimate the admin saw and accepted; the run starts only if the current estimate is not higher.</summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal ConfirmedEstimateUsd { get; init; }
}

/// <summary>Admin-only AI generation control. Reading explanations lives in the exam endpoints and never calls a model.</summary>
public static class ExplanationAdminEndpoints
{
    public static void MapExplanationAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/explanations").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapGet("/status", Status);
        group.MapGet("/documents/{documentId:guid}/estimate", Estimate);
        group.MapGet("/documents/{documentId:guid}/jobs", Jobs);
        group.MapPost("/documents/{documentId:guid}/jobs", Start).Validate<StartExplanationJobRequest>()
            .RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapGet("/jobs/{jobId:guid}", Job);
        group.MapPost("/jobs/{jobId:guid}/cancel", Cancel);

        group.MapGet("/documents", Documents);
        group.MapGet("/documents/{documentId:guid}/questions", Questions);
        group.MapGet("/documents/{documentId:guid}/visuals/{visualId:guid}", Visual);
        group.MapPut("/questions/{questionId:guid}", Save).Validate<SaveExplanationRequest>();
        group.MapPost("/approve", ApproveMany).Validate<ApproveExplanationsRequest>();
        group.MapPost("/{id:guid}/approve", (Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            SetStatus(id, ExplanationStatus.Approved, user, db, clock, ct));
        group.MapPost("/{id:guid}/reject", (Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            SetStatus(id, ExplanationStatus.Rejected, user, db, clock, ct));
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Documents(AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Documents.AsNoTracking()
            .Where(d => d.Status == DocumentStatus.Completed && d.Questions.Any(q => q.Options.Count >= 2))
            .OrderBy(d => d.OriginalFileName)
            .Select(d => new ExplanationDocumentDto(d.Id, d.OriginalFileName, d.Questions.Count(q => q.Options.Count >= 2)))
            .ToListAsync(ct));

    /// <summary>Questions of a PDF with their explanation (any status), filtered and paged; counts cover the whole PDF.</summary>
    private static async Task<IResult> Questions(Guid documentId, AppDbContext db, CancellationToken ct,
        string? filter = null, string? search = null, int page = 1, int pageSize = 20)
    {
        if (!await DocumentExists(db, documentId, ct)) return TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.DocumentId == documentId && q.Options.Count >= 2)
            .OrderBy(q => q.SourceNumber)
            .Select(q => new
            {
                q.Id, q.SourceNumber, q.Text, Unverified = q.VerificationStatus == VerificationStatus.Unverified,
                Options = q.Options.OrderBy(o => o.Position).Select(o => new AdminExplanationOptionDto(o.Id, o.Label, o.Text, o.IsCorrect)).ToList(),
            })
            .ToListAsync(ct);

        var keyed = questions.Select(q => new
        {
            Question = q,
            Key = q.Unverified || q.Options.Count(o => o.IsCorrect) != 1 ? null
                : ExplanationEngine.Fingerprint(q.Text, q.Options.Select(o => o.Text), q.Options.First(o => o.IsCorrect).Text),
        }).ToList();
        var keys = keyed.Where(x => x.Key is not null).Select(x => x.Key!).ToList();
        var rows = keys.Count == 0 ? [] : await db.QuestionExplanations.AsNoTracking().Where(e => keys.Contains(e.Fingerprint)).ToListAsync(ct);
        var byKey = rows.ToDictionary(r => Convert.ToHexString(r.Fingerprint));
        var ownIds = questions.Select(q => q.Id).ToHashSet();
        var failed = (await db.ExplanationJobs.AsNoTracking().Where(j => j.DocumentId == documentId).OrderByDescending(j => j.CreatedAt)
            .Select(j => j.FailedQuestionIds).FirstOrDefaultAsync(ct))?.ToHashSet() ?? [];

        var items = keyed.Select(x =>
        {
            var row = x.Key is null ? null : byKey.GetValueOrDefault(Convert.ToHexString(x.Key));
            var dto = row is null ? null : new AdminExplanationDto(row.Id, row.Status, row.QuestionType, row.Explanation, row.ReviewReason, row.Model, row.CostUsd,
                row.SourceQuestionId is not { } source || !ownIds.Contains(source), row.UpdatedAt, Convert.ToBase64String(row.RowVersion));
            return new AdminExplanationQuestionDto(x.Question.Id, x.Question.SourceNumber, x.Question.Text, x.Question.Options, x.Key is not null,
                row is null && failed.Contains(x.Question.Id), dto, []);
        }).ToList();

        var counts = new ExplanationCountsDto(items.Count, items.Count(i => !i.Explainable), items.Count(i => i.Explainable && i.Explanation is null),
            Count(ExplanationStatus.Draft), Count(ExplanationStatus.Review), Count(ExplanationStatus.Approved), Count(ExplanationStatus.Rejected),
            items.Count(i => i.FailedLastRun));
        int Count(ExplanationStatus status) => items.Count(i => i.Explanation?.Status == status);

        IEnumerable<AdminExplanationQuestionDto> filtered = filter switch
        {
            "missing" => items.Where(i => i.Explainable && i.Explanation is null),
            "failed" => items.Where(i => i.FailedLastRun),
            "unexplainable" => items.Where(i => !i.Explainable),
            "draft" => items.Where(i => i.Explanation?.Status == ExplanationStatus.Draft),
            "review" => items.Where(i => i.Explanation?.Status == ExplanationStatus.Review),
            "approved" => items.Where(i => i.Explanation?.Status == ExplanationStatus.Approved),
            "rejected" => items.Where(i => i.Explanation?.Status == ExplanationStatus.Rejected),
            _ => items,
        };
        if (search?.Trim() is { Length: > 0 } term)
            filtered = filtered.Where(i => i.Text.Contains(term, StringComparison.CurrentCultureIgnoreCase) || i.SourceNumber.ToString() == term);
        var list = filtered.ToList();
        var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var visuals = await QuestionVisuals.ForQuestionsAsync(db, pageItems.Select(i => i.QuestionId).ToList(), ct);
        pageItems = pageItems.Select(i => visuals.Contains(i.QuestionId)
                ? i with { Visuals = visuals[i.QuestionId].Select(v => QuestionVisuals.ToDto(v, i.Options.Select(o => o.Id).ToList())).ToList() }
                : i)
            .ToList();
        return TypedResults.Ok(new AdminExplanationPageDto(pageItems, page, pageSize, list.Count, counts));
    }

    /// <summary>An image of a bank question; admins see every completed PDF here, not only the ones shared with them.</summary>
    private static async Task<IResult> Visual(Guid documentId, Guid visualId, AppDbContext db, IFileStorage storage, HttpResponse response, CancellationToken ct)
    {
        var visual = await db.QuestionVisuals.AsNoTracking()
            .Where(v => v.Id == visualId && v.DocumentId == documentId)
            .Select(v => new { v.StorageKey, v.ContentType })
            .FirstOrDefaultAsync(ct);
        return visual is null
            ? TypedResults.Problem("Şəkil tapılmadı.", statusCode: StatusCodes.Status404NotFound)
            : QuestionVisuals.File(storage, response, visual.StorageKey, visual.ContentType);
    }

    /// <summary>Writes or edits the explanation of a bank question by hand. A save moves it to Draft unless it is approved in the same step.</summary>
    private static async Task<IResult> Save(Guid questionId, SaveExplanationRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var question = await db.Questions.AsNoTracking().Where(q => q.Id == questionId)
            .Select(q => new { q.Text, Unverified = q.VerificationStatus == VerificationStatus.Unverified, Options = q.Options.Select(o => new { o.Text, o.IsCorrect }).ToList() })
            .FirstOrDefaultAsync(ct);
        if (question is null) return TypedResults.Problem("Sual tapılmadı.", statusCode: 404);
        var correct = question.Options.Where(o => o.IsCorrect).ToList();
        if (question.Unverified || question.Options.Count < 2 || correct.Count != 1)
            return TypedResults.Problem("Bu sualın düzgün cavabı məlum deyil; izah yazmaq olmaz.", statusCode: 409);

        var key = ExplanationEngine.Fingerprint(question.Text, question.Options.Select(o => o.Text), correct[0].Text)!;
        var row = await db.QuestionExplanations.FirstOrDefaultAsync(e => e.Fingerprint == key, ct);
        if (row is null)
        {
            row = new QuestionExplanation { Id = Guid.NewGuid(), Fingerprint = key, SourceQuestionId = questionId };
            db.QuestionExplanations.Add(row);
        }
        else if (request.RowVersion is { Length: > 0 } version)
        {
            try { db.Entry(row).Property(e => e.RowVersion).OriginalValue = Convert.FromBase64String(version); }
            catch (FormatException) { return TypedResults.Problem("Versiya yanlışdır.", statusCode: 400); }
        }

        row.Explanation = request.Explanation.Trim();
        row.QuestionType = request.QuestionType!.Value;
        row.ReviewReason = null;
        Approve(row, request.Approve ? ExplanationStatus.Approved : ExplanationStatus.Draft, user, clock);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Changed(); }
        catch (DbUpdateException) { return Changed(); } // the same content was saved at the same moment
        return TypedResults.Ok(ToAdmin(row, shared: row.SourceQuestionId != questionId));
    }

    private static async Task<IResult> SetStatus(Guid id, ExplanationStatus status, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var row = await db.QuestionExplanations.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (row is null) return TypedResults.Problem("İzah tapılmadı.", statusCode: 404);
        Approve(row, status, user, clock);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Changed(); }
        return TypedResults.NoContent();
    }

    /// <summary>Bulk approval takes Draft rows only: Review rows (AI disagreed with the key, unverified math) always need a one-by-one look.</summary>
    private static async Task<IResult> ApproveMany(ApproveExplanationsRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().ToList();
        var drafts = await db.QuestionExplanations.Where(e => ids.Contains(e.Id) && e.Status == ExplanationStatus.Draft).ToListAsync(ct);
        drafts.ForEach(row => Approve(row, ExplanationStatus.Approved, user, clock));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Changed(); }
        return TypedResults.Ok(new BulkApproveResultDto(drafts.Count, ids.Count - drafts.Count));
    }

    /// <summary>Removes an unapproved explanation so the question is generated again by the next run.</summary>
    private static async Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct)
    {
        var row = await db.QuestionExplanations.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (row is null) return TypedResults.NoContent();
        if (row.Status == ExplanationStatus.Approved)
            return TypedResults.Problem("Təsdiqlənmiş izahı silmək üçün əvvəlcə onu rədd edin.", statusCode: 409);
        db.QuestionExplanations.Remove(row);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static void Approve(QuestionExplanation row, ExplanationStatus status, ClaimsPrincipal user, TimeProvider clock)
    {
        row.Status = status;
        var approved = status == ExplanationStatus.Approved;
        row.ApprovedByUserId = approved ? user.GetUserId() : null;
        row.ApprovedAt = approved ? clock.GetUtcNow() : null;
    }

    private static IResult Changed() => TypedResults.Problem("İzah bu arada dəyişdirilib. Səhifəni yeniləyib yenidən cəhd edin.", statusCode: 409);

    private static AdminExplanationDto ToAdmin(QuestionExplanation r, bool shared) => new(r.Id, r.Status, r.QuestionType, r.Explanation, r.ReviewReason,
        r.Model, r.CostUsd, shared, r.UpdatedAt, Convert.ToBase64String(r.RowVersion));

    private static async Task<IResult> Status(ExplanationService bank, IOptions<ExplanationAiOptions> options, IConfiguration configuration, CancellationToken ct)
    {
        var o = options.Value;
        var spent = await bank.MonthSpentAsync(ct);
        var key = OpenAiChatClient.ApiKey(configuration) is not null;
        return TypedResults.Ok(new ExplanationAiStatusDto(o.Enabled, o.Provider, o.Enabled && o.Provider == ExplanationAiOptions.OpenAiProvider && key, key,
            o.TheoryModel, o.MathModel, o.MonthlyBudgetUsd, spent, Math.Max(0, o.MonthlyBudgetUsd - spent), o.MaxQuestionsPerJob));
    }

    private static async Task<IResult> Estimate(Guid documentId, AppDbContext db, ExplanationService bank, IOptions<ExplanationAiOptions> options, CancellationToken ct)
    {
        if (!await DocumentExists(db, documentId, ct)) return TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
        var o = options.Value;
        var pending = await bank.PendingAsync(documentId, [], ct);
        var estimate = ExplanationEngine.Estimate(pending.Take(o.MaxQuestionsPerJob).ToList(), o);
        var remaining = Math.Max(0, o.MonthlyBudgetUsd - await bank.MonthSpentAsync(ct));
        var active = await db.ExplanationJobs.AnyAsync(j => j.DocumentId == documentId
            && (j.Status == ExplanationJobStatus.Pending || j.Status == ExplanationJobStatus.Running), ct);
        return TypedResults.Ok(new ExplanationEstimateDto(documentId, pending.Count, estimate, remaining, estimate.EstimatedUsd <= remaining, active));
    }

    private static async Task<IResult> Start(Guid documentId, StartExplanationJobRequest request, ClaimsPrincipal user, AppDbContext db, ExplanationService bank,
        ExplanationQueue queue, IOptions<ExplanationAiOptions> options, IConfiguration configuration, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled) return TypedResults.Problem("AI ilə izah generasiyası deaktivdir.", statusCode: 409);
        if (o.Provider == ExplanationAiOptions.OpenAiProvider && OpenAiChatClient.ApiKey(configuration) is null)
            return TypedResults.Problem("OpenAI açarı serverdə konfiqurasiya edilməyib.", statusCode: 409);
        if (!await DocumentExists(db, documentId, ct)) return TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
        if (await db.ExplanationJobs.AnyAsync(j => j.DocumentId == documentId
                && (j.Status == ExplanationJobStatus.Pending || j.Status == ExplanationJobStatus.Running), ct))
            return TypedResults.Problem("Bu sənəd üçün generasiya artıq gedir.", statusCode: 409);

        var next = (await bank.PendingAsync(documentId, [], ct)).Take(o.MaxQuestionsPerJob).ToList();
        if (next.Count == 0) return TypedResults.Problem("Bu sənədin bütün sualları üçün izah artıq var.", statusCode: 409);
        var estimate = ExplanationEngine.Estimate(next, o);
        var remaining = o.MonthlyBudgetUsd - await bank.MonthSpentAsync(ct);
        if (estimate.EstimatedUsd > remaining)
            return TypedResults.Problem($"Təxmini xərc (${estimate.EstimatedUsd:0.####}) aylıq limitin qalığından (${Math.Max(0, remaining):0.####}) çoxdur.", statusCode: 409);
        if (request.ConfirmedEstimateUsd < estimate.EstimatedUsd)
            return TypedResults.Problem($"Təxmini xərc dəyişib (${estimate.EstimatedUsd:0.####}). Yenidən baxıb təsdiqləyin.", statusCode: 409);

        var job = new ExplanationJob
        {
            Id = Guid.NewGuid(), DocumentId = documentId, RequestedByUserId = user.GetUserId(), Status = ExplanationJobStatus.Pending,
            Provider = o.Provider, Total = next.Count, EstimatedUsd = estimate.EstimatedUsd,
        };
        db.ExplanationJobs.Add(job);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return TypedResults.Problem("Bu sənəd üçün generasiya artıq gedir.", statusCode: 409); } // filtered unique index
        queue.Notify();
        return TypedResults.Accepted($"/api/admin/explanations/jobs/{job.Id}", ToDto(job));
    }

    private static async Task<IResult> Jobs(Guid documentId, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok((await db.ExplanationJobs.AsNoTracking().Where(j => j.DocumentId == documentId)
            .OrderByDescending(j => j.CreatedAt).Take(10).ToListAsync(ct)).Select(ToDto).ToList());

    private static async Task<IResult> Job(Guid jobId, AppDbContext db, CancellationToken ct) =>
        await db.ExplanationJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct) is { } job
            ? TypedResults.Ok(ToDto(job))
            : TypedResults.Problem("Generasiya tapılmadı.", statusCode: 404);

    /// <summary>Stops a run before its next request; the explanations already saved stay.</summary>
    private static async Task<IResult> Cancel(Guid jobId, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var job = await db.ExplanationJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return TypedResults.Problem("Generasiya tapılmadı.", statusCode: 404);
        if (job.Status is ExplanationJobStatus.Pending or ExplanationJobStatus.Running)
        {
            job.Status = ExplanationJobStatus.Cancelled;
            job.CompletedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        return TypedResults.Ok(ToDto(job));
    }

    private static Task<bool> DocumentExists(AppDbContext db, Guid documentId, CancellationToken ct) =>
        db.Documents.AnyAsync(d => d.Id == documentId && d.Status == DocumentStatus.Completed, ct);

    private static ExplanationJobDto ToDto(ExplanationJob j) => new(j.Id, j.DocumentId, j.Status, j.Provider, j.Total, j.Done, j.Failed,
        j.Requests, j.InputTokens, j.OutputTokens, j.EstimatedUsd, j.SpentUsd, j.LastError, j.FailedQuestionIds, j.CreatedAt, j.CompletedAt);
}

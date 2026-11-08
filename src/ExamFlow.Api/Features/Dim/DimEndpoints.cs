using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Dim;

/// <summary>
/// User side of DİM tests. An attempt is a normal <see cref="Exam"/> in AtEnd mode, so answering, navigation, resume
/// after a lost connection, server-side timing, grading and the result page all reuse the exam module.
/// </summary>
public static class DimEndpoints
{
    public const string ClosedOnlyNotice =
        "Bu sınaq yalnız qapalı (A–E) suallardan ibarətdir. Rəsmi DİM balı açıq (kodlaşdırılan və yazılı) tapşırıqları da " +
        "əhatə etdiyi üçün tam 300/400 ballıq nəticə hesablanmır. \"Qapalı hissə balı\" qapalı tapşırıqların rəsmi düsturla " +
        "gətirdiyi baldır; \"Təxmini bal\" isə açıq tapşırıqlarda da eyni nəticə göstərəcəyiniz fərziyyəsinə əsaslanır.";

    public static void MapDimEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/dim").RequireAuthorization();
        group.MapGet("/catalog", Catalog);
        group.MapGet("/tests", Tests);
        group.MapPost("/tests/{id:guid}/start", Start);
        group.MapGet("/attempts/{examId:guid}/report", Report);
    }

    private static IResult Catalog()
    {
        static DimSubjectDto Dto(DimSubjectSpec s) => new(s.Code, s.Name, s.Closed, s.Coded, s.Written, s.WrongPenalty);
        return TypedResults.Ok(new DimCatalogDto(
            DimCatalog.Buraxilis.Select(Dto).ToList(),
            DimCatalog.BlokSubjectCodes.Select(c => Dto(DimCatalog.BlokSubject(c))).ToList(),
            DimCatalog.Groups.Select(g => new DimGroupDto(g.Code, g.Name,
                g.Subjects.Select(s => new DimGroupSubjectDto(s.Code, DimCatalog.SubjectName(s.Code), s.Coefficient)).ToList())).ToList()));
    }

    private static async Task<IResult> Tests(
        DimTrack track, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var now = clock.GetUtcNow();
        // An attempt whose time ran out is graded first, so its card does not keep offering "Davam et".
        if (await ExamEndpoints.LoadActiveAsync(db, userId, ct) is { } active)
            await ExamEndpoints.FinalizeIfExpiredAsync(db, active, now, options.Value, ct);

        var tests = await db.DimTests.AsNoTracking()
            .Where(t => t.Track == track && t.IsPublished)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var attempts = await db.DimAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.Test.Track == track)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new { a.TestId, Last = new DimLastAttemptDto(a.ExamId, a.Exam.Status, a.StartedAt, a.Exam.ScorePercent) })
            .ToListAsync(ct);
        var byTest = attempts.ToLookup(a => a.TestId, a => a.Last);

        return TypedResults.Ok(tests.Select(t =>
        {
            var used = byTest[t.Id].Count();
            return new DimTestDto(t.Id, t.Track, t.Kind, t.GroupCode, DimCatalog.Group(t.GroupCode)?.Name, t.Title, t.DurationMinutes,
                t.Sections.Sum(s => s.QuestionCount), t.Sections.Select(DimAdminEndpoints.ToSectionDto).ToList(), t.AttemptLimit, used,
                t.AvailableFrom, t.AvailableTo, IsOpen(t, now), byTest[t.Id].FirstOrDefault());
        }).ToList());
    }

    internal static bool IsOpen(DimTest test, DateTimeOffset now) =>
        (test.AvailableFrom is null || now >= test.AvailableFrom) && (test.AvailableTo is null || now < test.AvailableTo);

    private static async Task<IResult> Start(
        Guid id, DimStartRequest? request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var now = clock.GetUtcNow();
        var test = await db.DimTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.IsPublished, ct);
        if (test is null) return Problem(StatusCodes.Status404NotFound, "Sınaq tapılmadı.");
        if (!IsOpen(test, now))
            return Problem(StatusCodes.Status409Conflict, test.AvailableFrom > now ? "Sınaq hələ başlamayıb." : "Sınağın vaxtı bitib.");

        var active = await ExamEndpoints.LoadActiveAsync(db, userId, ct);
        if (active is not null && !await ExamEndpoints.FinalizeIfExpiredAsync(db, active, now, options.Value, ct))
            return Problem(StatusCodes.Status409Conflict, "Davam edən imtahanınız var.", new() { ["activeExamId"] = active.Id });

        if (test.AttemptLimit is { } limit && await db.DimAttempts.CountAsync(a => a.TestId == id && a.UserId == userId, ct) >= limit)
            return Problem(StatusCodes.Status409Conflict, "Bu sınaq üçün cəhd limitiniz bitib.");

        // Fixed questions keep their order inside a subject; random sections draw fresh questions for this attempt.
        var picked = new List<(string Subject, Guid QuestionId)>();
        foreach (var section in test.Sections)
        {
            var ids = section.QuestionIds;
            if (ids.Count == 0)
            {
                var bank = await DimAdminEndpoints.BankCandidatesAsync(db, test.Track, section.Subject, section.SourceDocumentId, ct);
                ids = ExamEngine.PickQuestions(bank, section.RandomCount, Random.Shared);
                if (ids.Count < section.RandomCount)
                    return Problem(StatusCodes.Status422UnprocessableEntity, "Sual bankında kifayət qədər sual yoxdur. Administratora bildirin.");
            }

            picked.AddRange(ids.Select(q => (section.Subject, q)));
        }

        var questionIds = picked.Select(p => p.QuestionId).ToList();
        var questions = await db.Questions.AsNoTracking().Include(q => q.Options)
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);
        var topics = await db.DimBankQuestions.AsNoTracking()
            .Where(b => questionIds.Contains(b.QuestionId))
            .ToDictionaryAsync(b => b.QuestionId, b => b.Topic, ct);
        // A fixed question may have been deleted or removed from the bank since the test was built: it is skipped.
        picked = picked.Where(p => questions.ContainsKey(p.QuestionId) && topics.ContainsKey(p.QuestionId)).ToList();
        if (picked.Count == 0) return Problem(StatusCodes.Status422UnprocessableEntity, "Sınaqda sual qalmayıb. Administratora bildirin.");

        var usable = picked.Select(p => p.QuestionId).ToList();
        var visuals = await QuestionVisuals.ForQuestionsAsync(db, usable, ct);
        var exam = new Exam
        {
            UserId = userId,
            QuestionCount = (short)picked.Count,
            RevealMode = request?.RevealMode ?? RevealMode.AtEnd,
            Status = ExamStatus.InProgress,
            StartedAt = now,
            ExpiresAt = now.AddMinutes(test.DurationMinutes),
            Questions = picked.Select((p, i) => ExamEndpoints.Freeze(questions[p.QuestionId], i, visuals[p.QuestionId])).ToList(),
        };
        db.Exams.Add(exam);
        db.DimAttempts.Add(new DimAttempt
        {
            Exam = exam,
            TestId = test.Id,
            UserId = userId,
            StartedAt = now,
            Items = picked.Select((p, i) => new DimAttemptItem { Position = i, Subject = p.Subject, Topic = topics[p.QuestionId] }).ToList(),
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // UX_Exams_UserActive: another request started an exam at the same moment.
            return Problem(StatusCodes.Status409Conflict, "Davam edən imtahanınız var.");
        }

        return TypedResults.Created($"/api/exams/{exam.Id}", new DimStartedDto(exam.Id));
    }

    /// <summary>Per-subject breakdown, DİM points and weak topics; only after the exam is completed and graded.</summary>
    private static async Task<IResult> Report(
        Guid examId, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var attempt = await db.DimAttempts.AsNoTracking().Include(a => a.Test)
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.UserId == userId, ct);
        if (attempt is null) return Problem(StatusCodes.Status404NotFound, "Sınaq nəticəsi tapılmadı.");

        var exam = await db.Exams.AsTracking().Include(e => e.Questions).FirstAsync(e => e.Id == examId, ct);
        if (!await ExamEndpoints.FinalizeIfExpiredAsync(db, exam, clock.GetUtcNow(), options.Value, ct))
            return Problem(StatusCodes.Status409Conflict, "İmtahan hələ bitməyib.");

        var items = attempt.Items.ToDictionary(i => i.Position);
        var graded = exam.Questions
            .Where(q => items.ContainsKey(q.Position))
            .Select(q => new DimGradedItem(items[q.Position].Subject, items[q.Position].Topic, q.Outcome ?? ExamEngine.OutcomeOf(q)))
            .ToList();
        var test = attempt.Test;
        var used = await db.DimAttempts.CountAsync(a => a.TestId == test.Id && a.UserId == userId, ct);
        return TypedResults.Ok(new DimReportDto(exam.Id, test.Id, test.Title, test.Track, test.Kind, test.GroupCode, DimCatalog.Group(test.GroupCode)?.Name,
            exam.CorrectCount ?? 0, exam.IncorrectCount ?? 0, exam.UnansweredCount ?? 0, exam.UngradedCount ?? 0, exam.ScorePercent,
            DimScoring.Score(test.Track, test.GroupCode, graded), ClosedOnlyNotice,
            test.AttemptLimit, used, test.IsPublished && IsOpen(test, clock.GetUtcNow())));
    }

    private static IResult Problem(int status, string detail, Dictionary<string, object?>? extensions = null) =>
        TypedResults.Problem(detail, statusCode: status, extensions: extensions);
}

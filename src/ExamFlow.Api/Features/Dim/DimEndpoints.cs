using ExamFlow.Api.Features.Usage;
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
        "əhatə etdiyi üçün tam 300/400 ballıq nəticə hesablanmır. \"Ölçülən bal\" qapalı tapşırıqların rəsmi düsturla " +
        "gətirdiyi baldır; \"Təxmini bal\" isə açıq tapşırıqlarda da eyni nəticə göstərəcəyiniz fərziyyəsinə əsaslanır.";

    public const string WithOpenNotice =
        "Bu sınaqda qapalı və açıq (kodlaşdırılan) suallar var. Yazılı tapşırıqlar imtahançı tərəfindən yoxlandığı üçün sınağa " +
        "daxil deyil, ona görə tam 300/400 ballıq nəticə hesablanmır. \"Ölçülən bal\" sınaqdakı tapşırıqların rəsmi düsturla " +
        "gətirdiyi baldır; \"Təxmini bal\" isə yazılı tapşırıqlarda da eyni nəticə göstərəcəyiniz fərziyyəsinə əsaslanır.";

    public static void MapDimEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/dim").RequireAuthorization();
        group.MapGet("/catalog", Catalog);
        group.MapGet("/tests", Tests);
        group.MapPost("/tests/{id:guid}/start", Start);
        group.MapGet("/attempts/{examId:guid}/report", Report);
        group.MapGet("/attempts/{examId:guid}/sections", Sections);
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
        UsageLimiter usage, CancellationToken ct)
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
        // Monthly package limit: checked early for a fast answer, consumed below in the same transaction as the attempt.
        var monthly = await usage.CheckAsync(userId, user.IsAdmin(), UsageKind.DimTest, ct);
        if (!monthly.Allowed) return monthly.Problem();

        // Random sections draw fresh questions for this attempt; all are put in PDF order below.
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
        var documentIds = questions.Values.Select(q => q.DocumentId).Distinct().ToList();
        var audios = await db.DimAudios.AsNoTracking().Where(a => documentIds.Contains(a.DocumentId)).ToListAsync(ct);
        var passages = await QuestionPassages.ForQuestionsAsync(db, questions.Values, ct);
        // Like the paper exam: inside each subject the questions follow the PDF's order, so a recording or passage comes
        // with its first question (listening 1–4 first) and its questions stay together.
        var sectionOrder = picked.Select(p => p.Subject).Distinct().Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        var documentOrder = picked.Select(p => questions[p.QuestionId].DocumentId).Distinct().Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i);
        picked = picked.DistinctBy(p => p.QuestionId)
            .OrderBy(p => sectionOrder[p.Subject])
            .ThenBy(p => documentOrder[questions[p.QuestionId].DocumentId])
            .ThenBy(p => questions[p.QuestionId].SourceNumber)
            .ToList();
        var exam = new Exam
        {
            UserId = userId,
            QuestionCount = (short)picked.Count,
            RevealMode = request?.RevealMode ?? RevealMode.AtEnd,
            Status = ExamStatus.InProgress,
            StartedAt = now,
            ExpiresAt = now.AddMinutes(test.DurationMinutes),
            Questions = picked.Select((p, i) => WithAudio(ExamEndpoints.Freeze(questions[p.QuestionId], i, visuals[p.QuestionId], passages.GetValueOrDefault(p.QuestionId)),
                questions[p.QuestionId], audios)).ToList(),
        };
        exam.Id = Guid.NewGuid();
        var attempt = new DimAttempt
        {
            Exam = exam,
            TestId = test.Id,
            UserId = userId,
            StartedAt = now,
            Items = picked.Select((p, i) => new DimAttemptItem { Position = i, Subject = p.Subject, Topic = topics[p.QuestionId] }).ToList(),
        };

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await usage.TryConsumeAsync(user, UsageKind.DimTest, exam.Id, ct) is { } denied) return denied;
            db.Exams.Add(exam);
            db.DimAttempts.Add(attempt);
            try
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                // UX_Exams_UserActive: another request started an exam at the same moment.
                return Problem(StatusCodes.Status409Conflict, "Davam edən imtahanınız var.");
            }

            return (IResult)TypedResults.Created($"/api/exams/{exam.Id}", new DimStartedDto(exam.Id));
        });
    }

    /// <summary>Freezes the listening recording that covers the question's number in its document, if any.</summary>
    internal static ExamQuestion WithAudio(ExamQuestion frozen, Question question, IReadOnlyList<DimAudio> audios)
    {
        if (audios.FirstOrDefault(a => a.DocumentId == question.DocumentId && a.Covers(question.SourceNumber)) is { } audio)
        {
            frozen.Snapshot.AudioId = audio.Id;
            frozen.Snapshot.AudioTitle = audio.Title;
            frozen.Snapshot.AudioMaxPlays = audio.MaxPlays;
        }

        return frozen;
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
            .Select(q => new DimGradedItem(items[q.Position].Subject, items[q.Position].Topic, q.Outcome ?? ExamEngine.OutcomeOf(q),
                q.Snapshot.IsCoded))
            .ToList();
        var test = attempt.Test;
        var used = await db.DimAttempts.CountAsync(a => a.TestId == test.Id && a.UserId == userId, ct);
        return TypedResults.Ok(new DimReportDto(exam.Id, test.Id, test.Title, test.Track, test.Kind, test.GroupCode, DimCatalog.Group(test.GroupCode)?.Name,
            exam.CorrectCount ?? 0, exam.IncorrectCount ?? 0, exam.UnansweredCount ?? 0, exam.UngradedCount ?? 0, exam.ScorePercent,
            DimScoring.Score(test.Track, test.GroupCode, graded), graded.Any(g => g.IsOpen) ? WithOpenNotice : ClosedOnlyNotice,
            test.AttemptLimit, used, test.IsPublished && IsOpen(test, clock.GetUtcNow())));
    }

    /// <summary>
    /// Which exam positions belong to which subject, so the exam screen can split a full test into its subjects.
    /// Only subject names and positions: nothing that would reveal answers.
    /// </summary>
    private static async Task<IResult> Sections(Guid examId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var items = await db.DimAttempts.AsNoTracking()
            .Where(a => a.ExamId == examId && a.UserId == userId)
            .Select(a => a.Items)
            .FirstOrDefaultAsync(ct);
        return items is null
            ? Problem(StatusCodes.Status404NotFound, "Sınaq tapılmadı.")
            : TypedResults.Ok(SubjectRuns(items));
    }

    /// <summary>Consecutive positions of the same subject, in exam order.</summary>
    public static List<DimSubjectRunDto> SubjectRuns(IEnumerable<DimAttemptItem> items)
    {
        var runs = new List<DimSubjectRunDto>();
        foreach (var item in items.OrderBy(i => i.Position))
        {
            if (runs.Count > 0 && runs[^1].Subject == item.Subject && runs[^1].From + runs[^1].Count == item.Position)
                runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
            else
                runs.Add(new DimSubjectRunDto(item.Subject, DimCatalog.SubjectName(item.Subject), item.Position, 1));
        }

        return runs;
    }

    private static IResult Problem(int status, string detail, Dictionary<string, object?>? extensions = null) =>
        TypedResults.Problem(detail, statusCode: status, extensions: extensions);
}

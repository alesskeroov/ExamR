using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Explanations;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Exams;

public static class ExamEndpoints
{
    public static void MapExamEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/exams").RequireAuthorization();

        group.MapGet("/availability", Availability);
        group.MapPost("/", Create).Validate<CreateExamRequest>();
        group.MapGet("/", History);
        group.MapGet("/active", Active);
        group.MapGet("/{id:guid}", Session);
        group.MapPut("/{id:guid}/answers/{examQuestionId:guid}", Answer).Validate<AnswerRequest>();
        group.MapPost("/{id:guid}/questions/{examQuestionId:guid}/reveal", Reveal);
        group.MapPost("/{id:guid}/finish", Finish);
        group.MapGet("/{id:guid}/result", Result);
        group.MapGet("/{id:guid}/visuals/{visualId:guid}", Visual);
        group.MapGet("/{id:guid}/explanations", Explanations);
        group.MapGet("/{id:guid}/questions/{examQuestionId:guid}/explanation", QuestionExplanation);
    }

    /// <summary>Approved explanations of a finished exam's questions (database only, no AI call).</summary>
    private static async Task<IResult> Explanations(
        Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct);
        if (exam is null) return NotFound();
        if (!await FinalizeIfExpiredAsync(db, exam, clock.GetUtcNow(), options.Value, ct))
            return Problem(StatusCodes.Status409Conflict, "İmtahan hələ bitməyib.");

        var keyed = exam.Questions.Select(q => (q.Position, Key: ExplanationFingerprint(q))).Where(x => x.Key is not null).ToList();
        var found = await ExplanationLookup.ApprovedAsync(db, keyed.Select(x => x.Key!), ct);
        return TypedResults.Ok(keyed
            .Select(x => (x.Position, Dto: found.GetValueOrDefault(Convert.ToHexString(x.Key!))))
            .Where(x => x.Dto is not null)
            .OrderBy(x => x.Position)
            .Select(x => new ExamExplanationDto(x.Position, x.Dto!.QuestionType!.Value, x.Dto.Explanation!))
            .ToList());
    }

    /// <summary>One question's explanation, only once the answer itself may be shown (Immediate mode after answering, or a finished exam).</summary>
    private static async Task<IResult> QuestionExplanation(
        Guid id, Guid examQuestionId, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct);
        var question = exam?.Questions.FirstOrDefault(q => q.Id == examQuestionId);
        if (exam is null || question is null) return NotFound();
        var finished = await FinalizeIfExpiredAsync(db, exam, clock.GetUtcNow(), options.Value, ct);
        // The explanation names the correct answer, so it follows the same reveal rules as the answer.
        if (!finished && RevealFor(exam, question) is null)
            return Problem(StatusCodes.Status409Conflict, "İzah cavab göstərildikdən sonra açılır.");

        var key = ExplanationFingerprint(question);
        if (key is null) return TypedResults.Ok(new QuestionExplanationDto(null, null));
        var found = await ExplanationLookup.ApprovedAsync(db, [key], ct);
        return TypedResults.Ok(found.GetValueOrDefault(Convert.ToHexString(key)) ?? new QuestionExplanationDto(null, null));
    }

    private static byte[]? ExplanationFingerprint(ExamQuestion question) => ExplanationEngine.Fingerprint(
        question.Snapshot.Text,
        question.Snapshot.Options.Select(o => o.Text),
        question.Snapshot.Options.FirstOrDefault(o => o.OptionId == question.CorrectOptionId)?.Text);

    /// <summary>
    /// The PDF's question-number span and how many distinct questions a range of it holds.
    /// Without from/to the whole PDF is counted.
    /// </summary>
    private static async Task<IResult> Availability(
        Guid documentId, ClaimsPrincipal user, AppDbContext db, IOptions<ExamOptions> options, CancellationToken ct, int? from = null, int? to = null)
    {
        if (!await DocumentAccess.Readable(db, user.GetUserId()).AnyAsync(d => d.Id == documentId
            && d.Questions.Any(q => q.Options.Count >= 2)
                && !d.Questions.Any(q => q.Explanation != null && q.Explanation != ""), ct))
        {
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");
        }

        var numbers = await db.Questions.AsNoTracking()
            .Where(q => q.DocumentId == documentId)
            .GroupBy(_ => 1)
            .Select(g => new { First = g.Min(q => q.SourceNumber), Last = g.Max(q => q.SourceNumber) })
            .FirstOrDefaultAsync(ct);

        var candidates = await Candidates(db, documentId, from ?? int.MinValue, to ?? int.MaxValue, ct);
        return TypedResults.Ok(new ExamAvailabilityDto(
            ExamEngine.CountDistinct(candidates),
            ExamEngine.CountDistinct(candidates.Where(c => !c.IsGradable)),
            numbers?.First,
            numbers?.Last,
            options.Value.MaxQuestions,
            options.Value.MinutesPerQuestion));
    }

    private static async Task<IResult> Create(
        CreateExamRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock,
        IOptions<ExamOptions> options, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var now = clock.GetUtcNow();
        var settings = options.Value;

        if (request.QuestionCount > settings.MaxQuestions)
        {
            return Problem(StatusCodes.Status400BadRequest, $"Sual sayı {settings.MaxQuestions}-dən çox ola bilməz.");
        }

        var documentId = request.DocumentId!.Value;
        if (request.RangeFrom > request.RangeTo)
        {
            return Problem(StatusCodes.Status400BadRequest, "Başlanğıc sual nömrəsi son nömrədən böyük ola bilməz.");
        }

        if (!await DocumentAccess.Readable(db, user.GetUserId()).AnyAsync(d => d.Id == documentId
            && d.Questions.Any(q => q.Options.Count >= 2)
                && !d.Questions.Any(q => q.Explanation != null && q.Explanation != ""), ct))
        {
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");
        }

        var active = await LoadActiveAsync(db, userId, ct);
        if (active is not null && !await FinalizeIfExpiredAsync(db, active, now, settings, ct))
        {
            return Problem(StatusCodes.Status409Conflict, "Davam edən imtahanınız var.", new() { ["activeExamId"] = active.Id });
        }

        var candidates = await Candidates(db, documentId, request.RangeFrom, request.RangeTo, ct);
        var available = ExamEngine.CountDistinct(candidates);
        if (available < request.QuestionCount)
        {
            return Problem(StatusCodes.Status422UnprocessableEntity,
                $"{request.RangeFrom}–{request.RangeTo} aralığında cəmi {available} unikal sual var.", new() { ["available"] = available });
        }

        var pickedIds = ExamEngine.PickQuestions(candidates, request.QuestionCount, Random.Shared);
        var questions = await db.Questions.AsNoTracking()
            .Include(q => q.Options)
            .Where(q => pickedIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);
        var visuals = await QuestionVisuals.ForQuestionsAsync(db, pickedIds, ct);

        var exam = new Exam
        {
            UserId = userId,
            QuestionCount = (short)request.QuestionCount,
            RevealMode = request.RevealMode,
            LockAfterCorrect = request.RevealMode == RevealMode.Immediate && request.LockAfterCorrect,
            SourceDocumentIds = [documentId],
            RangeFrom = request.RangeFrom,
            RangeTo = request.RangeTo,
            Status = ExamStatus.InProgress,
            StartedAt = now,
            ExpiresAt = now + ExamEngine.Duration(request.QuestionCount, request.DurationMinutes, settings),
            Questions = pickedIds.Select((id, position) => Freeze(questions[id], position, visuals[id])).ToList(),
        };

        db.Exams.Add(exam);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // UX_Exams_UserActive: another request started an exam at the same moment.
            return Problem(StatusCodes.Status409Conflict, "Davam edən imtahanınız var.");
        }

        return TypedResults.Created($"/api/exams/{exam.Id}", ToSession(exam, now));
    }

    private static async Task<IResult> History(
        ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct,
        int? page = null, int? pageSize = null)
    {
        var userId = user.GetUserId();
        if (await LoadActiveAsync(db, userId, ct) is { } active)
        {
            await FinalizeIfExpiredAsync(db, active, clock.GetUtcNow(), options.Value, ct);
        }

        var (pageNumber, size) = Paging.Normalize(page, pageSize);
        var query = db.Exams.AsNoTracking().Where(e => e.UserId == userId);
        var total = await query.CountAsync(ct);
        var exams = await query
            .OrderByDescending(e => e.StartedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        var names = await DocumentNames(db, exams, ct);
        var items = exams.Select(e => new ExamSummaryDto(
                e.Id, e.Status, e.QuestionCount, e.RevealMode, Source(e, names),
                e.StartedAt, e.ExpiresAt, e.CompletedAt, e.CompletionReason,
                e.CorrectCount, e.IncorrectCount, e.UnansweredCount, e.UngradedCount, e.ScorePercent))
            .ToList();

        return TypedResults.Ok(new PagedResult<ExamSummaryDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> Active(
        ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var active = await LoadActiveAsync(db, user.GetUserId(), ct);
        if (active is null || await FinalizeIfExpiredAsync(db, active, clock.GetUtcNow(), options.Value, ct))
        {
            return TypedResults.NoContent();
        }

        return TypedResults.Ok(new ActiveExamDto(active.Id));
    }

    private static async Task<IResult> Session(
        Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct);
        if (exam is null)
        {
            return NotFound();
        }

        var now = clock.GetUtcNow();
        await FinalizeIfExpiredAsync(db, exam, now, options.Value, ct);
        return TypedResults.Ok(ToSession(exam, now));
    }

    private static async Task<IResult> Answer(
        Guid id, Guid examQuestionId, AnswerRequest request, ClaimsPrincipal user, AppDbContext db,
        TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct, onlyQuestionId: examQuestionId);
        var question = exam?.Questions.FirstOrDefault(q => q.Id == examQuestionId);
        if (exam is null || question is null)
        {
            return NotFound();
        }

        var now = clock.GetUtcNow();
        if (exam.Status == ExamStatus.Completed || await FinalizeIfExpiredAsync(db, exam, now, options.Value, ct))
        {
            return Problem(StatusCodes.Status409Conflict, "İmtahan artıq bitib.");
        }

        if (question.LockedAt is not null)
        {
            return Problem(StatusCodes.Status409Conflict, "Bu sualın cavabı kilidlənib.");
        }

        if (request.SelectedOptionId is { } optionId && question.Snapshot.Options.All(o => o.OptionId != optionId))
        {
            return Problem(StatusCodes.Status400BadRequest, "Variant bu suala aid deyil.");
        }

        // Answers can be changed until the exam ends in every mode; Immediate mode only disallows clearing.
        if (exam.RevealMode == RevealMode.Immediate && request.SelectedOptionId is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "Variant seçilməyib.");
        }

        if (question.SelectedOptionId == request.SelectedOptionId)
        {
            return TypedResults.Ok(ToAnswerResult(exam, question));
        }

        question.SelectedOptionId = request.SelectedOptionId;
        question.AnsweredAt = request.SelectedOptionId is null ? null : now;
        if (ExamEngine.LocksOnAnswer(exam, question)) question.LockedAt = now;
        // Saved under the exam row lock: an answer is either stored before a concurrent finish grades the exam,
        // or rejected after it; it can never be stored but missing from the result. The same lock serialises it
        // with a concurrent answer/reveal of this exam, so the question's lock is re-read from the database here.
        var saved = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await LockInProgressAsync(db, exam.Id, ct) == 0) return SaveResult.Finished;
            if (exam.LockAfterCorrect && await IsQuestionLockedAsync(db, question.Id, ct)) return SaveResult.Locked;
            await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.AcceptAllChanges();
            return SaveResult.Saved;
        });
        return saved switch
        {
            SaveResult.Finished => Problem(StatusCodes.Status409Conflict, "İmtahan artıq bitib."),
            SaveResult.Locked => Problem(StatusCodes.Status409Conflict, "Bu sualın cavabı kilidlənib."),
            _ => TypedResults.Ok(ToAnswerResult(exam, question)),
        };
    }

    /// <summary>
    /// "Show the correct answer" in a LockAfterCorrect exam: locks the question (whatever its answer) and returns
    /// the correct answer. Idempotent. Other exams already get the answer with the saved pick (Immediate) or never.
    /// </summary>
    private static async Task<IResult> Reveal(
        Guid id, Guid examQuestionId, ClaimsPrincipal user, AppDbContext db,
        TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct, onlyQuestionId: examQuestionId);
        var question = exam?.Questions.FirstOrDefault(q => q.Id == examQuestionId);
        if (exam is null || question is null)
        {
            return NotFound();
        }

        var now = clock.GetUtcNow();
        if (exam.Status == ExamStatus.Completed || await FinalizeIfExpiredAsync(db, exam, now, options.Value, ct))
        {
            return Problem(StatusCodes.Status409Conflict, "İmtahan artıq bitib.");
        }

        if (!exam.LockAfterCorrect || exam.RevealMode != RevealMode.Immediate)
        {
            return Problem(StatusCodes.Status400BadRequest, "Bu imtahanda cavab bu yolla açılmır.");
        }

        var saved = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await LockInProgressAsync(db, exam.Id, ct) == 0) return false;
            await db.ExamQuestions.Where(q => q.Id == question.Id && q.LockedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.LockedAt, now), ct);
            // Read back under the exam lock: a concurrent answer may have changed or locked the question meanwhile.
            var stored = await db.ExamQuestions.AsNoTracking().Where(q => q.Id == question.Id)
                .Select(q => new { q.SelectedOptionId, q.LockedAt }).FirstAsync(ct);
            await transaction.CommitAsync(ct);
            question.SelectedOptionId = stored.SelectedOptionId;
            question.LockedAt = stored.LockedAt;
            return true;
        });
        return saved
            ? TypedResults.Ok(ToAnswerResult(exam, question))
            : Problem(StatusCodes.Status409Conflict, "İmtahan artıq bitib.");
    }

    private enum SaveResult { Saved, Finished, Locked }

    private static Task<bool> IsQuestionLockedAsync(AppDbContext db, Guid examQuestionId, CancellationToken ct) =>
        db.ExamQuestions.AsNoTracking().AnyAsync(q => q.Id == examQuestionId && q.LockedAt != null, ct);

    private static AnswerResultDto ToAnswerResult(Exam exam, ExamQuestion question) =>
        new(question.Id, question.SelectedOptionId, RevealFor(exam, question), question.LockedAt is not null);

    private static async Task<IResult> Finish(
        Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct);
        if (exam is null)
        {
            return NotFound();
        }

        if (exam.Status == ExamStatus.InProgress)
        {
            var now = clock.GetUtcNow();
            await GradeAsync(db, exam, now, now >= exam.ExpiresAt ? CompletionReason.TimeExpired : CompletionReason.Submitted, ct);
        }

        // Idempotent: a finished exam returns its stored result.
        return TypedResults.Ok(ToResult(exam, await DocumentNames(db, [exam], ct)));
    }

    private static async Task<IResult> Result(
        Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> options, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct);
        if (exam is null)
        {
            return NotFound();
        }

        await FinalizeIfExpiredAsync(db, exam, clock.GetUtcNow(), options.Value, ct);
        return exam.Status == ExamStatus.Completed
            ? TypedResults.Ok(ToResult(exam, await DocumentNames(db, [exam], ct)))
            : Problem(StatusCodes.Status409Conflict, "İmtahan hələ bitməyib.");
    }

    /// <summary>
    /// An image of one of the exam's frozen questions. Served from the snapshot, so it stays tied to the exam
    /// even if the PDF is reprocessed; the same access rule as the exam itself applies.
    /// </summary>
    private static async Task<IResult> Visual(
        Guid id, Guid visualId, ClaimsPrincipal user, AppDbContext db, IFileStorage storage, HttpResponse response, CancellationToken ct)
    {
        var exam = await LoadAsync(db, id, user.GetUserId(), ct);
        var visual = exam?.Questions.SelectMany(q => q.Snapshot.Visuals ?? []).FirstOrDefault(v => v.VisualId == visualId);
        return visual is null
            ? Problem(StatusCodes.Status404NotFound, "Şəkil tapılmadı.")
            : QuestionVisuals.File(storage, response, visual.StorageKey, visual.ContentType);
    }

    /// <summary>Usable questions of one PDF whose original number lies in [from, to].</summary>
    internal static Task<List<ExamCandidate>> Candidates(AppDbContext db, Guid documentId, int from, int to, CancellationToken ct) =>
        db.Questions.AsNoTracking()
            .Where(q => q.DocumentId == documentId && q.SourceNumber >= from && q.SourceNumber <= to && q.Options.Count >= 2)
            .Select(q => new ExamCandidate(q.Id, q.ContentHash, q.VerificationStatus != VerificationStatus.Unverified))
            .ToListAsync(ct);

    /// <summary>File names of the exams' source PDFs (deleted PDFs are simply missing).</summary>
    private static async Task<Dictionary<Guid, string>> DocumentNames(AppDbContext db, IEnumerable<Exam> exams, CancellationToken ct)
    {
        var ids = exams.SelectMany(e => e.SourceDocumentIds).Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.Documents.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.OriginalFileName, ct);
    }

    private static ExamSourceDto Source(Exam exam, IReadOnlyDictionary<Guid, string> names)
    {
        Guid? documentId = exam.SourceDocumentIds.Count == 1 ? exam.SourceDocumentIds[0] : null;
        return new ExamSourceDto(documentId, documentId is { } id ? names.GetValueOrDefault(id) : null, exam.RangeFrom, exam.RangeTo);
    }

    /// <summary>Copies the question into the exam with its options in a new random order.</summary>
    internal static ExamQuestion Freeze(Question question, int position, IEnumerable<QuestionVisual>? visuals = null)
    {
        var options = question.Options.OrderBy(o => o.Position).ToList();
        ExamEngine.Shuffle(options, Random.Shared);

        return new ExamQuestion
        {
            QuestionId = question.Id,
            Position = (short)position,
            Snapshot = new QuestionSnapshot
            {
                Text = question.Text,
                Options = options.Select((o, i) => new OptionSnapshot { OptionId = o.Id, Label = ((char)('A' + i)).ToString(), Text = o.Text }).ToList(),
                Visuals = visuals?.Select(v => QuestionVisuals.ToSnapshot(v, question.Options)).ToList() ?? [],
            },
            CorrectOptionId = question.VerificationStatus == VerificationStatus.Unverified ? null : options.FirstOrDefault(o => o.IsCorrect)?.Id,
        };
    }

    private static Task<Exam?> LoadAsync(AppDbContext db, Guid id, Guid userId, CancellationToken ct, Guid? onlyQuestionId = null)
    {
        var readable = DocumentAccess.Readable(db, userId);
        // Preserve historical personal exams while blocking snapshots whose shared access was revoked.
        var exams = db.Exams.Where(e => e.Id == id && e.UserId == userId
            && !db.Documents.Any(d => e.SourceDocumentIds.Contains(d.Id)
                && d.UploadedByUserId != userId && !readable.Any(allowed => allowed.Id == d.Id)));
        // The access subquery is no-tracking, but answer and grading handlers update the loaded exam.
        return (onlyQuestionId is { } questionId
            ? exams.Include(e => e.Questions.Where(q => q.Id == questionId))
            : exams.Include(e => e.Questions)).AsTracking().FirstOrDefaultAsync(ct);
    }

    private static Task<Exam?> LoadActiveAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        db.Exams.FirstOrDefaultAsync(e => e.UserId == userId && e.Status == ExamStatus.InProgress, ct);

    /// <summary>Completes an exam whose time is up (even if the browser was closed). Returns true if it is now completed.</summary>
    private static async Task<bool> FinalizeIfExpiredAsync(AppDbContext db, Exam exam, DateTimeOffset now, ExamOptions options, CancellationToken ct)
    {
        if (exam.Status == ExamStatus.Completed)
        {
            return true;
        }

        if (now <= exam.ExpiresAt.AddSeconds(options.AnswerGraceSeconds))
        {
            return false;
        }

        await GradeAsync(db, exam, now, CompletionReason.TimeExpired, ct);
        return true;
    }

    /// <summary>
    /// Takes the exam row's update lock if the exam is still in progress (a no-op UPDATE). Answer saving and grading
    /// both take it inside their transaction, so they run one after the other; 0 = the exam is already completed.
    /// </summary>
    private static Task<int> LockInProgressAsync(AppDbContext db, Guid examId, CancellationToken ct) =>
        db.Exams.Where(e => e.Id == examId && e.Status == ExamStatus.InProgress)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, e => e.Status), ct);

    /// <summary>
    /// Grades and completes the exam in one transaction, using the answers as committed after the lock, so an answer
    /// saved by a concurrent request is never lost from the result. If another request completed the exam first,
    /// its stored result is loaded instead of grading twice.
    /// </summary>
    private static Task GradeAsync(AppDbContext db, Exam exam, DateTimeOffset now, CompletionReason reason, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var locked = await LockInProgressAsync(db, exam.Id, ct) == 1;
            if (!locked) await db.Entry(exam).ReloadAsync(ct);

            if (exam.Questions.Count < exam.QuestionCount)
            {
                // A filtered Include marks the navigation as loaded, so query it explicitly.
                await db.Entry(exam).Collection(e => e.Questions).Query().LoadAsync(ct);
                if (exam.Questions.Count < exam.QuestionCount)
                    throw new InvalidOperationException("Cannot grade an exam with missing questions.");
            }

            var stored = await db.ExamQuestions.AsNoTracking().Where(q => q.ExamId == exam.Id)
                .Select(q => new { q.Id, q.SelectedOptionId, q.AnsweredAt, q.Outcome })
                .ToDictionaryAsync(q => q.Id, ct);
            foreach (var question in exam.Questions)
            {
                if (!stored.TryGetValue(question.Id, out var row)) continue;
                var entry = db.Entry(question);
                entry.Property(q => q.SelectedOptionId).OriginalValue = entry.Property(q => q.SelectedOptionId).CurrentValue = row.SelectedOptionId;
                entry.Property(q => q.AnsweredAt).OriginalValue = entry.Property(q => q.AnsweredAt).CurrentValue = row.AnsweredAt;
                entry.Property(q => q.Outcome).OriginalValue = entry.Property(q => q.Outcome).CurrentValue = row.Outcome;
            }

            if (locked)
            {
                ExamEngine.Grade(exam, now, reason);
                await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            }
            await transaction.CommitAsync(ct);
            db.ChangeTracker.AcceptAllChanges();
        });

    private static AnswerRevealDto? RevealFor(Exam exam, ExamQuestion question) =>
        ExamEngine.MayReveal(exam, question)
            ? new AnswerRevealDto(question.CorrectOptionId, question.CorrectOptionId is not null)
            : null;

    private static IReadOnlyList<ExamOptionDto> Options(ExamQuestion question) =>
        question.Snapshot.Options.Select(o => new ExamOptionDto(o.OptionId, o.Label, o.Text)).ToList();

    internal static IReadOnlyList<VisualDto> Visuals(ExamQuestion question) =>
        question.Snapshot.Visuals?.Select(QuestionVisuals.ToDto).ToList() ?? [];

    private static ExamSessionDto ToSession(Exam exam, DateTimeOffset now) => new(
        exam.Id, exam.Status, exam.RevealMode, exam.LockAfterCorrect, exam.QuestionCount, exam.StartedAt, exam.ExpiresAt, now,
        exam.Questions.OrderBy(q => q.Position)
            .Select(q => new ExamSessionQuestionDto(q.Id, q.Position, q.Snapshot.Text, Options(q), q.SelectedOptionId, RevealFor(exam, q), Visuals(q),
                q.LockedAt is not null))
            .ToList());

    private static ExamResultDto ToResult(Exam exam, IReadOnlyDictionary<Guid, string> documentNames)
    {
        var completedAt = exam.CompletedAt ?? exam.ExpiresAt;
        return new ExamResultDto(
            exam.Id, exam.QuestionCount, exam.RevealMode, exam.LockAfterCorrect, ExamEngine.TimeLimitMinutes(exam), Source(exam, documentNames), exam.CompletionReason ?? CompletionReason.Submitted,
            exam.CorrectCount ?? 0, exam.IncorrectCount ?? 0, exam.UnansweredCount ?? 0, exam.UngradedCount ?? 0, exam.ScorePercent,
            exam.StartedAt, completedAt, (int)Math.Round((completedAt - exam.StartedAt).TotalSeconds),
            exam.Questions.OrderBy(q => q.Position)
                .Select(q => new ExamResultQuestionDto(q.Position, q.Snapshot.Text, Options(q), q.SelectedOptionId, q.CorrectOptionId,
                    q.Outcome ?? ExamEngine.OutcomeOf(q), Visuals(q)))
                .ToList());
    }

    private static IResult NotFound() => Problem(StatusCodes.Status404NotFound, "İmtahan tapılmadı.");

    private static IResult Problem(int status, string detail, Dictionary<string, object?>? extensions = null) =>
        TypedResults.Problem(detail, statusCode: status, extensions: extensions);
}

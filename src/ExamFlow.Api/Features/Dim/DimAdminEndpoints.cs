using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Dim;

/// <summary>
/// Admin side of DİM tests. Questions come from the normal upload pipeline (PDF/DOCX → parser); here an admin fixes
/// the answer key, approves questions into the bank with a subject/topic, and builds tests from the bank.
/// </summary>
public static class DimAdminEndpoints
{
    public const int MaxOptions = 5;

    public static void MapDimAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/dim").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapGet("/documents/{documentId:guid}/questions", SourceQuestions);
        group.MapPost("/documents/{documentId:guid}/answer-key", AnswerKey).Validate<DimAnswerKeyRequest>();
        group.MapPost("/bank", Approve).Validate<DimApproveRequest>();
        group.MapDelete("/bank/{questionId:guid}", RemoveFromBank);
        group.MapGet("/bank/stats", BankStats);
        group.MapGet("/tests", Tests);
        group.MapPost("/tests", CreateTest).Validate<DimTestRequest>();
        group.MapPut("/tests/{id:guid}", UpdateTest).Validate<DimTestSettingsRequest>();
        group.MapDelete("/tests/{id:guid}", DeleteTest);
    }

    /// <summary>Questions of a document the admin uploaded, with their answer key and bank state.</summary>
    private static async Task<IResult> SourceQuestions(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.DocumentId == documentId)
            .OrderBy(q => q.SourceNumber)
            .Select(q => new
            {
                q.Id, q.SourceNumber, q.Text, q.VerificationStatus, q.ParseWarning,
                Options = q.Options.OrderBy(o => o.Position).Select(o => new { o.Id, o.Text, o.IsCorrect }).ToList(),
                Bank = db.DimBankQuestions.Where(b => b.QuestionId == q.Id).Select(b => new DimBankInfoDto(b.Track, b.Subject, b.Topic)).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return TypedResults.Ok(questions.Select(q => new DimSourceQuestionDto(q.Id, q.SourceNumber, q.Text,
            q.Options.Select((o, i) => new DimOptionDto(o.Id, ((char)('A' + i)).ToString(), o.Text, o.IsCorrect)).ToList(),
            q.VerificationStatus, q.ParseWarning, q.Bank)).ToList());
    }

    /// <summary>Applies a typed answer key ("1-A 2-C …") to the document's questions by their original number.</summary>
    private static async Task<IResult> AnswerKey(
        Guid documentId, DimAnswerKeyRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");

        var (key, conflicts) = DimScoring.ParseAnswerKey(request.Text);
        if (key.Count == 0)
            return Problem(StatusCodes.Status400BadRequest, "Cavab açarında heç bir \"nömrə-variant\" cütü tapılmadı (məs. 1-A 2-C).");

        var numbers = key.Keys.ToList();
        var questions = await db.Questions.Include(q => q.Options)
            .Where(q => q.DocumentId == documentId && numbers.Contains(q.SourceNumber))
            .ToListAsync(ct);

        // Duplicated source numbers (rare parser output) all receive the same key.
        var targets = questions.Where(q => key[q.SourceNumber] < q.Options.Count).ToList();
        var invalid = questions.Except(targets).Select(q => q.SourceNumber).ToList();
        var applied = targets.Count;

        // UX_AnswerOptions_SingleCorrect allows one correct option per question at any moment, so the old answers are
        // cleared and saved before the new ones are set, in one transaction.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var option in targets.SelectMany(q => q.Options)) option.IsCorrect = false;
            await db.SaveChangesAsync(ct);
            foreach (var question in targets)
            {
                question.Options.OrderBy(o => o.Position).ElementAt(key[question.SourceNumber]).IsCorrect = true;
                question.VerificationStatus = VerificationStatus.Verified;
                question.ParseWarning = null;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
        var found = questions.Select(q => q.SourceNumber).ToHashSet();
        return TypedResults.Ok(new DimAnswerKeyResultDto(applied, numbers.Where(n => !found.Contains(n)).Order().ToList(),
            invalid.Distinct().Order().ToList(), conflicts.Order().ToList()));
    }

    /// <summary>Why a question cannot enter the bank (MVP: closed A–E questions with exactly one known answer).</summary>
    public static string? BankRejection(Question question) =>
        question.Options.Count < 2 ? "Qapalı sual deyil (variant yoxdur)."
        : question.Options.Count > MaxOptions ? "Variantların sayı 5-dən çoxdur."
        : question.Options.Count(o => o.IsCorrect) != 1 ? "Düzgün cavab müəyyən edilməyib."
        : question.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)) || string.IsNullOrWhiteSpace(question.Text) ? "Sual və ya variant mətni boşdur."
        : null;

    private static async Task<IResult> Approve(
        DimApproveRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!DimCatalog.IsSubjectOf(request.Track, request.Subject))
            return Problem(StatusCodes.Status400BadRequest, "Fənn bu imtahan növünə aid deyil.");

        var userId = user.GetUserId();
        var ids = request.QuestionIds.Distinct().ToList();
        var questions = await db.Questions.Include(q => q.Options)
            .Where(q => ids.Contains(q.Id) && q.Document.UploadedByUserId == userId)
            .ToListAsync(ct);
        var existing = await db.DimBankQuestions.Where(b => ids.Contains(b.QuestionId)).ToDictionaryAsync(b => b.QuestionId, ct);

        var now = clock.GetUtcNow();
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim();
        var rejected = ids.Where(id => questions.All(q => q.Id != id)).Select(id => new DimRejectedDto(id, "Sual tapılmadı.")).ToList();
        foreach (var question in questions)
        {
            if (BankRejection(question) is { } reason)
            {
                rejected.Add(new DimRejectedDto(question.Id, reason));
                continue;
            }

            question.VerificationStatus = VerificationStatus.Verified;
            if (!existing.TryGetValue(question.Id, out var bank))
            {
                bank = new DimBankQuestion { QuestionId = question.Id };
                db.DimBankQuestions.Add(bank);
            }

            bank.Track = request.Track;
            bank.Subject = request.Subject;
            bank.Topic = topic;
            bank.ApprovedByUserId = userId;
            bank.ApprovedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new DimApproveResultDto(ids.Count - rejected.Count, rejected));
    }

    private static async Task<IResult> RemoveFromBank(Guid questionId, AppDbContext db, CancellationToken ct) =>
        await db.DimBankQuestions.Where(b => b.QuestionId == questionId).ExecuteDeleteAsync(ct) == 0
            ? Problem(StatusCodes.Status404NotFound, "Sual bankda tapılmadı.")
            : TypedResults.NoContent();

    private static async Task<IResult> BankStats(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.DimBankQuestions.AsNoTracking()
            .GroupBy(b => new { b.Track, b.Subject })
            .Select(g => new { g.Key.Track, g.Key.Subject, Count = g.Count(), Topics = g.Select(b => b.Topic).Distinct().Count(t => t != null) })
            .ToListAsync(ct);
        return TypedResults.Ok(rows.OrderBy(r => r.Track).ThenBy(r => r.Subject)
            .Select(r => new DimBankStatDto(r.Track, r.Subject, DimCatalog.SubjectName(r.Subject), r.Count, r.Topics)).ToList());
    }

    private static async Task<IResult> Tests(AppDbContext db, CancellationToken ct)
    {
        var tests = await db.DimTests.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var attempts = await db.DimAttempts.AsNoTracking().GroupBy(a => a.TestId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return TypedResults.Ok(tests.Select(t => ToAdminDto(t, attempts.GetValueOrDefault(t.Id))).ToList());
    }

    private static async Task<IResult> CreateTest(
        DimTestRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ValidateShape(request) is { } error) return Problem(StatusCodes.Status400BadRequest, error);
        if (ValidateWindow(request.AvailableFrom, request.AvailableTo) is { } windowError) return Problem(StatusCodes.Status400BadRequest, windowError);

        var sections = new List<DimTestSection>();
        foreach (var section in request.Sections)
        {
            var bank = await BankCandidatesAsync(db, request.Track, section.Subject, ct);
            var name = DimCatalog.SubjectName(section.Subject);
            if (section.QuestionIds.Count > 0)
            {
                var ids = section.QuestionIds.Distinct().ToList();
                var inBank = bank.Select(c => c.Id).ToHashSet();
                if (ids.Any(id => !inBank.Contains(id)))
                    return Problem(StatusCodes.Status400BadRequest, $"{name}: seçilmiş sualların bəziləri bu fənnin sual bankında deyil.");
                sections.Add(new DimTestSection { Subject = section.Subject, QuestionIds = ids });
                continue;
            }

            if (section.RandomCount < 1)
                return Problem(StatusCodes.Status400BadRequest, $"{name}: sual seçin və ya təsadüfi sual sayını göstərin.");
            if (ExamEngine.CountDistinct(bank) < section.RandomCount)
                return Problem(StatusCodes.Status422UnprocessableEntity,
                    $"{name}: bankda cəmi {ExamEngine.CountDistinct(bank)} unikal sual var, {section.RandomCount} tələb olunur.");

            sections.Add(section.DrawPerAttempt
                ? new DimTestSection { Subject = section.Subject, RandomCount = section.RandomCount }
                : new DimTestSection { Subject = section.Subject, QuestionIds = ExamEngine.PickQuestions(bank, section.RandomCount, Random.Shared) });
        }

        if (sections.Sum(s => s.QuestionCount) > ExamOptions.HardMaxQuestions)
            return Problem(StatusCodes.Status400BadRequest, $"Sınaqda ən çox {ExamOptions.HardMaxQuestions} sual ola bilər.");

        var test = new DimTest
        {
            Track = request.Track,
            Kind = request.Kind,
            GroupCode = request.Track == DimTrack.Blok && request.Kind == DimTestKind.Full ? request.GroupCode : null,
            Title = request.Title.Trim(),
            DurationMinutes = (short)request.DurationMinutes,
            AttemptLimit = (short?)request.AttemptLimit,
            AvailableFrom = request.AvailableFrom,
            AvailableTo = request.AvailableTo,
            IsPublished = request.IsPublished,
            CreatedByUserId = user.GetUserId(),
            CreatedAt = clock.GetUtcNow(),
            Sections = sections,
        };
        db.DimTests.Add(test);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/dim/tests/{test.Id}", ToAdminDto(test, 0));
    }

    private static async Task<IResult> UpdateTest(Guid id, DimTestSettingsRequest request, AppDbContext db, CancellationToken ct)
    {
        if (ValidateWindow(request.AvailableFrom, request.AvailableTo) is { } error) return Problem(StatusCodes.Status400BadRequest, error);
        var test = await db.DimTests.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (test is null) return NotFound();

        test.Title = request.Title.Trim();
        test.DurationMinutes = (short)request.DurationMinutes;
        test.AttemptLimit = (short?)request.AttemptLimit;
        test.AvailableFrom = request.AvailableFrom;
        test.AvailableTo = request.AvailableTo;
        test.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToAdminDto(test, await db.DimAttempts.CountAsync(a => a.TestId == id, ct)));
    }

    private static async Task<IResult> DeleteTest(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (await db.DimAttempts.AnyAsync(a => a.TestId == id, ct))
            return Problem(StatusCodes.Status409Conflict, "Bu sınaqda cəhdlər var; silmək əvəzinə dərcdən çıxarın.");
        return await db.DimTests.Where(t => t.Id == id).ExecuteDeleteAsync(ct) == 0 ? NotFound() : TypedResults.NoContent();
    }

    /// <summary>Approved bank questions of one subject, as exam candidates (all are gradable).</summary>
    internal static Task<List<ExamCandidate>> BankCandidatesAsync(AppDbContext db, DimTrack track, string subject, CancellationToken ct) =>
        db.DimBankQuestions.AsNoTracking()
            .Where(b => b.Track == track && b.Subject == subject)
            .Select(b => new ExamCandidate(b.QuestionId, b.Question.ContentHash, true))
            .ToListAsync(ct);

    public static string? ValidateShape(DimTestRequest request)
    {
        var subjects = request.Sections.Select(s => s.Subject).ToList();
        if (subjects.Distinct().Count() != subjects.Count) return "Hər fənn sınaqda bir dəfə ola bilər.";
        if (subjects.Any(s => !DimCatalog.IsSubjectOf(request.Track, s))) return "Fənn bu imtahan növünə aid deyil.";

        if (request.Kind == DimTestKind.Subject)
            return subjects.Count == 1 ? null : "Fənn sınağında yalnız bir fənn olmalıdır.";

        if (request.Track == DimTrack.Buraxilis)
            return subjects.Order().SequenceEqual(DimCatalog.Buraxilis.Select(s => s.Code).Order()) ? null
                : "Buraxılış tam sınağı Azərbaycan dili, Riyaziyyat və Xarici dil fənlərini əhatə etməlidir.";

        return DimCatalog.Group(request.GroupCode) is not { } group ? "Blok tam sınağı üçün ixtisas qrupu seçin."
            : subjects.Order().SequenceEqual(group.Subjects.Select(s => s.Code).Order()) ? null
            : $"{group.Name} sınağı qrupun üç fənnini əhatə etməlidir.";
    }

    private static string? ValidateWindow(DateTimeOffset? from, DateTimeOffset? to) =>
        from is not null && to is not null && from >= to ? "Başlama vaxtı bitmə vaxtından əvvəl olmalıdır." : null;

    internal static DimSectionDto ToSectionDto(DimTestSection s) =>
        new(s.Subject, DimCatalog.SubjectName(s.Subject), s.QuestionCount, s.QuestionIds.Count == 0);

    private static DimAdminTestDto ToAdminDto(DimTest t, int attempts) => new(
        t.Id, t.Track, t.Kind, t.GroupCode, t.Title, t.DurationMinutes, t.AttemptLimit, t.AvailableFrom, t.AvailableTo,
        t.IsPublished, t.CreatedAt, t.Sections.Select(ToSectionDto).ToList(), attempts);

    private static IResult NotFound() => Problem(StatusCodes.Status404NotFound, "Sınaq tapılmadı.");

    private static IResult Problem(int status, string detail) => TypedResults.Problem(detail, statusCode: status);
}

using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Learning;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Courses;

/// <summary>Fənlər: a student's subjects with lecture materials, question banks, exam date and a preparation plan. Private per user.</summary>
public static class CourseEndpoints
{
    private static readonly string[] MaterialExtensions = [".pdf", ".docx", ".pptx"];
    private const int MaxCandidates = 200;
    private const int ExcerptLength = 240;

    public static void MapCourseEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/courses").RequireAuthorization();
        group.MapGet("/", List);
        group.MapPost("/", Create).Validate<CourseRequest>();
        group.MapGet("/candidates", Candidates);
        group.MapGet("/exam-links/{examId:guid}", ExamLinks);
        group.MapGet("/{id:guid}", Get);
        group.MapPut("/{id:guid}", Update).Validate<CourseRequest>();
        group.MapDelete("/{id:guid}", Delete);
        group.MapPut("/{id:guid}/documents", SetDocuments).Validate<CourseDocumentsRequest>();
        group.MapGet("/{id:guid}/plan", Plan);
    }

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>Readable files without multiple-choice questions: lecture material for the guide.</summary>
    private static IQueryable<UploadedDocument> MaterialFiles(AppDbContext db, Guid userId) =>
        LearningEndpoints.GuideDocuments(db, userId)
            .Where(d => d.StoredFileName.EndsWith(".pdf") || d.StoredFileName.EndsWith(".docx") || d.StoredFileName.EndsWith(".pptx"));

    /// <summary>Readable, processed files with multiple-choice questions: question banks the exam setup can use.</summary>
    private static IQueryable<UploadedDocument> BankFiles(AppDbContext db, Guid userId) =>
        DocumentAccess.Readable(db, userId)
            .Where(d => d.Status == DocumentStatus.Completed && db.Questions.Any(q => q.DocumentId == d.Id && q.Options.Count >= 2));

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var today = Today(clock);
        var courses = await db.Set<StudyCourse>().AsNoTracking().Where(c => c.UserId == userId)
            .OrderBy(c => c.ExamDate == null).ThenBy(c => c.ExamDate).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.ExamDate,
                Materials = c.Documents.Count(d => d.Role == CourseDocumentRole.Material),
                Banks = c.Documents.Count(d => d.Role == CourseDocumentRole.Bank),
            })
            .ToListAsync(ct);
        return TypedResults.Ok(courses.Select(c => new CourseListItem(c.Id, c.Name, c.ExamDate, CoursePlanner.DaysLeft(c.ExamDate, today), c.Materials, c.Banks)));
    }

    private static async Task<IResult> Create(CourseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (await db.Set<StudyCourse>().CountAsync(c => c.UserId == userId, ct) >= StudyCourse.MaxCoursesPerUser)
            return TypedResults.Problem($"Ən çox {StudyCourse.MaxCoursesPerUser} fənn yaratmaq olar.", statusCode: StatusCodes.Status409Conflict);
        var now = clock.GetUtcNow();
        var course = new StudyCourse { Id = Guid.NewGuid(), UserId = userId, Name = request.Name!.Trim(), ExamDate = request.ExamDate, CreatedAt = now, UpdatedAt = now };
        db.Add(course);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/courses/{course.Id}", await DetailAsync(db, userId, course.Id, Today(clock), ct));
    }

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        await DetailAsync(db, user.GetUserId(), id, Today(clock), ct) is { } detail ? TypedResults.Ok(detail) : NotFound();

    private static async Task<IResult> Update(Guid id, CourseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var course = await db.Set<StudyCourse>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (course is null) return NotFound();
        course.Name = request.Name!.Trim();
        course.ExamDate = request.ExamDate;
        course.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetailAsync(db, userId, id, Today(clock), ct));
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        // The files, cards and exam history stay: only the course grouping is removed.
        var deleted = await db.Set<StudyCourse>().Where(c => c.Id == id && c.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : TypedResults.NoContent();
    }

    private static async Task<IResult> SetDocuments(Guid id, CourseDocumentsRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var course = await db.Set<StudyCourse>().Include(c => c.Documents).FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (course is null) return NotFound();
        var materials = request.Materials!.Distinct().ToList();
        var banks = request.Banks!.Distinct().ToList();
        if (materials.Intersect(banks).Any()) return Invalid("Eyni fayl həm mühazirə, həm sual bankı ola bilməz.");

        var allowedMaterials = await MaterialFiles(db, userId).Where(d => materials.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct);
        if (allowedMaterials.Count != materials.Count) return Invalid("Mühazirə faylı tapılmadı və ya test sualları olan fayldır.");
        var allowedBanks = await BankFiles(db, userId).Where(d => banks.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct);
        if (allowedBanks.Count != banks.Count) return Invalid("Sual bankı tapılmadı və ya hələ emal olunmayıb.");

        course.Documents.Clear();
        course.Documents.AddRange(materials.Select((d, i) => new StudyCourseDocument { CourseId = id, DocumentId = d, Role = CourseDocumentRole.Material, Position = i }));
        course.Documents.AddRange(banks.Select((d, i) => new StudyCourseDocument { CourseId = id, DocumentId = d, Role = CourseDocumentRole.Bank, Position = i }));
        course.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetailAsync(db, userId, id, Today(clock), ct));
    }

    /// <summary>Files the student can add, newest first: lecture materials and question banks.</summary>
    private static async Task<IResult> Candidates(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var materials = await MaterialFiles(db, userId).OrderByDescending(d => d.CreatedAt).Take(MaxCandidates)
            .Select(d => new { d.Id, d.OriginalFileName, d.StoredFileName, d.CreatedAt }).ToListAsync(ct);
        var banks = await BankFiles(db, userId).OrderByDescending(d => d.CreatedAt).Take(MaxCandidates)
            .Select(d => new { d.Id, d.OriginalFileName, d.StoredFileName, d.CreatedAt }).ToListAsync(ct);
        return TypedResults.Ok(
            materials.Select(d => new CourseCandidate(d.Id, d.OriginalFileName, Format(d.StoredFileName), nameof(CourseDocumentRole.Material), d.CreatedAt))
                .Concat(banks.Select(d => new CourseCandidate(d.Id, d.OriginalFileName, Format(d.StoredFileName), nameof(CourseDocumentRole.Bank), d.CreatedAt))));
    }

    private static async Task<IResult> Plan(Guid id, ClaimsPrincipal user, AppDbContext db, CourseInsights insights, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var course = await db.Set<StudyCourse>().AsNoTracking().Where(c => c.Id == id && c.UserId == userId)
            .Select(c => new { c.ExamDate }).FirstOrDefaultAsync(ct);
        if (course is null) return NotFound();
        var (materials, banks) = await FilesAsync(db, insights, userId, id, ct);

        var progress = new List<MaterialProgress>();
        foreach (var material in materials) progress.Add(await insights.CardsAsync(userId, material, ct));
        var (bankProgress, wrong) = await insights.BanksAsync(userId, banks, ct);
        var spots = await insights.WeakSpotsAsync(materials, wrong, ct);
        var daysLeft = CoursePlanner.DaysLeft(course.ExamDate, Today(clock));
        return TypedResults.Ok(new CoursePlan(daysLeft, CoursePlanner.Readiness(progress, bankProgress), progress, bankProgress, spots,
            CoursePlanner.Today(daysLeft, progress, bankProgress, spots)));
    }

    /// <summary>
    /// For a finished exam: where the lectures explain each question answered wrong or left blank, using the materials
    /// of the student's courses that contain the question's bank. Questions of other files get no link.
    /// </summary>
    private static async Task<IResult> ExamLinks(Guid examId, ClaimsPrincipal user, AppDbContext db, CourseInsights insights, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var exam = await db.Exams.AsNoTracking().Where(e => e.Id == examId && e.UserId == userId).Select(e => new { e.Status }).FirstOrDefaultAsync(ct);
        if (exam is null) return TypedResults.Problem("İmtahan tapılmadı.", statusCode: StatusCodes.Status404NotFound);
        // Links point at the answer: never while the exam is still running.
        if (exam.Status != ExamStatus.Completed) return TypedResults.Ok(Array.Empty<ExamQuestionLink>());

        var missed = await db.ExamQuestions.AsNoTracking()
            .Where(q => q.ExamId == examId && q.QuestionId != null && (q.Outcome == ExamOutcome.Incorrect || q.Outcome == ExamOutcome.Unanswered))
            .Select(q => new { q.Position, QuestionId = q.QuestionId!.Value, q.Question!.DocumentId }).ToListAsync(ct);
        if (missed.Count == 0) return TypedResults.Ok(Array.Empty<ExamQuestionLink>());

        var bankIds = missed.Select(m => m.DocumentId).Distinct().ToList();
        var courseIds = await db.Set<StudyCourseDocument>().AsNoTracking()
            .Where(d => d.Role == CourseDocumentRole.Bank && bankIds.Contains(d.DocumentId))
            .Join(db.Set<StudyCourse>().Where(c => c.UserId == userId), d => d.CourseId, c => c.Id, (d, c) => new { c.Id, d.DocumentId })
            .ToListAsync(ct);
        var links = new List<ExamQuestionLink>();
        foreach (var bank in courseIds.GroupBy(c => c.DocumentId))
        {
            var inCourses = bank.Select(b => b.Id).ToList();
            var materialFiles = await MaterialFiles(db, userId)
                .Where(f => db.Set<StudyCourseDocument>().Any(d => d.DocumentId == f.Id && d.Role == CourseDocumentRole.Material
                    && inCourses.Contains(d.CourseId)))
                .Select(f => new { f.Id, f.OriginalFileName, f.StoredFileName }).ToListAsync(ct);
            var materials = await insights.MaterialsAsync(userId, materialFiles.Select(f => (f.Id, f.OriginalFileName, f.StoredFileName)).ToList(), ct);
            var questions = missed.Where(m => m.DocumentId == bank.Key).ToList();
            var found = await insights.LinkAsync(materials, await insights.QueriesAsync(questions.Select(q => q.QuestionId).Distinct().ToList(), ct), ct);
            links.AddRange(questions.Where(q => found.ContainsKey(q.QuestionId)).Select(q =>
            {
                var (material, chunk) = found[q.QuestionId];
                var excerpt = chunk.Text.Length > ExcerptLength ? chunk.Text[..ExcerptLength].TrimEnd() + "…" : chunk.Text;
                return new ExamQuestionLink(q.Position, material.DocumentId, material.Name, material.Format, material.ChatId, chunk.Section,
                    chunk.PageFrom, chunk.PageTo, chunk.Ordinal, excerpt);
            }));
        }
        return TypedResults.Ok(links);
    }

    private static async Task<(List<CourseMaterial> Materials, List<(Guid Id, string Name)> Banks)> FilesAsync(AppDbContext db, CourseInsights insights,
        Guid userId, Guid courseId, CancellationToken ct)
    {
        // Only files the student can still read: a file whose share was withdrawn drops out of the plan.
        var rows = await db.Set<StudyCourseDocument>().AsNoTracking().Where(d => d.CourseId == courseId)
            .Join(DocumentAccess.Readable(db, userId), d => d.DocumentId, f => f.Id, (d, f) => new { d.Role, d.Position, f.Id, f.OriginalFileName, f.StoredFileName })
            .OrderBy(r => r.Position).ToListAsync(ct);
        var materials = await insights.MaterialsAsync(userId,
            rows.Where(r => r.Role == CourseDocumentRole.Material).Select(r => (r.Id, r.OriginalFileName, r.StoredFileName)).ToList(), ct);
        return (materials, rows.Where(r => r.Role == CourseDocumentRole.Bank).Select(r => (r.Id, r.OriginalFileName)).ToList());
    }

    private static async Task<CourseDetail?> DetailAsync(AppDbContext db, Guid userId, Guid id, DateOnly today, CancellationToken ct)
    {
        var course = await db.Set<StudyCourse>().AsNoTracking().Where(c => c.Id == id && c.UserId == userId)
            .Select(c => new { c.Id, c.Name, c.ExamDate }).FirstOrDefaultAsync(ct);
        if (course is null) return null;
        var rows = await db.Set<StudyCourseDocument>().AsNoTracking().Where(d => d.CourseId == id)
            .Join(DocumentAccess.Readable(db, userId), d => d.DocumentId, f => f.Id, (d, f) => new
            {
                d.Role, d.Position, f.Id, f.OriginalFileName, f.StoredFileName,
                Questions = db.Questions.Count(q => q.DocumentId == f.Id && q.Options.Count >= 2),
                Learning = db.Set<LearningDocument>().Where(l => l.DocumentId == f.Id).Select(l => new { l.Id, l.Status }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        rows = [.. rows.OrderBy(r => r.Role).ThenBy(r => r.Position)]; // materials first (the enum order, not its stored text)
        var learningIds = rows.Where(r => r.Learning != null).Select(r => r.Learning!.Id).ToList();
        var chats = await db.Set<LearningChatSession>().AsNoTracking().Where(s => s.UserId == userId && learningIds.Contains(s.LearningDocumentId))
            .Select(s => new { s.Id, s.LearningDocumentId, s.UpdatedAt }).ToListAsync(ct);
        var documents = rows.Select(r => r.Role == CourseDocumentRole.Material
            ? new CourseDocumentDto(r.Id, r.OriginalFileName, r.Role.ToString(), Format(r.StoredFileName), r.Learning?.Status.ToString(),
                r.Learning is null ? null : chats.Where(c => c.LearningDocumentId == r.Learning.Id).MaxBy(c => c.UpdatedAt)?.Id, null)
            : new CourseDocumentDto(r.Id, r.OriginalFileName, r.Role.ToString(), Format(r.StoredFileName), null, null, r.Questions)).ToList();
        return new CourseDetail(course.Id, course.Name, course.ExamDate, CoursePlanner.DaysLeft(course.ExamDate, today), documents);
    }

    private static string Format(string storedFileName) => Path.GetExtension(storedFileName).TrimStart('.').ToLowerInvariant();
    private static IResult Invalid(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
    private static IResult NotFound() => TypedResults.Problem("Fənn tapılmadı.", statusCode: StatusCodes.Status404NotFound);
}

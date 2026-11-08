using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Learning;

public sealed class QuizAnswerRequest
{
    [MaxLength(4000)] public string? Answer { get; init; }
    /// <summary>"Bilmirəm" button: graded INCORRECT without a model call.</summary>
    public bool GiveUp { get; init; }
}

public sealed class SharedAnswerRequest
{
    public bool Regenerate { get; init; }
    public bool Auto { get; init; }
    [MaxLength(40)] public int[]? QuestionNumbers { get; init; }
    public AnswerLength Length { get; init; } = AnswerLength.Short;
}

public sealed class LastMinuteRequest
{
    [Range(15, 60)] public int Minutes { get; init; } = 30;
}

public sealed class SaveMaterialRequest { public bool Saved { get; init; } }

/// <summary>Number is the question's number in the PDF; the client sends it back as "asked" so a session never repeats it.</summary>
public sealed record QuizQuestionDto(Guid Id, string Question, int? PageFrom, int? PageTo, int Number);
/// <summary>Status: Correct, Incorrect or Unchecked. CanRetry: not graded because of a technical problem, so "Yenidən yoxla" may try again.</summary>
public sealed record QuizResultDto(Guid Id, string Question, string Status, string Feedback, string CorrectAnswer, IReadOnlyList<string> MissingPoints,
    int? PageFrom, int? PageTo, bool AiChecked, bool CanRetry);
public sealed record QuizReviewItemDto(int Number, string Question, string CorrectAnswer, int? Page, DateTimeOffset AnsweredAt);
public sealed record WeakTopicDto(string Title, IReadOnlyList<int> Numbers, int Wrong, int Answered);
/// <summary>Per question, by the latest answer: Correct / Incorrect counts, Unchecked = questions never graded. Review = questions last answered wrong.</summary>
public sealed record QuizReviewDto(int Correct, int Incorrect, int Unchecked, int Questions, IReadOnlyList<QuizReviewItemDto> Review, IReadOnlyList<WeakTopicDto> WeakTopics,
    QuizQuestionDto? Pending = null);
public sealed record OpenQuestionsDto(bool Supported, string? Warning, IReadOnlyList<OpenQuestion> Questions);
public sealed record MaterialSummaryDto(Guid Id, string Kind, string? Length, int? Minutes, bool Saved, string Title, int Items, DateTimeOffset CreatedAt, string? GeneratorVersion = null);
public sealed record MaterialDto(Guid Id, string Kind, string? Length, int? Minutes, bool Saved, bool FromCache, DateTimeOffset CreatedAt, JsonElement Payload);

/// <summary>AI Tutor 2.0 study tools: "Məni yoxla" quiz, shared answers for open questions, last-minute revision sheet.</summary>
public static partial class LearningStudyEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapLearningStudyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/learning").RequireAuthorization().AddEndpointFilter<ExamFlow.Api.Features.Usage.UsageLimitFilter>();
        group.MapPost("/chats/{sessionId:guid}/quiz", NextQuestion).RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapPost("/quiz/{itemId:guid}/answer", Answer).Validate<QuizAnswerRequest>().RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapGet("/chats/{sessionId:guid}/quiz/review", Review);
        group.MapGet("/chats/{sessionId:guid}/open-questions", OpenQuestions);
        group.MapPost("/chats/{sessionId:guid}/shared-answers", SharedAnswers).Validate<SharedAnswerRequest>().RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapPost("/chats/{sessionId:guid}/last-minute", LastMinute).Validate<LastMinuteRequest>().RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapGet("/chats/{sessionId:guid}/materials", Materials);
        group.MapGet("/materials/{id:guid}", Material);
        group.MapPost("/materials/{id:guid}/save", SaveMaterial).Validate<SaveMaterialRequest>();
        group.MapDelete("/materials/{id:guid}", DeleteMaterial);
        MapPrepare(group);
        MapCards(group);
    }

    private sealed record StudyDoc(Guid LearningId, Guid DocumentId, byte[] Sha256, string Format);

    /// <summary>The session's document, only while the user still owns the session and may read the document.</summary>
    private static async Task<StudyDoc?> FromSession(AppDbContext db, Guid userId, Guid sessionId, CancellationToken ct)
    {
        var learningId = await db.Set<LearningChatSession>().AsNoTracking().Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => (Guid?)s.LearningDocumentId).FirstOrDefaultAsync(ct);
        return learningId is null ? null : await FromLearning(db, userId, learningId.Value, ct);
    }

    private static async Task<StudyDoc?> FromLearning(AppDbContext db, Guid userId, Guid learningId, CancellationToken ct)
    {
        var doc = await db.Set<LearningDocument>().AsNoTracking().Where(l => l.Id == learningId && l.Status == LearningStatus.Completed)
            .Select(l => new { l.DocumentId, l.Document.Sha256, l.Format }).FirstOrDefaultAsync(ct);
        if (doc is null || !await DocumentAccess.Readable(db, userId).AnyAsync(d => d.Id == doc.DocumentId, ct)) return null;
        return new StudyDoc(learningId, doc.DocumentId, doc.Sha256, doc.Format);
    }

    /// <summary>The session's document and its open questions, or the reason the tutor does not work with it.</summary>
    private static async Task<(StudyDoc? Doc, OpenQuestionIndex? Index, IResult? Error)> OpenDocAsync(AppDbContext db, OpenQuestionIndexer indexer, Guid userId,
        Guid sessionId, CancellationToken ct)
    {
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return (null, null, NotFound());
        if (await indexer.GetAsync(doc.LearningId, ct) is not { } index) return (null, null, NotFound());
        return index.Supported ? (doc, index, null) : (null, null, Unsupported(index));
    }

    private static IResult Unsupported(OpenQuestionIndex index) =>
        TypedResults.Problem(index.Rejection, statusCode: StatusCodes.Status422UnprocessableEntity);

    // ---------- Məni yoxla ----------

    /// <summary>Asks one of the document's own open questions (no AI call); its document answer is the reference for grading.</summary>
    /// <param name="numbers">Optional filter: one Hazırlaş topic, or the review list ("Səhv etdiklərimi təkrar et").</param>
    /// <param name="asked">Question numbers already asked in this session; they are never asked again in it.</param>
    private static async Task<IResult> NextQuestion(Guid sessionId, ClaimsPrincipal user, AppDbContext db, OpenQuestionIndexer indexer, TimeProvider clock,
        [FromQuery] int[]? numbers, [FromQuery] int[]? asked, CancellationToken ct)
    {
        if (numbers?.Length > QuizLogic.MaxNumbers || asked?.Length > QuizLogic.MaxNumbers)
            return Bad($"Bir sessiyada ən çox {QuizLogic.MaxNumbers} sual ola bilər.");
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;

        var recent = await db.Set<LearningQuizItem>().AsNoTracking().Where(q => q.UserId == userId && q.LearningDocumentId == doc!.LearningId)
            .OrderByDescending(q => q.CreatedAt).Take(60).Select(q => new QuizAttempt(q.Question, q.Status, q.AnsweredAt ?? q.CreatedAt)).ToListAsync(ct);
        if (QuizLogic.Pick(index!.Questions, numbers, asked, recent, Random.Shared) is not { } picked)
            return TypedResults.Problem(asked is { Length: > 0 } ? "Bu sessiyada bütün suallar soruşuldu." : "Seçilmiş suallar sənəddə tapılmadı.",
                statusCode: StatusCodes.Status409Conflict);

        var item = new LearningQuizItem
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc!.LearningId, Status = QuizStatus.Pending, CreatedAt = clock.GetUtcNow(),
            Question = QuizLogic.Key(picked.Text), ReferenceAnswer = Shorten(picked.Answer, 2000), KeyPointsJson = "[]",
            ChunkOrdinal = picked.ChunkOrdinal, PageFrom = picked.Page, PageTo = picked.Page,
        };
        db.Add(item);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new QuizQuestionDto(item.Id, item.Question, item.PageFrom, item.PageTo, picked.Number));
    }

    /// <summary>Grades by meaning against the document's answer: Correct, Incorrect, or Unchecked when no reliable verdict is possible.</summary>
    private static async Task<IResult> Answer(Guid itemId, QuizAnswerRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var item = await db.Set<LearningQuizItem>().FirstOrDefaultAsync(q => q.Id == itemId && q.UserId == userId, ct);
        if (item is null || await FromLearning(db, userId, item.LearningDocumentId, ct) is not { } doc) return NotFound();
        // Graded answers are final (never re-graded or re-billed). Only "Yoxlanılmadı" after a technical problem may be retried.
        if (item.Status != QuizStatus.Pending && !CanRetry(item)) return TypedResults.Ok(ToResult(item));
        if (!OpenQuestionIndexer.IsSupportedFormat(doc.Format))
            return Unsupported(new OpenQuestionIndex(OpenQuestionClassifier.Version, OpenDocumentKind.NotPdf, 0, 0, 0, []));

        if (item.Status == QuizStatus.Pending) item.UserAnswer = request.Answer?.Trim(); // a retry grades the answer already given
        if (item.Status == QuizStatus.Pending && (request.GiveUp || RefusalDetector.IsRefusal(request.Answer)))
        {
            // No model call: "I don't know" is always wrong; the document's answer is shown and the question goes to review.
            item.Status = QuizStatus.Incorrect;
            item.Feedback = request.GiveUp
                ? "Düzgün cavab aşağıdadır. Sual təkrar siyahısına əlavə olundu."
                : "Cavab verilmədi. Düzgün cavab aşağıdadır; sual təkrar siyahısına əlavə olundu.";
            item.MissingPointsJson = "[]";
            item.AiChecked = false;
        }
        else
        {
            // Only the stored reference (the document's answer) is sent: no chunk lookup, no re-sending of document parts.
            var keyPoints = JsonSerializer.Deserialize<string[]>(item.KeyPointsJson, Json) ?? [];
            var input = new StringBuilder()
                .Append("<source pages=\"").Append(StudyPrompts.PageText(item.PageFrom, item.PageTo)).Append("\">\n")
                .Append(TutorPrompt.Neutralise(item.ReferenceAnswer))
                .Append(keyPoints.Length > 0 ? "\nKey points: " + TutorPrompt.Neutralise(string.Join("; ", keyPoints)) : "").Append("\n</source>\n")
                .Append("<question>\n").Append(TutorPrompt.Neutralise(item.Question)).Append("\n</question>\n")
                .Append("<answer>\n").Append(TutorPrompt.Neutralise(item.UserAnswer ?? "")).Append("\n</answer>\n")
                .Append(StudyPrompts.GradeTask).ToString();
            var result = await ai.RunAsync(userId, user.IsInRole(Roles.Admin),
                [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(item.Question))), new("user", input)],
                "grade_answer", StudyPrompts.GradeSchema(), 350, light: false, ct);
            var grade = result.Ok ? TryParseGrade(result.Json!.Value) : null;
            if (grade is { } g)
            {
                item.Status = g.Status;
                item.Feedback = g.Feedback;
                item.MissingPointsJson = JsonSerializer.Serialize(g.Missing, Json);
                item.AiChecked = true; // ReferenceAnswer stays the document's own text
            }
            else
            {
                // Technical problem (AI off, limit, timeout, invalid output): never an invented grade. The answer is kept as
                // "Yoxlanılmadı" with the document's answer, and the student may retry the check.
                item.Status = QuizStatus.Unchecked;
                item.Feedback = result.Ok ? "AI cavabı yoxlana bilmədi." : result.Error ?? "Cavab yoxlana bilmədi.";
                item.MissingPointsJson = "[]";
                item.AiChecked = false;
            }
        }
        item.AnsweredAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToResult(item));
    }

    /// <summary>
    /// The student's standing on this document, by each question's latest answer: counts, the review list (last answered wrong,
    /// stored per user and document) and weak topics. Weak topics use the document's question groups (made once, then cached).
    /// </summary>
    private static async Task<IResult> Review(Guid sessionId, ClaimsPrincipal user, AppDbContext db, StudyAi ai, OpenQuestionIndexer indexer,
        TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;
        var attempts = await db.Set<LearningQuizItem>().AsNoTracking()
            .Where(q => q.UserId == userId && q.LearningDocumentId == doc!.LearningId && q.Status != QuizStatus.Pending)
            .OrderByDescending(q => q.AnsweredAt).Take(2000)
            .Select(q => new { Attempt = new QuizAttempt(q.Question, q.Status, q.AnsweredAt ?? q.CreatedAt), q.ReferenceAnswer, q.PageFrom }).ToListAsync(ct);

        var latest = QuizLogic.Latest(attempts.Select(a => a.Attempt));
        var byKey = index!.Questions.GroupBy(q => QuizLogic.Key(q.Text)).ToDictionary(g => g.Key, g => g.First());
        var notGraded = attempts.Select(a => a.Attempt.Question).Distinct().Count(q => !latest.ContainsKey(q));
        var review = latest.Values.Where(a => QuizLogic.IsWrong(a.Status) && byKey.ContainsKey(a.Question)).OrderByDescending(a => a.At).Take(QuizLogic.MaxNumbers)
            .Select(a => (Attempt: a, Question: byKey[a.Question]))
            .Select(x => new QuizReviewItemDto(x.Question.Number, x.Attempt.Question, Shorten(x.Question.Answer, 2000), x.Question.Page, x.Attempt.At)).ToList();

        IReadOnlyList<WeakTopicDto> weak = [];
        if (review.Count > 0)
        {
            // Grouping is the cheap, cached step Hazırlaş and Son təkrar use too; if it cannot run now, the list just stays empty.
            var (groups, _) = await GroupsAsync(db, ai, userId, user.IsInRole(Roles.Admin), doc!, index.ToScan(), index.Fingerprint(), clock, ct);
            if (groups is not null)
                weak = QuizLogic.WeakTopics(latest, index.Questions, groups.Select(g => (g.Title, (IReadOnlyList<int>)g.Numbers)))
                    .Select(t => new WeakTopicDto(t.Title, t.Numbers, t.Wrong, t.Answered)).ToList();
        }
        // "Qaldığın yerdən davam et": the newest question was asked but not answered (left mid-session), recently.
        var newest = await db.Set<LearningQuizItem>().AsNoTracking().Where(q => q.UserId == userId && q.LearningDocumentId == doc!.LearningId)
            .OrderByDescending(q => q.CreatedAt).Select(q => new { q.Id, q.Question, q.Status, q.PageFrom, q.PageTo, q.CreatedAt }).FirstOrDefaultAsync(ct);
        var pending = newest is { Status: QuizStatus.Pending } && newest.CreatedAt > clock.GetUtcNow().AddDays(-7) && byKey.TryGetValue(newest.Question, out var open)
            ? new QuizQuestionDto(newest.Id, newest.Question, newest.PageFrom, newest.PageTo, open.Number) : null;

        return TypedResults.Ok(new QuizReviewDto(latest.Values.Count(a => a.Status == QuizStatus.Correct), latest.Values.Count(a => QuizLogic.IsWrong(a.Status)),
            notGraded, index.Questions.Count, review, weak, pending));
    }

    private static bool CanRetry(LearningQuizItem q) => q.Status == QuizStatus.Unchecked && !q.AiChecked;

    private static (QuizStatus Status, string Feedback, IReadOnlyList<string> Missing)? TryParseGrade(JsonElement json)
    {
        try { return QuizLogic.ParseGrade(json); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    private static QuizResultDto ToResult(LearningQuizItem q) => new(q.Id, q.Question,
        (QuizLogic.IsWrong(q.Status) ? QuizStatus.Incorrect : q.Status).ToString(), q.Feedback ?? "", q.ReferenceAnswer,
        q.MissingPointsJson is null ? [] : JsonSerializer.Deserialize<string[]>(q.MissingPointsJson, Json) ?? [], q.PageFrom, q.PageTo, q.AiChecked, CanRetry(q));

    // ---------- Ortaq cavab ----------

    private static async Task<IResult> OpenQuestions(Guid sessionId, ClaimsPrincipal user, AppDbContext db, OpenQuestionIndexer indexer, CancellationToken ct)
    {
        if (await FromSession(db, user.GetUserId(), sessionId, ct) is not { } doc) return NotFound();
        if (await indexer.GetAsync(doc.LearningId, ct) is not { } index) return NotFound();
        return TypedResults.Ok(index.Supported
            ? new OpenQuestionsDto(true, index.Warning, index.ToScan().Questions)
            : new OpenQuestionsDto(false, index.Rejection, []));
    }

    /// <summary>Section: the broad main topic (empty for groups stored before sections existed).</summary>
    private sealed record QuestionGroup(string Title, List<int> Numbers, string? Section = null);

    /// <summary>
    /// Topic groups of ALL open questions, made once per document version with the cheap model and reused by shared answers and
    /// last-minute notes (stored as a QuestionGroups material shared by every reader of the same file version).
    /// </summary>
    private static async Task<(List<QuestionGroup>? Groups, IResult? Error)> GroupsAsync(AppDbContext db, StudyAi ai, Guid userId, bool admin, StudyDoc doc,
        OpenQuestionScan scan, string fingerprint, TimeProvider clock, CancellationToken ct)
    {
        var key = StudyPrompts.CacheKey("groups", doc.LearningId, doc.Sha256, fingerprint, StudyPrompts.Version);
        var cached = await db.Set<LearningMaterial>().AsNoTracking()
            .Where(m => m.LearningDocumentId == doc.LearningId && m.CacheKey == key).Select(m => m.PayloadJson).FirstOrDefaultAsync(ct);
        if (cached is not null) return (JsonSerializer.Deserialize<List<QuestionGroup>>(cached, Json), null);

        var byNumber = scan.Questions.ToDictionary(q => q.Number);
        var all = scan.Questions.Select(q => q.Number).ToList();
        var groups = new List<(string Section, string Title, List<int> Numbers)>();
        // Long documents are grouped in slices, so no question is cut off by a token limit.
        foreach (var slice in all.Chunk(100))
        {
            // The start of each answer shows what a question is really about (meaning, not just shared words).
            var list = string.Join('\n', slice.Select(n => $"{n}. {TutorPrompt.Neutralise(Shorten(byNumber[n].Text, 220))}"
                + (scan.Answers.TryGetValue(n, out var a) && a.Length > 0 ? $"\n   ↳ {TutorPrompt.Neutralise(Shorten(a.Replace('\n', ' '), 160))}" : "")));
            var result = await ai.RunAsync(userId, admin,
                [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(list))), new("user", $"<question>\n{list}\n</question>\n{StudyPrompts.GroupTask}")],
                "group_questions", StudyPrompts.GroupSchema(), Math.Min(6000, 300 + slice.Length * 26), light: true, ct);
            if (!result.Ok) return (null, TypedResults.Problem(result.Error, statusCode: result.Status));
            try
            {
                groups.AddRange(StudyJson.NormalizeGroups(result.Json!.Value.GetProperty("groups").EnumerateArray()
                    .Select(g => (g.GetProperty("section").GetString()?.Trim() ?? "", StudyJson.Text(g, "title", 120), StudyJson.Numbers(g, "questionNumbers"))),
                    slice, n => Shorten(byNumber[n].Text, 80)));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return (null, InvalidOutput()); }
        }

        // Duplicate topics (also across slices) are merged; then main topics by size, their subtopics by size.
        var merged = StudyJson.MergeSimilar(groups.Select(g => (g.Section.Length > 120 ? g.Section[..120] : g.Section, g.Title, g.Numbers)));
        var sectionSize = merged.GroupBy(g => g.Section).ToDictionary(g => g.Key, g => g.Sum(x => x.Numbers.Count));
        var ordered = merged.Select(g => new QuestionGroup(g.Title, g.Numbers, g.Section))
            .OrderByDescending(g => g.Section is { Length: > 0 } s ? sectionSize[s] : g.Numbers.Count).ThenBy(g => g.Section)
            .ThenByDescending(g => g.Numbers.Count).ThenBy(g => g.Numbers.Min()).ToList();
        db.Add(new LearningMaterial
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc.LearningId, DocumentSha256 = doc.Sha256, Kind = MaterialKind.QuestionGroups,
            CacheKey = key, PromptVersion = StudyPrompts.Version, PayloadJson = JsonSerializer.Serialize(ordered, Json), CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        return (ordered, null);
    }

    /// <summary>Document text behind a question: its own answer when the document has one, otherwise the closest indexed parts.</summary>
    private static async Task<(string Text, List<LearningDocumentChunk> Chunks)> QuestionSourceAsync(AppDbContext db, IChunkRetriever retriever, StudyDoc doc,
        OpenQuestionScan scan, OpenQuestion question, int maxChars, CancellationToken ct)
    {
        var ordinals = new List<int> { question.ChunkOrdinal };
        string text;
        if (scan.Answers.TryGetValue(question.Number, out var own) && own.Length >= OpenQuestionClassifier.MinAnswerCharacters)
            text = $"{question.Number}. {question.Text}\n{own}"; // the question keeps its answer's context
        else
        {
            var hits = await retriever.SearchAsync(doc.LearningId, question.Text, 2, ct);
            ordinals.AddRange(hits.Chunks.Select(h => h.Ordinal));
            text = string.Join("\n", hits.Chunks.Select(h => h.Text));
        }
        var chunks = await db.Set<LearningDocumentChunk>().AsNoTracking()
            .Where(c => c.LearningDocumentId == doc.LearningId && ordinals.Contains(c.Ordinal)).ToListAsync(ct);
        return (text.Length > maxChars ? text[..maxChars] : text, chunks);
    }

    private static async Task<IResult> SharedAnswers(Guid sessionId, SharedAnswerRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai,
        OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return error;
        // Built only from the document's open questions and their answers, so every reader of this version shares it.
        var units = SharedAnswerGenerator.QuestionUnits(index!.Questions);
        var key = SharedAnswerGenerator.CacheKey(doc!.DocumentId, doc.Sha256, index.Fingerprint());
        if (!request.Regenerate && await FromCache(db, userId, doc.LearningId, key, mine: false, clock, ct) is { } cached)
            return TypedResults.Ok(cached);

        var result = await SharedAnswerGenerator.GenerateAsync(units,
            (messages, schemaName, schema, tokens, token) => ai.RunAsync(userId, user.IsInRole(Roles.Admin),
                messages, schemaName, schema, tokens, light: false, token), ct);
        if (result.Payload is null) return TypedResults.Problem(result.Error, statusCode: result.Status);
        var material = new LearningMaterial
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc.LearningId, DocumentSha256 = doc.Sha256,
            Kind = MaterialKind.SharedAnswer, CacheKey = key, PromptVersion = SharedAnswerGenerator.Version,
            PayloadJson = JsonSerializer.Serialize(result.Payload, Json), CreatedAt = clock.GetUtcNow(),
        };
        db.Add(material);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(material, fromCache: false));
    }

    // ---------- Son dəqiqə hazırlığı ----------

    private static async Task<IResult> LastMinute(Guid sessionId, LastMinuteRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai,
        IChunkRetriever retriever, OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var admin = user.IsInRole(Roles.Admin);
        var (doc, index, openError) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (openError is not null) return openError;
        var minutes = request.Minutes <= 15 ? 15 : request.Minutes <= 30 ? 30 : 60;
        var fingerprint = index!.Fingerprint();

        // Built only from the document (no personal test history), so readers of the same version share it.
        var key = StudyPrompts.CacheKey("notes", doc!.LearningId, doc.Sha256, fingerprint, minutes, StudyPrompts.Version);
        if (await FromCache(db, userId, doc.LearningId, key, mine: false, clock, ct) is { } cached) return TypedResults.Ok(cached);

        var topics = minutes switch { 15 => 5, 30 => 8, _ => 14 };
        var charBudget = minutes switch { 15 => 9000, 30 => 15000, _ => 26000 };
        var scan = index.ToScan();
        var sources = new StringBuilder();
        var used = new List<LearningDocumentChunk>();
        const bool fromQuestions = true; // only open-question documents reach this point

        // Topics asked about in the most questions first; each topic is backed by its questions' document answers.
        var (groups, error) = await GroupsAsync(db, ai, userId, admin, doc, scan, fingerprint, clock, ct);
        if (error is not null) return error;
        var byNumber = scan.Questions.ToDictionary(q => q.Number);
        var picked = groups!.Take(topics).ToList();
        var perTopic = charBudget / Math.Max(1, picked.Count);
        for (var i = 0; i < picked.Count; i++)
        {
            sources.Append($"<topic priority=\"{i + 1}\" questions=\"{picked[i].Numbers.Count}\" title=\"{TutorPrompt.Neutralise(picked[i].Title).Replace("\"", "'")}\">\n");
            foreach (var n in picked[i].Numbers.Take(4))
            {
                var (text, cs) = await QuestionSourceAsync(db, retriever, doc, scan, byNumber[n], perTopic / Math.Min(4, picked[i].Numbers.Count), ct);
                used.AddRange(cs);
                sources.Append($"<source>\n{TutorPrompt.Neutralise(text)}\n</source>\n");
            }
            sources.Append("</topic>\n");
        }

        var first = index.Questions[0];
        var result = await ai.RunAsync(userId, admin,
            [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(first.Text + " " + first.Answer))), new("user", sources + StudyPrompts.NotesTask(minutes, topics, fromQuestions))],
            "study_notes", StudyPrompts.NotesSchema(), minutes switch { 15 => 1300, 30 => 2200, _ => 3800 }, light: false, ct);
        if (!result.Ok) return TypedResults.Problem(result.Error, statusCode: result.Status);

        try
        {
            var json = result.Json!.Value;
            var notes = json.GetProperty("topics").EnumerateArray().Take(topics).Select(t => new
            {
                Title = StudyJson.Text(t, "title", 120), Explanation = StudyJson.Text(t, "explanation", 1500),
                Remember = StudyJson.Texts(t, "remember", 5, 160), Related = StudyJson.Texts(t, "related", 4, 80),
            }).ToList();
            if (notes.Count == 0) throw new JsonException("topics");
            // About 180 words per minute; the sheet is never padded to reach the chosen time.
            var words = notes.Sum(n => (n.Title + " " + n.Explanation + " " + string.Join(' ', n.Remember)).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            var payload = new
            {
                Topics = notes, KeyTerms = StudyJson.Texts(json, "keyTerms", 25, 60), SourcePages = StudyPrompts.Pages(used),
                ReadingMinutes = Math.Max(1, (int)Math.Round(words / 180.0)), FromQuestions = fromQuestions,
            };
            var material = new LearningMaterial
            {
                Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc.LearningId, DocumentSha256 = doc.Sha256, Kind = MaterialKind.LastMinute,
                Minutes = minutes, CacheKey = key, PromptVersion = StudyPrompts.Version, PayloadJson = JsonSerializer.Serialize(payload, Json), CreatedAt = clock.GetUtcNow(),
            };
            db.Add(material);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToDto(material, fromCache: false));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return InvalidOutput(); }
    }

    // ---------- Saved materials ----------

    /// <summary>
    /// Reuses a result for the same document version and request. A shared-answer result made by another reader is copied into this
    /// user's list, so later deletes or saves never touch someone else's row.
    /// </summary>
    private static async Task<MaterialDto?> FromCache(AppDbContext db, Guid userId, Guid learningId, string key, bool mine, TimeProvider clock, CancellationToken ct)
    {
        var hits = db.Set<LearningMaterial>().Where(m => m.LearningDocumentId == learningId && m.CacheKey == key);
        if (mine) hits = hits.Where(m => m.UserId == userId);
        var hit = await hits.OrderByDescending(m => m.UserId == userId).ThenByDescending(m => m.CreatedAt).FirstOrDefaultAsync(ct);
        if (hit is null) return null;
        if (hit.UserId == userId) return ToDto(hit, fromCache: true);
        var copy = new LearningMaterial
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = learningId, DocumentSha256 = hit.DocumentSha256, Kind = hit.Kind, Length = hit.Length,
            Minutes = hit.Minutes, QuestionNumbersJson = hit.QuestionNumbersJson, PayloadJson = hit.PayloadJson, CacheKey = key, PromptVersion = hit.PromptVersion,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Add(copy);
        await db.SaveChangesAsync(ct);
        return ToDto(copy, fromCache: true);
    }

    private static async Task<IResult> Materials(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return NotFound();
        // An indexed document version never changes, so the prompt version alone tells current results from old ones.
        var rows = await db.Set<LearningMaterial>().AsNoTracking().Where(m => m.UserId == userId && m.LearningDocumentId == doc.LearningId
                && ((m.Kind == MaterialKind.LastMinute && m.PromptVersion == StudyPrompts.Version)
                    || (m.Kind == MaterialKind.SharedAnswer && m.PromptVersion == SharedAnswerGenerator.Version)))
            .OrderByDescending(m => m.Saved).ThenByDescending(m => m.CreatedAt).Take(30).ToListAsync(ct);
        return TypedResults.Ok(rows.Select(m =>
        {
            using var payload = JsonDocument.Parse(m.PayloadJson);
            var root = payload.RootElement;
            var (title, count) = m.Kind == MaterialKind.LastMinute
                ? ($"Son dəqiqə · {m.Minutes} dəq", root.TryGetProperty("topics", out var s) ? s.GetArrayLength() : 0)
                : (root.GetProperty("mainTopic").GetString() ?? "Ortaq cavab", 1);
            return new MaterialSummaryDto(m.Id, m.Kind.ToString(), m.Length?.ToString(), m.Minutes, m.Saved, title, count, m.CreatedAt, m.Kind == MaterialKind.SharedAnswer ? m.PromptVersion : null);
        }).ToList());
    }

    private static async Task<LearningMaterial?> OwnMaterial(AppDbContext db, Guid userId, Guid id, CancellationToken ct)
    {
        var material = await db.Set<LearningMaterial>().FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        // Losing access to the document also closes the materials made from it.
        return material is null || await FromLearning(db, userId, material.LearningDocumentId, ct) is null ? null : material;
    }

    private static async Task<IResult> Material(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await OwnMaterial(db, user.GetUserId(), id, ct) is { } m ? TypedResults.Ok(ToDto(m, fromCache: false)) : NotFound();

    private static async Task<IResult> SaveMaterial(Guid id, SaveMaterialRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await OwnMaterial(db, user.GetUserId(), id, ct) is not { } m) return NotFound();
        m.Saved = request.Saved;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteMaterial(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var deleted = await db.Set<LearningMaterial>().Where(m => m.Id == id && m.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : TypedResults.NoContent();
    }

    private static MaterialDto ToDto(LearningMaterial m, bool fromCache)
    {
        using var payload = JsonDocument.Parse(m.PayloadJson);
        return new MaterialDto(m.Id, m.Kind.ToString(), m.Length?.ToString(), m.Minutes, m.Saved, fromCache, m.CreatedAt, payload.RootElement.Clone());
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
    private static IResult NotFound() => TypedResults.Problem("Söhbət və ya sənəd tapılmadı.", statusCode: StatusCodes.Status404NotFound);
    private static IResult Bad(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
    private static IResult InvalidOutput() =>
        TypedResults.Problem("AI cavabı yoxlana bilmədi. Yenidən cəhd edin.", statusCode: StatusCodes.Status502BadGateway);
}

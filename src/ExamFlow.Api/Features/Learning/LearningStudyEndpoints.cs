using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
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
    public bool Auto { get; init; }
    [MaxLength(40)] public int[]? QuestionNumbers { get; init; }
    public AnswerLength Length { get; init; } = AnswerLength.Short;
}

public sealed class LastMinuteRequest
{
    [Range(15, 60)] public int Minutes { get; init; } = 30;
}

public sealed class SaveMaterialRequest { public bool Saved { get; init; } }

public sealed record QuizQuestionDto(Guid Id, string Question, int? PageFrom, int? PageTo);
public sealed record QuizResultDto(Guid Id, string Question, string Status, string Feedback, string CorrectAnswer, IReadOnlyList<string> MissingPoints,
    int? PageFrom, int? PageTo, bool AiChecked);
public sealed record QuizReviewDto(int Correct, int Partial, int Incorrect, IReadOnlyList<QuizResultDto> Items);
public sealed record OpenQuestionsDto(bool Supported, string? Warning, IReadOnlyList<OpenQuestion> Questions);
public sealed record MaterialSummaryDto(Guid Id, string Kind, string? Length, int? Minutes, bool Saved, string Title, int Items, DateTimeOffset CreatedAt);
public sealed record MaterialDto(Guid Id, string Kind, string? Length, int? Minutes, bool Saved, bool FromCache, DateTimeOffset CreatedAt, JsonElement Payload);

/// <summary>AI Tutor 2.0 study tools: "Məni yoxla" quiz, shared answers for open questions, last-minute revision sheet.</summary>
public static class LearningStudyEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int MaxAutoQuestions = 60;

    public static void MapLearningStudyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/learning").RequireAuthorization();
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
    }

    private sealed record StudyDoc(Guid LearningId, Guid DocumentId, byte[] Sha256);

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
            .Select(l => new { l.DocumentId, l.Document.Sha256 }).FirstOrDefaultAsync(ct);
        if (doc is null || !await DocumentAccess.Readable(db, userId).AnyAsync(d => d.Id == doc.DocumentId, ct)) return null;
        return new StudyDoc(learningId, doc.DocumentId, doc.Sha256);
    }

    // ---------- Məni yoxla ----------

    private static async Task<IResult> NextQuestion(Guid sessionId, ClaimsPrincipal user, AppDbContext db, StudyAi ai, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return NotFound();
        var chunks = await TeachingStep.ChunksAsync(db, doc.LearningId, ct);
        if (chunks.Count == 0) return TypedResults.Problem("Sənəddə sual hazırlamaq üçün mətn tapılmadı.", statusCode: 409);

        // Prefer parts the student has not been asked about recently; sometimes return to a weak part.
        var recent = await db.Set<LearningQuizItem>().AsNoTracking().Where(q => q.UserId == userId && q.LearningDocumentId == doc.LearningId)
            .OrderByDescending(q => q.CreatedAt).Take(40).Select(q => new { q.ChunkOrdinal, q.Status }).ToListAsync(ct);
        var weak = recent.Where(r => r.Status is QuizStatus.Incorrect or QuizStatus.Partial).Select(r => r.ChunkOrdinal).Distinct().ToList();
        var lastAsked = recent.Take(8).Select(r => r.ChunkOrdinal).ToHashSet();
        var candidates = chunks.Where(c => c.Text.Length >= 300 && !lastAsked.Contains(c.Ordinal)).ToList();
        if (candidates.Count == 0) candidates = chunks;
        var chunk = weak.Count > 0 && Random.Shared.NextDouble() < 0.35
            ? chunks.FirstOrDefault(c => c.Ordinal == weak[Random.Shared.Next(weak.Count)]) ?? candidates[Random.Shared.Next(candidates.Count)]
            : candidates[Random.Shared.Next(candidates.Count)];

        var language = TutorPrompt.DetectLanguage(chunk.Text);
        var result = await ai.RunAsync(userId, user.IsInRole(Roles.Admin),
            [new("system", StudyPrompts.System(language)), new("user", StudyPrompts.Sources([chunk]) + StudyPrompts.QuizTask)],
            "quiz_question", StudyPrompts.QuizSchema(), 400, light: false, ct);
        if (!result.Ok) return TypedResults.Problem(result.Error, statusCode: result.Status);

        try
        {
            var json = result.Json!.Value;
            var item = new LearningQuizItem
            {
                Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc.LearningId, Status = QuizStatus.Pending, CreatedAt = clock.GetUtcNow(),
                Question = StudyJson.Text(json, "question", 1000), ReferenceAnswer = StudyJson.Text(json, "answer", 2000),
                KeyPointsJson = JsonSerializer.Serialize(StudyJson.Texts(json, "keyPoints", 4, 300), Json),
                ChunkOrdinal = chunk.Ordinal, PageFrom = chunk.PageFrom, PageTo = chunk.PageTo,
            };
            db.Add(item);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(new QuizQuestionDto(item.Id, item.Question, item.PageFrom, item.PageTo));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return InvalidOutput();
        }
    }

    private static async Task<IResult> Answer(Guid itemId, QuizAnswerRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var item = await db.Set<LearningQuizItem>().FirstOrDefaultAsync(q => q.Id == itemId && q.UserId == userId, ct);
        if (item is null || await FromLearning(db, userId, item.LearningDocumentId, ct) is null) return NotFound();
        if (item.Status != QuizStatus.Pending) return TypedResults.Ok(ToResult(item)); // already graded: never re-grade or re-bill

        item.UserAnswer = request.Answer?.Trim();
        if (request.GiveUp || RefusalDetector.IsRefusal(request.Answer))
        {
            // No model call: "I don't know" is always INCORRECT; the stored document-based answer is shown.
            item.Status = QuizStatus.Incorrect;
            item.Feedback = request.GiveUp ? "Bu sualı sonra təkrar edin." : "Cavab verilmədi.";
            item.MissingPointsJson = item.KeyPointsJson;
            item.AiChecked = false;
        }
        else
        {
            var chunk = await db.Set<LearningDocumentChunk>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.LearningDocumentId == item.LearningDocumentId && c.Ordinal == item.ChunkOrdinal, ct);
            var keyPoints = JsonSerializer.Deserialize<string[]>(item.KeyPointsJson, Json) ?? [];
            var input = new StringBuilder()
                .Append(chunk is null ? "" : StudyPrompts.Sources([chunk]))
                .Append("<question>\n").Append(TutorPrompt.Neutralise(item.Question)).Append("\n</question>\n")
                .Append("<reference>\n").Append(TutorPrompt.Neutralise(item.ReferenceAnswer))
                .Append(keyPoints.Length > 0 ? "\nKey points: " + TutorPrompt.Neutralise(string.Join("; ", keyPoints)) : "").Append("\n</reference>\n")
                .Append("<answer>\n").Append(TutorPrompt.Neutralise(item.UserAnswer ?? "")).Append("\n</answer>\n")
                .Append(StudyPrompts.GradeTask).ToString();
            var result = await ai.RunAsync(userId, user.IsInRole(Roles.Admin),
                [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(item.Question))), new("user", input)],
                "grade_answer", StudyPrompts.GradeSchema(), 350, light: false, ct);
            // Grading failed: say so and keep the question open; never show an invented grade.
            if (!result.Ok) return TypedResults.Problem(result.Error, statusCode: result.Status);
            try
            {
                var json = result.Json!.Value;
                item.Status = json.GetProperty("status").GetString() switch
                {
                    "CORRECT" => QuizStatus.Correct,
                    "PARTIAL" => QuizStatus.Partial,
                    "INCORRECT" => QuizStatus.Incorrect,
                    _ => throw new JsonException("status"),
                };
                item.Feedback = StudyJson.Text(json, "feedback", 300);
                item.MissingPointsJson = JsonSerializer.Serialize(item.Status == QuizStatus.Correct ? [] : StudyJson.Texts(json, "missingPoints", 5, 200), Json);
                item.ReferenceAnswer = StudyJson.Text(json, "correctAnswer", 2000);
                item.AiChecked = true;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return InvalidOutput();
            }
        }
        item.AnsweredAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToResult(item));
    }

    private static async Task<IResult> Review(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return NotFound();
        var answered = db.Set<LearningQuizItem>().AsNoTracking().Where(q => q.UserId == userId && q.LearningDocumentId == doc.LearningId && q.Status != QuizStatus.Pending);
        var counts = await answered.GroupBy(q => q.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var items = await answered.Where(q => q.Status == QuizStatus.Incorrect || q.Status == QuizStatus.Partial)
            .OrderByDescending(q => q.AnsweredAt).Take(30).ToListAsync(ct);
        int Count(QuizStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        return TypedResults.Ok(new QuizReviewDto(Count(QuizStatus.Correct), Count(QuizStatus.Partial), Count(QuizStatus.Incorrect), items.Select(ToResult).ToList()));
    }

    private static QuizResultDto ToResult(LearningQuizItem q) => new(q.Id, q.Question, q.Status.ToString(), q.Feedback ?? "", q.ReferenceAnswer,
        q.MissingPointsJson is null ? [] : JsonSerializer.Deserialize<string[]>(q.MissingPointsJson, Json) ?? [], q.PageFrom, q.PageTo, q.AiChecked);

    // ---------- Ortaq cavab ----------

    private static async Task<(OpenQuestionScan Scan, bool MultipleChoice)> ScanAsync(AppDbContext db, StudyDoc doc, CancellationToken ct)
    {
        var scan = OpenQuestionExtractor.Scan(await TeachingStep.ChunksAsync(db, doc.LearningId, ct));
        // A document that the exam parser read as a multiple-choice test is never an open-question document.
        var parsedAsTest = await db.Set<Questions.Question>().AnyAsync(q => q.DocumentId == doc.DocumentId && q.Options.Count > 1, ct);
        return (scan, scan.LooksMultipleChoice || parsedAsTest);
    }

    private static async Task<IResult> OpenQuestions(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await FromSession(db, user.GetUserId(), sessionId, ct) is not { } doc) return NotFound();
        var (scan, multipleChoice) = await ScanAsync(db, doc, ct);
        if (multipleChoice)
            return TypedResults.Ok(new OpenQuestionsDto(false, "Bu sənəd variantlı testə oxşayır. Ortaq cavab yalnız açıq suallı sənədlər üçündür.", []));
        if (scan.Questions.Count == 0)
            return TypedResults.Ok(new OpenQuestionsDto(false, "Sənəddə açıq sual tapılmadı. Suallar nömrələnmiş və ya “?” ilə bitən olmalıdır.", []));
        return TypedResults.Ok(new OpenQuestionsDto(true, scan.Questions.Count >= OpenQuestionExtractor.MaxQuestions ? $"İlk {OpenQuestionExtractor.MaxQuestions} sual göstərilir." : null,
            scan.Questions));
    }

    private static async Task<IResult> SharedAnswers(Guid sessionId, SharedAnswerRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai,
        IChunkRetriever retriever, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var admin = user.IsInRole(Roles.Admin);
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return NotFound();
        var (scan, multipleChoice) = await ScanAsync(db, doc, ct);
        if (multipleChoice) return Bad("Variantlı test sənədləri üçün ortaq cavab yaradılmır.");
        var byNumber = scan.Questions.ToDictionary(q => q.Number);
        List<int> selected = request.Auto
            ? scan.Questions.Take(MaxAutoQuestions).Select(q => q.Number).ToList()
            : (request.QuestionNumbers ?? []).Distinct().Where(byNumber.ContainsKey).ToList();
        if (request.Auto ? selected.Count < 2 : selected.Count < 2)
            return Bad(request.Auto ? "Ortaq cavab üçün sənəddə ən azı 2 açıq sual olmalıdır." : "Ən azı 2 sual seçin.");
        selected.Sort();

        // Shared answers come only from the document, so any reader of the same file version may reuse them.
        var key = StudyPrompts.CacheKey("shared", doc.LearningId, doc.Sha256, selected, request.Length, request.Auto, StudyPrompts.Version);
        if (await FromCache(db, userId, doc.LearningId, key, mine: false, clock, ct) is { } cached) return TypedResults.Ok(cached);

        // 1. Groups: automatic (cheap model) or the user's selection, which the model may still split.
        List<(string Title, List<int> Numbers)> groups;
        if (request.Auto)
        {
            var list = string.Join('\n', selected.Select(n => $"{n}. {TutorPrompt.Neutralise(byNumber[n].Text)}"));
            var grouping = await ai.RunAsync(userId, admin,
                [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(list))), new("user", $"<question>\n{list}\n</question>\n{StudyPrompts.GroupTask}")],
                "group_questions", StudyPrompts.GroupSchema(), Math.Min(3000, 200 + selected.Count * 20), light: true, ct);
            if (!grouping.Ok) return TypedResults.Problem(grouping.Error, statusCode: grouping.Status);
            try
            {
                groups = StudyJson.NormalizeGroups(grouping.Json!.Value.GetProperty("groups").EnumerateArray()
                    .Select(g => (StudyJson.Text(g, "title", 120), StudyJson.Numbers(g, "questionNumbers"))), selected, n => Shorten(byNumber[n].Text, 80));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return InvalidOutput(); }
        }
        else groups = [("", selected)];

        // 2. Shared answers in batches, each with the document parts that match its questions.
        var output = new List<object>();
        foreach (var batch in groups.Chunk(5))
        {
            var sources = new Dictionary<int, LearningDocumentChunk>();
            var groupChunks = new List<HashSet<int>>();
            foreach (var (_, numbers) in batch)
            {
                var ordinals = new HashSet<int>();
                foreach (var n in numbers)
                {
                    var hits = await retriever.SearchAsync(doc.LearningId, byNumber[n].Text, 2, ct);
                    foreach (var hit in hits.Chunks) ordinals.Add(hit.Ordinal);
                }
                groupChunks.Add(ordinals.Take(5).ToHashSet());
            }
            var wanted = groupChunks.SelectMany(o => o).Distinct().Take(14).ToList();
            foreach (var chunk in await db.Set<LearningDocumentChunk>().AsNoTracking()
                         .Where(c => c.LearningDocumentId == doc.LearningId && wanted.Contains(c.Ordinal)).OrderBy(c => c.Ordinal).ToListAsync(ct))
                sources[chunk.Ordinal] = chunk;

            var prompt = new StringBuilder();
            foreach (var (title, numbers) in batch)
            {
                prompt.Append("<group").Append(title.Length > 0 ? $" title=\"{TutorPrompt.Neutralise(title).Replace("\"", "'")}\"" : "").Append(">\n");
                foreach (var n in numbers) prompt.Append($"<question number=\"{n}\">{TutorPrompt.Neutralise(byNumber[n].Text)}</question>\n");
                prompt.Append("</group>\n");
            }
            prompt.Append(StudyPrompts.Sources(sources.Values)).Append(StudyPrompts.SharedTask(request.Length, mayRegroup: !request.Auto));
            var perGroup = request.Length == AnswerLength.Short ? 450 : 800;
            var result = await ai.RunAsync(userId, admin,
                [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(byNumber[batch[0].Numbers[0]].Text))), new("user", prompt.ToString())],
                "shared_answers", StudyPrompts.SharedSchema(), Math.Min(4000, perGroup * batch.Length + (request.Auto ? 0 : 400)), light: false, ct);
            if (!result.Ok) return TypedResults.Problem(result.Error, statusCode: result.Status);

            try
            {
                var batchNumbers = batch.SelectMany(g => g.Numbers).ToList();
                var parsed = result.Json!.Value.GetProperty("groups").EnumerateArray().Select(g => new
                {
                    Title = StudyJson.Text(g, "title", 120), Numbers = StudyJson.Numbers(g, "questionNumbers"),
                    Answer = StudyJson.Text(g, "answer", 4000), Keywords = StudyJson.Texts(g, "keywords", 8, 60),
                    Coverage = g.GetProperty("coverage").EnumerateArray().Select(c => (Number: c.GetProperty("questionNumber").GetInt32(),
                        Full: c.GetProperty("level").GetString() == "FULL", Extra: StudyJson.Texts(c, "extraPoints", 4, 240))).ToList(),
                }).Where(g => g.Numbers.Any(batchNumbers.Contains)).ToList();
                // Every question appears once; questions the model left out are reported as not covered rather than hidden.
                var used = new HashSet<int>();
                foreach (var g in parsed)
                {
                    var numbers = g.Numbers.Where(n => batchNumbers.Contains(n) && used.Add(n)).ToList();
                    if (numbers.Count == 0) continue;
                    var chunks = numbers.SelectMany(n => groupChunks[batch.ToList().FindIndex(b => b.Numbers.Contains(n))]).Distinct()
                        .Where(sources.ContainsKey).Select(o => sources[o]).ToList();
                    output.Add(new
                    {
                        g.Title, g.Answer, g.Keywords, SourcePages = StudyPrompts.Pages(chunks),
                        Questions = numbers.Select(n =>
                        {
                            var c = g.Coverage.FirstOrDefault(x => x.Number == n);
                            return new { Number = n, byNumber[n].Text, Coverage = c.Number == n && c.Full ? "FULL" : "PARTIAL", ExtraPoints = c.Number == n ? c.Extra : [] };
                        }).ToList(),
                    });
                }
                foreach (var n in batchNumbers.Where(n => !used.Contains(n)))
                    output.Add(new
                    {
                        Title = Shorten(byNumber[n].Text, 80), Answer = "", Keywords = Array.Empty<string>(), SourcePages = Array.Empty<int>(),
                        Questions = new[] { new { Number = n, byNumber[n].Text, Coverage = "PARTIAL", ExtraPoints = (IReadOnlyList<string>)["Bu sual üçün ortaq cavab yaradılmadı."] } },
                    });
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return InvalidOutput(); }
        }

        var material = new LearningMaterial
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = doc.LearningId, DocumentSha256 = doc.Sha256, Kind = MaterialKind.SharedAnswer,
            Length = request.Length, QuestionNumbersJson = JsonSerializer.Serialize(selected), CacheKey = key, PromptVersion = StudyPrompts.Version,
            PayloadJson = JsonSerializer.Serialize(new { groups = output, auto = request.Auto }, Json), CreatedAt = clock.GetUtcNow(),
        };
        db.Add(material);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(material, fromCache: false));
    }

    // ---------- Son dəqiqə hazırlığı ----------

    private static async Task<IResult> LastMinute(Guid sessionId, LastMinuteRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (await FromSession(db, userId, sessionId, ct) is not { } doc) return NotFound();
        var minutes = request.Minutes <= 15 ? 15 : request.Minutes <= 30 ? 30 : 60;
        var chunks = await TeachingStep.ChunksAsync(db, doc.LearningId, ct);
        if (chunks.Count == 0) return TypedResults.Problem("Sənəddə mətn tapılmadı.", statusCode: 409);

        var history = await db.Set<LearningQuizItem>().AsNoTracking()
            .Where(q => q.UserId == userId && q.LearningDocumentId == doc.LearningId && q.Status != QuizStatus.Pending)
            .Select(q => new { q.ChunkOrdinal, q.Status, q.AnsweredAt }).ToListAsync(ct);
        // The sheet is personal (it follows the student's mistakes), so the cache is per user and changes with new answers.
        var fingerprint = $"{history.Count}:{history.Max(h => h.AnsweredAt)?.ToUnixTimeSeconds()}";
        var key = StudyPrompts.CacheKey("last", userId, doc.LearningId, doc.Sha256, minutes, fingerprint, StudyPrompts.Version);
        if (await FromCache(db, userId, doc.LearningId, key, mine: true, clock, ct) is { } cached) return TypedResults.Ok(cached);

        var budget = minutes switch { 15 => 6, 30 => 10, _ => 16 };
        var weak = history.Where(h => h.Status is QuizStatus.Incorrect or QuizStatus.Partial).Select(h => h.ChunkOrdinal).Distinct().Take(budget / 2).ToHashSet();
        var picked = chunks.Where(c => weak.Contains(c.Ordinal)).ToList();
        var rest = chunks.Where(c => !weak.Contains(c.Ordinal)).ToList();
        var step = Math.Max(1.0, rest.Count / (double)Math.Max(1, budget - picked.Count));
        for (var i = 0.0; i < rest.Count && picked.Count < budget; i += step) picked.Add(rest[(int)i]);
        picked = picked.OrderBy(c => c.Ordinal).ToList();

        var sources = string.Concat(picked.Select(c =>
            $"<source pages=\"{StudyPrompts.PageText(c.PageFrom, c.PageTo)}\"{(weak.Contains(c.Ordinal) ? " weak=\"true\"" : "")}>\n{TutorPrompt.Neutralise(c.Text)}\n</source>\n"));
        var result = await ai.RunAsync(userId, user.IsInRole(Roles.Admin),
            [new("system", StudyPrompts.System(TutorPrompt.DetectLanguage(picked[0].Text))), new("user", sources + StudyPrompts.LastMinuteTask(minutes, weak.Count > 0))],
            "last_minute", StudyPrompts.LastMinuteSchema(), minutes switch { 15 => 900, 30 => 1600, _ => 2800 }, light: false, ct);
        if (!result.Ok) return TypedResults.Problem(result.Error, statusCode: result.Status);

        try
        {
            var json = result.Json!.Value;
            var payload = new
            {
                Sections = json.GetProperty("sections").EnumerateArray().Take(10)
                    .Select(s => new { Heading = StudyJson.Text(s, "heading", 120), Items = StudyJson.Texts(s, "items", 14, 260) })
                    .Where(s => s.Items.Count > 0).ToList(),
                KeyTerms = StudyJson.Texts(json, "keyTerms", 25, 60),
                ReviewQuestions = json.GetProperty("reviewQuestions").EnumerateArray().Take(15)
                    .Select(q => new { Question = StudyJson.Text(q, "question", 300), Answer = StudyJson.Text(q, "answer", 500) }).ToList(),
                SourcePages = StudyPrompts.Pages(picked),
                WeakParts = weak.Count,
            };
            if (payload.Sections.Count == 0) throw new JsonException("sections");
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
        var rows = await db.Set<LearningMaterial>().AsNoTracking().Where(m => m.UserId == userId && m.LearningDocumentId == doc.LearningId)
            .OrderByDescending(m => m.Saved).ThenByDescending(m => m.CreatedAt).Take(30).ToListAsync(ct);
        return TypedResults.Ok(rows.Select(m =>
        {
            using var payload = JsonDocument.Parse(m.PayloadJson);
            var root = payload.RootElement;
            var (title, count) = m.Kind == MaterialKind.LastMinute
                ? ($"Son dəqiqə · {m.Minutes} dəq", root.TryGetProperty("sections", out var s) ? s.GetArrayLength() : 0)
                : root.TryGetProperty("groups", out var g) && g.GetArrayLength() > 0
                    ? (g[0].GetProperty("title").GetString() ?? "Ortaq cavab", g.GetArrayLength())
                    : ("Ortaq cavab", 0);
            return new MaterialSummaryDto(m.Id, m.Kind.ToString(), m.Length?.ToString(), m.Minutes, m.Saved, title, count, m.CreatedAt);
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

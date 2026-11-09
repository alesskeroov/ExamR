using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Learning;

public sealed class CreateChatRequest
{
    [Required] public Guid? DocumentId { get; init; }
}

public sealed class SendMessageRequest
{
    [MaxLength(4000)] public string Content { get; init; } = "";
    public TutorMode Mode { get; init; } = TutorMode.Free;
    /// <summary>Teacher-mode button; Ask is a typed message (an answer when a check question is pending).</summary>
    public TutorAction Action { get; init; } = TutorAction.Ask;
    [MaxLength(32)] public string? TopicKey { get; init; }
    public TutorDifficulty Difficulty { get; init; } = TutorDifficulty.Medium;
}

public sealed record TopicDto(string Key, string Title, int? PageFrom, int? PageTo, int Steps, string Status, int CurrentStep, int Attempts, int Correct, int Partial,
    bool Weak, bool AwaitingAnswer);

public sealed record ChatSessionDto(Guid Id, Guid DocumentId, string DocumentName, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int MessageCount);
public sealed record ChatSourceDto(int Number, int ChunkOrdinal, int? PageFrom, int? PageTo, string? Section, string Excerpt, bool Cited);
public sealed record ChatMessageDto(Guid Id, string Role, string Content, string Status, bool? Grounded, DateTimeOffset CreatedAt, IReadOnlyList<ChatSourceDto> Sources);
public sealed record ChatSessionDetailDto(ChatSessionDto Session, IReadOnlyList<ChatMessageDto> Messages);

/// <summary>Document-grounded tutor chat for every signed-in user, behind LearningAi:Enabled and the shared budget.</summary>
public static class LearningChatEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapLearningChatEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/learning").RequireAuthorization();
        group.MapGet("/ai/usage", Usage);
        group.MapPost("/chats", Create).Validate<CreateChatRequest>();
        group.MapGet("/chats", List);
        group.MapGet("/chats/{sessionId:guid}", Get);
        group.MapDelete("/chats/{sessionId:guid}", Delete);
        group.MapGet("/chats/{sessionId:guid}/topics", Topics);
        group.MapPost("/chats/{sessionId:guid}/messages", Send).Validate<SendMessageRequest>()
            .RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
    }

    // Budget, cost and token figures are for admins; other users only learn whether the tutor is on.
    private static async Task<IResult> Usage(ClaimsPrincipal user, LearningAiBudget budget, IOptions<LearningAiOptions> options, CancellationToken ct) =>
        user.IsInRole(Roles.Admin) ? TypedResults.Ok(await budget.StatusAsync(user.GetUserId(), ct)) : (IResult)TypedResults.Ok(new { enabled = options.Value.Enabled });

    private static async Task<IResult> Create(CreateChatRequest request, ClaimsPrincipal user, AppDbContext db,
        CardDeckService decks, TimeProvider clock, CancellationToken ct)
    {
        // No AI check: a session is also the home of the AI-free "Kartlar"; AI tools check LearningAi:Enabled themselves.
        var userId = user.GetUserId();
        if (!await DocumentAccess.Readable(db, userId).AnyAsync(d => d.Id == request.DocumentId, ct)) return NotFound("Sənəd tapılmadı.");
        var learning = await db.Set<LearningDocument>().AsNoTracking().Where(l => l.DocumentId == request.DocumentId)
            .Select(l => new { l.Id, l.Status, l.Document.OriginalFileName }).FirstOrDefaultAsync(ct);
        if (learning is not { Status: LearningStatus.Completed })
            return TypedResults.Problem("Sənəd hələ söhbət üçün hazır deyil. Əvvəlcə emal olunmalıdır.", statusCode: StatusCodes.Status409Conflict);
        // Any document that gives cards: questions and answers, tests with marked answers, or lecture notes.
        if (await decks.GetAsync(learning.Id, ct) is { Deck: null } rejected)
            return TypedResults.Problem(rejected.Error, statusCode: StatusCodes.Status422UnprocessableEntity);

        var now = clock.GetUtcNow();
        var session = new LearningChatSession { Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = learning.Id, Title = Shorten(learning.OriginalFileName, 120), CreatedAt = now, UpdatedAt = now };
        db.Add(session);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/learning/chats/{session.Id}", await Sessions(db, userId, session.Id).FirstAsync(ct));
    }

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, CancellationToken ct, Guid? documentId = null, int? page = null, int? pageSize = null)
    {
        var (number, size) = Paging.Normalize(page, pageSize);
        var query = Sessions(db, user.GetUserId(), documentId: documentId);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((number - 1) * size).Take(size).ToListAsync(ct);
        return TypedResults.Ok(new PagedResult<ChatSessionDto>(items, number, size, total));
    }

    private static async Task<IResult> Get(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var session = await Sessions(db, user.GetUserId(), sessionId).FirstOrDefaultAsync(ct);
        if (session is null) return NotFound("Söhbət tapılmadı.");
        var messages = await db.Set<LearningChatMessage>().AsNoTracking().Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto(m.Id, m.Role.ToString(), m.Content, m.Status.ToString(), m.Grounded, m.CreatedAt,
                m.Sources.OrderBy(s => s.Number).Select(s => new ChatSourceDto(s.Number, s.ChunkOrdinal, s.PageFrom, s.PageTo, s.Section, s.Excerpt, s.Cited)).ToList()))
            .ToListAsync(ct);
        return TypedResults.Ok(new ChatSessionDetailDto(session, messages));
    }

    private static async Task<IResult> Delete(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var deleted = await db.Set<LearningChatSession>().Where(s => s.Id == sessionId && s.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound("Söhbət tapılmadı.") : TypedResults.NoContent();
    }

    private static async Task<IResult> Send(Guid sessionId, SendMessageRequest request, ClaimsPrincipal user, HttpContext http, AppDbContext db,
        IChunkRetriever retriever, IChatModel model, LearningAiBudget budget, IOptions<LearningAiOptions> options, IConfiguration configuration,
        OpenQuestionIndexer indexer, TimeProvider clock, ILogger<LearningChatSession> logger, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled || OpenAiChatClient.ApiKey(configuration) is null) return Disabled();
        var question = request.Content.Trim();
        var typed = request.Action is TutorAction.Ask or TutorAction.Answer;
        if ((typed && question.Length == 0) || question.Length > o.MaxQuestionCharacters)
            return TypedResults.Problem($"Sual 1–{o.MaxQuestionCharacters} simvol olmalıdır.", statusCode: StatusCodes.Status400BadRequest);

        var userId = user.GetUserId();
        var session = await db.Set<LearningChatSession>().FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is null) return NotFound("Söhbət tapılmadı.");
        // Access to the document can be revoked after the session was created (unshared, deleted).
        var documentId = await db.Set<LearningDocument>().Where(l => l.Id == session.LearningDocumentId && l.Status == LearningStatus.Completed)
            .Select(l => (Guid?)l.DocumentId).FirstOrDefaultAsync(ct);
        if (documentId is null || !await DocumentAccess.Readable(db, userId).AnyAsync(d => d.Id == documentId, ct)) return NotFound("Sənəd tapılmadı.");
        // Sessions created before the open-question rule (DOCX/TXT, tests) stay readable but get no new answers.
        if (await indexer.GetAsync(session.LearningDocumentId, ct) is not { } index) return NotFound("Sənəd tapılmadı.");
        if (!index.Supported) return TypedResults.Problem(index.Rejection, statusCode: StatusCodes.Status422UnprocessableEntity);

        // Context selection: a few recent completed turns, never the whole history.
        var recent = await db.Set<LearningChatMessage>().AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Status == LearningMessageStatus.Completed)
            .OrderByDescending(m => m.CreatedAt).Take(o.HistoryMessages).Select(m => new HistoryTurn(m.Role, m.Content)).ToListAsync(ct);
        var history = TutorPrompt.Trim(recent, o.HistoryMessages, o.HistoryMaxCharacters);

        // Teacher mode: a button action, or a typed reply while a check question is pending, works on one topic part.
        var teaching = await TeachingStep.ResolveAsync(db, userId, session.LearningDocumentId, request, clock.GetUtcNow(), ct);
        if (teaching is { Error: { } teachingError }) return TypedResults.Problem(teachingError, statusCode: StatusCodes.Status400BadRequest);
        if (teaching is not null && question.Length == 0) question = TeachingPrompt.StudentLine(teaching.Action, teaching.Topic!, request.Difficulty);

        RetrievalResult retrieval;
        if (teaching is not null) retrieval = new RetrievalResult(true, teaching.Sources, "teacher");
        else
        {
            retrieval = await retriever.SearchAsync(session.LearningDocumentId, question, o.ContextChunks, ct);
            // Follow-ups ("explain more") rarely repeat the topic words; fall back to the previous question's sources.
            if (!retrieval.Found && recent.FirstOrDefault(t => t.Role == LearningMessageRole.User) is { } previous)
                retrieval = await retriever.SearchAsync(session.LearningDocumentId, previous.Content, o.ContextChunks, ct);
            // Overview questions ("main topics", "key definitions") name no document terms: give an even sample of the document.
            if (!retrieval.Found && TutorPrompt.IsOverviewQuestion(question))
            {
                var all = await TeachingStep.ChunksAsync(db, session.LearningDocumentId, ct);
                var step = Math.Max(1.0, all.Count / (double)o.ContextChunks);
                var sample = Enumerable.Range(0, Math.Min(o.ContextChunks, all.Count)).Select(i => all[(int)(i * step)]).ToList();
                if (sample.Count > 0)
                    retrieval = new RetrievalResult(true, sample.Select(c => new RetrievedChunk(c.Ordinal, 0, 0, c.PageFrom, c.PageTo, c.Section,
                        c.ParagraphFrom, c.ParagraphTo, c.Text)).ToList(), "overview");
            }
            // A message about one of the document's questions gets that question's own answer as the first source, so the
            // reply is built from the matching answer instead of whatever chunk shares the most words.
            if (index.Match(question) is { } asked)
            {
                var own = new RetrievedChunk(asked.ChunkOrdinal, 1, 1, asked.Page, asked.Page, null, 0, 0, $"Sual {asked.Number}: {asked.Text}\nCavab: {asked.Answer}");
                var rest = retrieval.Found ? retrieval.Chunks.Where(c => c.Ordinal != asked.ChunkOrdinal) : [];
                retrieval = new RetrievalResult(true, [own, .. rest.Take(Math.Max(0, o.ContextChunks - 1))], "question");
            }
        }
        // Button lines are Azerbaijani; the student's own words (or their last typed turn) decide the reply language.
        var language = TutorPrompt.DetectLanguage(teaching is null || typed ? question
            : recent.FirstOrDefault(t => t.Role == LearningMessageRole.User)?.Content ?? question);

        var now = clock.GetUtcNow();
        var userMessage = new LearningChatMessage { Id = Guid.NewGuid(), SessionId = sessionId, Role = LearningMessageRole.User, Content = question, Status = LearningMessageStatus.Completed, CreatedAt = now };
        var answer = new LearningChatMessage { Id = Guid.NewGuid(), SessionId = sessionId, Role = LearningMessageRole.Assistant, Status = LearningMessageStatus.Completed, Grounded = retrieval.Found };
        if (!await db.Set<LearningChatMessage>().AnyAsync(m => m.SessionId == sessionId, ct))
            session.Title = Shorten(question, 120);

        if (!retrieval.Found)
        {
            // Nothing to ground an answer on: say so without calling (or paying for) the model.
            answer.Content = TutorPrompt.NoSourceReply(language);
            answer.CreatedAt = clock.GetUtcNow();
            session.UpdatedAt = answer.CreatedAt;
            db.AddRange(userMessage, answer);
            await db.SaveChangesAsync(ct);
            await StartStream(http);
            await Event(http, "meta", new { userMessageId = userMessage.Id, assistantMessageId = answer.Id, grounded = false, sources = Array.Empty<ChatSourceDto>() });
            await Event(http, "delta", new { text = answer.Content });
            await Event(http, "done", new { status = answer.Status.ToString(), grounded = false });
            return Results.Empty;
        }

        var prompt = TutorPrompt.Build(question, retrieval.Chunks, history, language, request.Mode,
            teaching is null ? null : TeachingPrompt.Instruction(teaching.Action, teaching.Topic!, teaching.Step, request.Difficulty));
        var inputEstimate = (int)Math.Ceiling(prompt.Sum(m => m.Content.Length) / 3.0) + 20;
        var (decision, reservation) = await budget.ReserveAsync(userId, sessionId, AiCost.Reserve(inputEstimate, o), ct);
        switch (decision)
        {
            case BudgetDecision.Busy: return TypedResults.Problem("Əvvəlki cavab hələ hazırlanır. Bir az gözləyin.", statusCode: StatusCodes.Status429TooManyRequests);
            case BudgetDecision.MonthlyLimit: return (await budget.MonthlyAsync(user.GetUserId(), ct)).Problem();
            case BudgetDecision.DailyLimit: return TypedResults.Problem("Bugünkü AI sorğu limitinə çatdınız. Sabah yenidən cəhd edin.", statusCode: StatusCodes.Status429TooManyRequests);
            case BudgetDecision.BudgetExhausted:
                return TypedResults.Problem(user.IsInRole(Roles.Admin) ? "AI büdcəsi bitib. Yeni AI sorğuları dayandırılıb." : "AI köməkçisi müvəqqəti əlçatan deyil. Daha sonra yenidən cəhd edin.",
                    statusCode: StatusCodes.Status403Forbidden);
        }

        var sources = retrieval.Chunks.Select((c, i) => new LearningMessageSource
        {
            Id = Guid.NewGuid(), MessageId = answer.Id, Number = i + 1, ChunkOrdinal = c.Ordinal, PageFrom = c.PageFrom, PageTo = c.PageTo,
            Section = c.Section is { Length: > 200 } s ? s[..200] : c.Section, Excerpt = Shorten(c.Text, 400), Score = c.Score,
        }).ToList();
        session.UpdatedAt = now;
        db.Add(userMessage);
        await db.SaveChangesAsync(ct);

        await StartStream(http);
        await Event(http, "meta", new
        {
            userMessageId = userMessage.Id, assistantMessageId = answer.Id, grounded = true,
            sources = sources.Select(s => new ChatSourceDto(s.Number, s.ChunkOrdinal, s.PageFrom, s.PageTo, s.Section, s.Excerpt, false)),
        });

        var text = new StringBuilder();
        var streamed = 0;
        ChatModelUsage? usage = null;
        string? error = null;
        try
        {
            await foreach (var chunk in model.StreamAsync(prompt, o.MaxOutputTokens, ct))
            {
                if (chunk.Usage is not null) { usage = chunk.Usage; continue; }
                text.Append(chunk.Delta);
                // An evaluation ends with a verdict tag that the student must not see: hold back anything from "[[".
                var safe = teaching?.Action == TutorAction.Answer ? TeachingPrompt.SafeLength(text.ToString()) : text.Length;
                if (safe > streamed)
                {
                    await Event(http, "delta", new { text = text.ToString(streamed, safe - streamed) });
                    streamed = safe;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            answer.Status = LearningMessageStatus.Cancelled; // client closed the stream
        }
        catch (ChatModelException ex)
        {
            logger.LogWarning("Tutor answer failed: {Failure}", ex.Failure);
            answer.Status = LearningMessageStatus.Failed;
            error = ex.Failure switch
            {
                ChatModelFailure.RateLimited => "AI xidməti hazırda məşğuldur. Bir az sonra yenidən cəhd edin.",
                ChatModelFailure.Timeout => "Cavab vaxtında hazırlanmadı. Yenidən cəhd edin.",
                _ => "AI cavabı hazırlana bilmədi. Bir az sonra yenidən cəhd edin.",
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tutor answer crashed for session {SessionId}", sessionId);
            answer.Status = LearningMessageStatus.Failed;
            error = "AI cavabı hazırlana bilmədi. Bir az sonra yenidən cəhd edin.";
        }

        answer.Content = text.Length > 0 ? text.ToString() : error ?? "";
        AnswerResult? verdict = null;
        if (teaching is not null)
        {
            if (teaching.Action == TutorAction.Answer && text.Length > 0)
            {
                (answer.Content, verdict) = TeachingPrompt.ExtractResult(answer.Content);
                // Text held back as a possible tag that turned out to be ordinary text.
                if (answer.Content.Length > streamed) await Event(http, "delta", new { text = answer.Content[streamed..] });
            }
            // Progress only moves for answers the student actually received.
            if (answer.Status == LearningMessageStatus.Completed) teaching.Apply(db, verdict, clock.GetUtcNow());
        }
        answer.CreatedAt = clock.GetUtcNow();
        var cited = TutorPrompt.CitedNumbers(answer.Content, sources.Count);
        sources.ForEach(s => s.Cited = cited.Contains(s.Number));
        answer.Sources = sources;
        db.Add(answer);
        await db.SaveChangesAsync(CancellationToken.None);
        var estimate = new ChatModelUsage(inputEstimate, 0, (int)Math.Ceiling(text.Length / 3.0));
        await budget.SettleAsync(reservation!.Id, answer.Id, usage, estimate, failedBeforeOutput: text.Length == 0 && answer.Status == LearningMessageStatus.Failed);

        if (!ct.IsCancellationRequested)
        {
            if (error is not null) await Event(http, "error", new { detail = error });
            await Event(http, "done", new
            {
                status = answer.Status.ToString(), grounded = true, cited = cited.Order(), result = verdict?.ToString(),
                topic = teaching is { Progress: { } p, Topic: { } t } && answer.Status == LearningMessageStatus.Completed ? ToDto(t, p) : null,
            });
        }
        return Results.Empty;
    }

    private static async Task<IResult> Topics(Guid sessionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var learningId = await db.Set<LearningChatSession>().Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => (Guid?)s.LearningDocumentId).FirstOrDefaultAsync(ct);
        if (learningId is null) return NotFound("Söhbət tapılmadı.");
        var topics = TopicPlanner.Plan(await TeachingStep.ChunksAsync(db, learningId.Value, ct));
        var progress = await db.Set<LearningTopicProgress>().AsNoTracking().Where(p => p.UserId == userId && p.LearningDocumentId == learningId)
            .ToDictionaryAsync(p => p.TopicKey, ct);
        return TypedResults.Ok(topics.Select(t => ToDto(t, progress.GetValueOrDefault(t.Key))));
    }

    private static TopicDto ToDto(TutorTopic topic, LearningTopicProgress? p) =>
        new(topic.Key, topic.Title, topic.PageFrom, topic.PageTo, topic.ChunkOrdinals.Count, (p?.Status ?? TopicStatus.NotStarted).ToString(),
            p?.CurrentStep ?? 0, p?.Attempts ?? 0, p?.Correct ?? 0, p?.Partial ?? 0, p is not null && TeachingPrompt.IsWeak(p), p?.AwaitingAnswer ?? false);

    private static IQueryable<ChatSessionDto> Sessions(AppDbContext db, Guid userId, Guid? sessionId = null, Guid? documentId = null) =>
        from s in db.Set<LearningChatSession>().AsNoTracking()
        where s.UserId == userId && (sessionId == null || s.Id == sessionId)
            && (documentId == null || db.Set<LearningDocument>().Any(d => d.Id == s.LearningDocumentId && d.DocumentId == documentId))
        from l in db.Set<LearningDocument>().Where(l => l.Id == s.LearningDocumentId)
        orderby s.UpdatedAt descending
        select new ChatSessionDto(s.Id, l.DocumentId, l.Document.OriginalFileName, s.Title, s.CreatedAt, s.UpdatedAt, s.Messages.Count);

    private static async Task StartStream(HttpContext http)
    {
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-transform";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        await http.Response.Body.FlushAsync(http.RequestAborted);
    }

    private static async Task Event(HttpContext http, string name, object data)
    {
        if (http.RequestAborted.IsCancellationRequested) return;
        try
        {
            await http.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", http.RequestAborted);
            await http.Response.Body.FlushAsync(http.RequestAborted);
        }
        catch (OperationCanceledException) { /* client went away; the answer is still saved */ }
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
    private static IResult Disabled() => TypedResults.Problem("AI köməkçisi hazırda aktiv deyil.", statusCode: StatusCodes.Status503ServiceUnavailable);
    private static IResult NotFound(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status404NotFound);
}

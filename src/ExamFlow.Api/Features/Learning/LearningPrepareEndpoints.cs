using System.Security.Claims;
using System.Text.Json;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Learning;

public sealed class PrepareRequest { public bool Regenerate { get; init; } }

public sealed record PrepareQuestionDto(int Number, string Text, int? Page);
/// <summary>Section: the broad main topic the subtopic belongs to (empty when unknown).</summary>
public sealed record PrepareTopicDto(string Key, string Title, IReadOnlyList<PrepareQuestionDto> Questions, bool Explained, bool Answered, string Section = "");
/// <summary>Found = every numbered question; Usable = with the document's answer. Excluded ones are listed with a plain reason.</summary>
public sealed record PrepareStatsDto(int Found, int Usable, int SharedAnswers, int NoAnswer, int MultipleChoice, int Unclear);
public sealed record ExcludedQuestionDto(int Number, string Text, int? Page, string Reason);
public sealed record PrepareTopicsDto(string? Warning, IReadOnlyList<PrepareTopicDto> Topics, PrepareStatsDto? Stats = null, IReadOnlyList<ExcludedQuestionDto>? Excluded = null);
public sealed record PrepareMaterialDto(string Key, bool FromCache, DateTimeOffset CreatedAt, JsonElement Payload);

/// <summary>
/// "Hazırlaş": topics of the document's open questions (the cached question groups, split to at most
/// <see cref="PrepareGenerator.MaxGroupQuestions"/>), with a topic explanation and a shared exam answer per topic.
/// Results depend only on the document version, so they are stored once (UserId = Guid.Empty) and shared by every reader.
/// </summary>
public static partial class LearningStudyEndpoints
{
    private static void MapPrepare(RouteGroupBuilder group)
    {
        group.MapPost("/chats/{sessionId:guid}/prepare/topics", PrepareTopics).RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapPost("/chats/{sessionId:guid}/prepare/topics/{key}/explanation", ExplainTopic).Validate<PrepareRequest>()
            .RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapPost("/chats/{sessionId:guid}/prepare/topics/{key}/answer", AnswerTopic).Validate<PrepareRequest>()
            .RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
        group.MapPost("/chats/{sessionId:guid}/revision", Revision).Validate<RevisionRequest>()
            .RequireRateLimiting(AuthenticationSetup.LearningAiRateLimit);
    }

    private sealed record PrepareTopic(string Key, string Title, IReadOnlyList<IndexedQuestion> Questions, string Section = "");
    private sealed record PrepareContext(StudyDoc Doc, string Fingerprint, IReadOnlyList<PrepareTopic> Topics, string? Warning, OpenQuestionIndex Index);

    private static async Task<(PrepareContext? Context, IResult? Error)> PrepareAsync(AppDbContext db, StudyAi ai, OpenQuestionIndexer indexer,
        ClaimsPrincipal user, Guid sessionId, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (doc, index, error) = await OpenDocAsync(db, indexer, userId, sessionId, ct);
        if (error is not null) return (null, error);
        var fingerprint = index!.Fingerprint();
        var (groups, groupError) = await GroupsAsync(db, ai, userId, user.IsInRole(Roles.Admin), doc!, index.ToScan(), fingerprint, clock, ct);
        if (groupError is not null) return (null, groupError);

        var byNumber = index.Questions.ToDictionary(q => q.Number);
        var topics = new List<PrepareTopic>();
        foreach (var group in groups!)
        {
            var parts = group.Numbers.Where(byNumber.ContainsKey).Chunk(PrepareGenerator.MaxGroupQuestions).ToList();
            for (var i = 0; i < parts.Count; i++)
                topics.Add(new(TopicKey(parts[i]), parts.Count > 1 ? $"{group.Title} ({i + 1}/{parts.Count})" : group.Title,
                    parts[i].Select(n => byNumber[n]).ToList(), group.Section ?? ""));
        }
        return (new(doc!, fingerprint, topics, index.Warning, index), null);
    }

    private static string TopicKey(IEnumerable<int> numbers) => StudyPrompts.CacheKey(numbers.ToList())[..16].ToLowerInvariant();

    private static string PrepareKey(MaterialKind kind, PrepareContext context, PrepareTopic topic) =>
        StudyPrompts.CacheKey(kind, PrepareGenerator.Version, context.Doc.LearningId, context.Doc.Sha256, context.Fingerprint, topic.Questions.Select(q => q.Number).ToList());

    private static async Task<IResult> PrepareTopics(Guid sessionId, ClaimsPrincipal user, AppDbContext db, StudyAi ai, OpenQuestionIndexer indexer,
        TimeProvider clock, CancellationToken ct)
    {
        var (context, error) = await PrepareAsync(db, ai, indexer, user, sessionId, clock, ct);
        if (error is not null) return error;
        var keys = context!.Topics.SelectMany(t => new[] { PrepareKey(MaterialKind.TopicExplanation, context, t), PrepareKey(MaterialKind.GroupAnswer, context, t) }).ToList();
        var ready = (await db.Set<LearningMaterial>().AsNoTracking()
            .Where(m => m.LearningDocumentId == context.Doc.LearningId && m.UserId == Guid.Empty && keys.Contains(m.CacheKey))
            .Select(m => m.CacheKey).ToListAsync(ct)).ToHashSet();
        var index = context.Index;
        var excluded = index.Excluded ?? [];
        int Count(ExclusionReason reason) => excluded.Count(e => e.Reason == reason);
        var stats = new PrepareStatsDto(index.Questions.Count + excluded.Count, index.Questions.Count, index.Questions.Count(q => q.SharedAnswer),
            Count(ExclusionReason.NoAnswer), Count(ExclusionReason.MultipleChoice), Count(ExclusionReason.Unclear));
        return TypedResults.Ok(new PrepareTopicsDto(context.Warning, context.Topics.Select(t => new PrepareTopicDto(t.Key, t.Title,
            t.Questions.Select(q => new PrepareQuestionDto(q.Number, q.Text, q.Page)).ToList(),
            ready.Contains(PrepareKey(MaterialKind.TopicExplanation, context, t)), ready.Contains(PrepareKey(MaterialKind.GroupAnswer, context, t)), t.Section)).ToList(),
            stats, excluded.Select(e => new ExcludedQuestionDto(e.Number, e.Text, e.Page, e.Reason switch
            {
                ExclusionReason.NoAnswer => "Sənəddə bu sualın cavabı tapılmadı",
                ExclusionReason.MultipleChoice => "Variantlı test sualıdır",
                _ => "Sualın növü dəqiq müəyyən edilmədi",
            })).ToList()));
    }

    private static Task<IResult> ExplainTopic(Guid sessionId, string key, PrepareRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai,
        OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct) =>
        TopicMaterial(sessionId, key, request, MaterialKind.TopicExplanation, user, db, ai, indexer, clock, ct,
            (topic, call, token) => PrepareGenerator.ExplainAsync(topic.Title, topic.Questions, call, token));

    private static Task<IResult> AnswerTopic(Guid sessionId, string key, PrepareRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai,
        OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct) =>
        TopicMaterial(sessionId, key, request, MaterialKind.GroupAnswer, user, db, ai, indexer, clock, ct,
            (topic, call, token) => PrepareGenerator.AnswerAsync(topic.Title, topic.Questions, call, token));

    /// <summary>Returns the stored result for this document version and topic, or generates, validates and stores it once.</summary>
    private static async Task<IResult> TopicMaterial<T>(Guid sessionId, string key, PrepareRequest request, MaterialKind kind, ClaimsPrincipal user,
        AppDbContext db, StudyAi ai, OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct,
        Func<PrepareTopic, PrepareGenerator.ModelCall, CancellationToken, Task<PrepareResult<T>>> generate) where T : class
    {
        var (context, error) = await PrepareAsync(db, ai, indexer, user, sessionId, clock, ct);
        if (error is not null) return error;
        if (context!.Topics.FirstOrDefault(t => t.Key == key) is not { } topic)
            return TypedResults.Problem("Mövzu tapılmadı. Mövzu siyahısını yeniləyin.", statusCode: StatusCodes.Status404NotFound);

        var cacheKey = PrepareKey(kind, context, topic);
        if (!request.Regenerate)
        {
            var cached = await db.Set<LearningMaterial>().AsNoTracking()
                .Where(m => m.LearningDocumentId == context.Doc.LearningId && m.UserId == Guid.Empty && m.CacheKey == cacheKey)
                .OrderByDescending(m => m.CreatedAt).Select(m => new { m.PayloadJson, m.CreatedAt }).FirstOrDefaultAsync(ct);
            if (cached is not null) return TypedResults.Ok(Dto(key, true, cached.CreatedAt, cached.PayloadJson));
        }

        var userId = user.GetUserId();
        var admin = user.IsInRole(Roles.Admin);
        var result = await generate(topic, (messages, schemaName, schema, tokens, token) =>
            ai.RunAsync(userId, admin, messages, schemaName, schema, tokens, light: false, token), ct);
        if (result.Payload is null) return TypedResults.Problem(result.Error, statusCode: result.Status);

        var material = new LearningMaterial
        {
            Id = Guid.NewGuid(), UserId = Guid.Empty, LearningDocumentId = context.Doc.LearningId, DocumentSha256 = context.Doc.Sha256, Kind = kind,
            QuestionNumbersJson = JsonSerializer.Serialize(topic.Questions.Select(q => q.Number)), CacheKey = cacheKey, PromptVersion = PrepareGenerator.Version,
            PayloadJson = JsonSerializer.Serialize(result.Payload, Json), CreatedAt = clock.GetUtcNow(),
        };
        db.Add(material);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Dto(key, false, material.CreatedAt, material.PayloadJson));
    }

    private static PrepareMaterialDto Dto(string key, bool fromCache, DateTimeOffset createdAt, string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);
        return new PrepareMaterialDto(key, fromCache, createdAt, payload.RootElement.Clone());
    }
}

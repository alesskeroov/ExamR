using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Learning;

/// <summary>Keys: topics whose cards should be prepared now ("Daha çox"); empty = the first topics in revision order.</summary>
public sealed class RevisionRequest
{
    [MaxLength(RevisionGenerator.BatchTopics)] public string[]? Keys { get; init; }
}

public sealed record RevisionTopicDto(string Key, string Title, int Questions, int Wrong, int Answered, IReadOnlyList<int> Numbers,
    IReadOnlyList<int> WrongNumbers, RevisionCard? Card);

/// <summary>HasHistory: the order follows the student's Məni yoxla results; otherwise it is the neutral analysed order. Error: why requested cards are missing.</summary>
public sealed record RevisionDto(bool HasHistory, string? Warning, IReadOnlyList<RevisionTopicDto> Topics, string? Error);

/// <summary>
/// "Son təkrar": the Hazırlaş topics in personal order (weak topics from Məni yoxla first) with a short revision card each.
/// Cards are stored per topic and document version (UserId = Guid.Empty) and only missing ones are generated, a batch per call.
/// </summary>
public static partial class LearningStudyEndpoints
{
    private static async Task<IResult> Revision(Guid sessionId, RevisionRequest request, ClaimsPrincipal user, AppDbContext db, StudyAi ai,
        OpenQuestionIndexer indexer, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var (context, error) = await PrepareAsync(db, ai, indexer, user, sessionId, clock, ct);
        if (error is not null) return error;

        var attempts = await db.Set<LearningQuizItem>().AsNoTracking()
            .Where(q => q.UserId == userId && q.LearningDocumentId == context!.Doc.LearningId && q.Status != QuizStatus.Pending)
            .OrderByDescending(q => q.AnsweredAt).Take(2000)
            .Select(q => new QuizAttempt(q.Question, q.Status, q.AnsweredAt ?? q.CreatedAt)).ToListAsync(ct);
        var latest = QuizLogic.Latest(attempts);
        var ordered = RevisionGenerator.Order(context!.Topics.Select(t => (t.Key, t.Title, t.Questions)), latest);

        string CardKey(RevisionTopic t) => StudyPrompts.CacheKey(MaterialKind.RevisionCard, RevisionGenerator.Version, context.Doc.LearningId, context.Doc.Sha256,
            context.Fingerprint, t.Questions.Select(q => q.Number).ToList());
        var keys = ordered.ToDictionary(t => t.Key, CardKey);
        var cacheKeys = keys.Values.ToList();
        var rows = await db.Set<LearningMaterial>().AsNoTracking()
            .Where(m => m.LearningDocumentId == context.Doc.LearningId && m.UserId == Guid.Empty && cacheKeys.Contains(m.CacheKey))
            .Select(m => new { m.CacheKey, m.PayloadJson }).ToListAsync(ct);
        var byCacheKey = rows.GroupBy(r => r.CacheKey).ToDictionary(g => g.Key, g => JsonSerializer.Deserialize<RevisionCard>(g.First().PayloadJson, Json));
        var cards = ordered.Where(t => byCacheKey.GetValueOrDefault(keys[t.Key]) is not null).ToDictionary(t => t.Key, t => byCacheKey[keys[t.Key]]!);

        // Only cards that are missing are generated: the requested ones, or the first topics of the revision order.
        var wanted = (request.Keys is { Length: > 0 } requested ? ordered.Where(t => requested.Contains(t.Key)) : ordered.Take(RevisionGenerator.BatchTopics))
            .Where(t => !cards.ContainsKey(t.Key)).Take(RevisionGenerator.BatchTopics).ToList();
        string? generationError = null;
        if (wanted.Count > 0)
        {
            var admin = user.IsInRole(Roles.Admin);
            var (generated, status, message) = await RevisionGenerator.GenerateAsync(wanted,
                (messages, schemaName, schema, tokens, token) => ai.RunAsync(userId, admin, messages, schemaName, schema, tokens, light: false, token), ct);
            if (generated is null)
            {
                // Nothing stored yet and nothing to show: report the provider/validation problem as it is.
                if (cards.Count == 0) return TypedResults.Problem(message, statusCode: status);
                generationError = message;
            }
            else
            {
                foreach (var topic in wanted.Where(t => generated.ContainsKey(t.Key)))
                {
                    cards[topic.Key] = generated[topic.Key];
                    db.Add(new LearningMaterial
                    {
                        Id = Guid.NewGuid(), UserId = Guid.Empty, LearningDocumentId = context.Doc.LearningId, DocumentSha256 = context.Doc.Sha256,
                        Kind = MaterialKind.RevisionCard, QuestionNumbersJson = JsonSerializer.Serialize(topic.Questions.Select(q => q.Number)),
                        CacheKey = keys[topic.Key], PromptVersion = RevisionGenerator.Version, PayloadJson = JsonSerializer.Serialize(generated[topic.Key], Json),
                        CreatedAt = clock.GetUtcNow(),
                    });
                }
                await db.SaveChangesAsync(ct);
                if (wanted.Any(t => !generated.ContainsKey(t.Key))) generationError = "Bəzi mövzular üçün sənəddə qısa təkrar kartı qurmağa kifayət qədər məlumat tapılmadı.";
            }
        }

        return TypedResults.Ok(new RevisionDto(latest.Count > 0, context.Warning, ordered.Select(t => new RevisionTopicDto(t.Key, t.Title, t.Questions.Count,
            t.Wrong, t.Answered, t.Questions.Select(q => q.Number).ToList(), t.WrongNumbers, cards.GetValueOrDefault(t.Key))).ToList(), generationError));
    }
}

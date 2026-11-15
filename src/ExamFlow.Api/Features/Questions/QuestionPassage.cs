using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Questions;

/// <summary>
/// A text that several questions of a document refer to (a reading passage, a listening transcript). It covers the
/// questions whose <see cref="Question.SourceNumber"/> is between <see cref="FromNumber"/> and <see cref="ToNumber"/>.
/// </summary>
public class QuestionPassage : IAuditable
{
    public const int MaxTitleLength = 300;
    public const int MaxTextLength = 20000;

    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public UploadedDocument Document { get; set; } = null!;
    public int FromNumber { get; set; }
    public int ToNumber { get; set; }
    public string? Title { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class QuestionPassages
{
    /// <summary>The passage of each given question, by question id; questions without one are missing.</summary>
    public static async Task<Dictionary<Guid, QuestionPassage>> ForQuestionsAsync(
        AppDbContext db, IReadOnlyCollection<Question> questions, CancellationToken ct)
    {
        var documentIds = questions.Select(q => q.DocumentId).Distinct().ToList();
        if (documentIds.Count == 0)
        {
            return [];
        }

        var passages = await db.QuestionPassages.AsNoTracking()
            .Where(p => documentIds.Contains(p.DocumentId))
            .OrderBy(p => p.FromNumber)
            .ToListAsync(ct);
        var result = new Dictionary<Guid, QuestionPassage>();
        foreach (var question in questions)
        {
            if (passages.FirstOrDefault(p => p.DocumentId == question.DocumentId
                    && question.SourceNumber >= p.FromNumber && question.SourceNumber <= p.ToNumber) is { } passage)
            {
                result[question.Id] = passage;
            }
        }

        return result;
    }

    /// <summary>
    /// Moves questions that share a passage next to each other (at the place of the first one, in their original
    /// order), so the text is read once. Other questions keep their random order.
    /// </summary>
    public static List<Guid> KeepTogether(
        IReadOnlyList<Guid> picked, IReadOnlyDictionary<Guid, QuestionPassage> passages, IReadOnlyDictionary<Guid, Question> questions)
    {
        var result = new List<Guid>(picked.Count);
        var placed = new HashSet<Guid>();
        foreach (var id in picked)
        {
            if (!placed.Add(id))
            {
                continue;
            }

            if (!passages.TryGetValue(id, out var passage))
            {
                result.Add(id);
                continue;
            }

            var group = picked.Where(other => passages.TryGetValue(other, out var p) && p.Id == passage.Id)
                .OrderBy(other => questions[other].SourceNumber)
                .ToList();
            result.AddRange(group);
            placed.UnionWith(group);
        }

        return result;
    }
}

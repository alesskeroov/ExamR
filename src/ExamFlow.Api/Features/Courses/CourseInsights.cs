using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Learning;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Courses;

/// <summary>A course material with its guide index (LearningId, when the file was indexed) and the student's guide entry.</summary>
public sealed record CourseMaterial(Guid DocumentId, string Name, string Format, Guid? LearningId, LearningStatus? Status, Guid? ChatId)
{
    public bool Ready => LearningId is not null && Status == LearningStatus.Completed;
}

/// <summary>A bank question to place in the lectures: its text and correct answer are only used server-side as search words.</summary>
public sealed record QuestionQuery(Guid QuestionId, string Text);

/// <summary>Progress and question-to-lecture links of a course, computed from data the app already stores (no AI).</summary>
public sealed class CourseInsights(AppDbContext db, CardDeckService decks, IChunkRetriever retriever)
{
    /// <summary>Wrong answers looked at for weak spots; older ones matter less and each costs a search per material.</summary>
    private const int MaxWrongForSpots = 100;
    public const int MaxWeakSpots = 5;
    private const int MaxQueryLength = 400;

    public async Task<List<CourseMaterial>> MaterialsAsync(Guid userId, IReadOnlyList<(Guid Id, string Name, string StoredFileName)> files, CancellationToken ct)
    {
        var ids = files.Select(f => f.Id).ToList();
        var learning = await db.Set<LearningDocument>().AsNoTracking().Where(l => ids.Contains(l.DocumentId))
            .Select(l => new { l.Id, l.DocumentId, l.Status }).ToListAsync(ct);
        var learningIds = learning.Select(l => l.Id).ToList();
        var chats = await db.Set<LearningChatSession>().AsNoTracking().Where(s => s.UserId == userId && learningIds.Contains(s.LearningDocumentId))
            .Select(s => new { s.Id, s.LearningDocumentId, s.UpdatedAt }).ToListAsync(ct);
        return files.Select(f =>
        {
            var index = learning.FirstOrDefault(l => l.DocumentId == f.Id);
            var chat = index is null ? null : chats.Where(c => c.LearningDocumentId == index.Id).MaxBy(c => c.UpdatedAt);
            return new CourseMaterial(f.Id, f.Name, Path.GetExtension(f.StoredFileName).TrimStart('.').ToLowerInvariant(), index?.Id, index?.Status, chat?.Id);
        }).ToList();
    }

    /// <summary>Cards the student studies in this material (hidden ones left out), mastered and weak among them.</summary>
    public async Task<MaterialProgress> CardsAsync(Guid userId, CourseMaterial material, CancellationToken ct)
    {
        var empty = new MaterialProgress(material.DocumentId, material.Name, material.Format, material.ChatId, material.Ready, 0, 0, 0);
        if (!material.Ready) return empty;
        var learningId = material.LearningId!.Value;
        var (deck, _) = await decks.GetAsync(learningId, ct);
        if (deck is null) return empty;
        var overrides = await db.Set<LearningCardOverride>().AsNoTracking().Where(o => o.UserId == userId && o.LearningDocumentId == learningId).ToListAsync(ct);
        var own = await db.Set<LearningUserCard>().AsNoTracking().Where(c => c.UserId == userId && c.LearningDocumentId == learningId).ToListAsync(ct);
        var visible = PersonalDeck.Merge(deck, overrides, own).Where(p => !p.Hidden).Select(p => p.Card.Id).ToHashSet();
        var states = await db.Set<LearningCardState>().AsNoTracking().Where(s => s.UserId == userId && s.LearningDocumentId == learningId)
            .Select(s => new { s.QuestionHash, s.Mastered, s.LastGrade }).ToListAsync(ct);
        var mine = states.Where(s => visible.Contains(s.QuestionHash)).ToList();
        return empty with { Cards = visible.Count, Mastered = mine.Count(s => s.Mastered), Weak = mine.Count(s => s.LastGrade != CardGrade.Good) };
    }

    /// <summary>
    /// Per bank: distinct questions, and how the student last answered each in finished exams (same rule as the exam
    /// "Səhvlərim" pool: identical questions share their latest answer). Also returns the questions last answered wrong.
    /// </summary>
    public async Task<(List<BankProgress> Banks, List<Guid> Wrong)> BanksAsync(Guid userId, IReadOnlyList<(Guid Id, string Name)> banks, CancellationToken ct)
    {
        var ids = banks.Select(b => b.Id).ToList();
        var questions = await db.Questions.AsNoTracking().Where(q => ids.Contains(q.DocumentId) && q.Options.Count >= 2)
            .Select(q => new { q.Id, q.DocumentId, q.ContentHash }).ToListAsync(ct);
        var answers = await db.Exams.AsNoTracking()
            .Where(e => e.UserId == userId && e.Status == ExamStatus.Completed && e.CompletedAt != null)
            .SelectMany(e => e.Questions
                .Where(q => q.Question != null && ids.Contains(q.Question.DocumentId) && q.Outcome != null && q.Outcome != ExamOutcome.Ungraded)
                .Select(q => new { q.Question!.ContentHash, CompletedAt = e.CompletedAt!.Value, Outcome = q.Outcome!.Value }))
            .ToListAsync(ct);
        var latest = answers.GroupBy(a => Convert.ToHexString(a.ContentHash)).ToDictionary(g => g.Key, g => g.MaxBy(a => a.CompletedAt)!.Outcome);

        var result = new List<BankProgress>();
        var wrong = new List<Guid>();
        foreach (var (id, name) in banks)
        {
            var byHash = questions.Where(q => q.DocumentId == id).GroupBy(q => Convert.ToHexString(q.ContentHash)).ToList();
            var answered = byHash.Where(g => latest.ContainsKey(g.Key)).ToList();
            var wrongHere = answered.Where(g => latest[g.Key] is ExamOutcome.Incorrect or ExamOutcome.Unanswered).ToList();
            wrong.AddRange(wrongHere.Select(g => g.First().Id));
            result.Add(new BankProgress(id, name, byHash.Count, answered.Count, answered.Count(g => latest[g.Key] == ExamOutcome.Correct), wrongHere.Count));
        }
        return (result, wrong);
    }

    /// <summary>Search words of bank questions: the question and its correct answer (never sent to the client).</summary>
    public async Task<List<QuestionQuery>> QueriesAsync(IReadOnlyList<Guid> questionIds, CancellationToken ct)
    {
        var rows = await db.Questions.AsNoTracking().Where(q => questionIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Text, Correct = q.Options.Where(o => o.IsCorrect).Select(o => o.Text).FirstOrDefault() }).ToListAsync(ct);
        return rows.Select(r =>
        {
            var text = $"{r.Text} {r.Correct}".Trim();
            return new QuestionQuery(r.Id, text.Length > MaxQueryLength ? text[..MaxQueryLength] : text);
        }).ToList();
    }

    /// <summary>The best matching lecture passage for each question, across the ready materials; questions without a good match are left out.</summary>
    public async Task<Dictionary<Guid, (CourseMaterial Material, RetrievedChunk Chunk)>> LinkAsync(IReadOnlyList<CourseMaterial> materials,
        IReadOnlyList<QuestionQuery> queries, CancellationToken ct)
    {
        var links = new Dictionary<Guid, (CourseMaterial, RetrievedChunk)>();
        var ready = materials.Where(m => m.Ready).ToList();
        if (ready.Count == 0) return links;
        foreach (var query in queries.Where(q => q.Text.Length > 0))
        {
            (CourseMaterial Material, RetrievedChunk Chunk)? best = null;
            foreach (var material in ready)
            {
                var found = await retriever.SearchAsync(material.LearningId!.Value, query.Text, 1, ct);
                if (!found.Found || found.Chunks.Count == 0) continue;
                var chunk = found.Chunks[0];
                // Scores of different files are not on one scale: the share of the question's words found decides first.
                if (best is null || chunk.Coverage > best.Value.Chunk.Coverage
                    || (chunk.Coverage == best.Value.Chunk.Coverage && chunk.Score > best.Value.Chunk.Score))
                    best = (material, chunk);
            }
            if (best is { } hit) links[query.QuestionId] = hit;
        }
        return links;
    }

    /// <summary>Lecture parts most of the student's wrong answers point to, most first.</summary>
    public async Task<List<WeakSpot>> WeakSpotsAsync(IReadOnlyList<CourseMaterial> materials, IReadOnlyList<Guid> wrong, CancellationToken ct)
    {
        if (wrong.Count == 0 || !materials.Any(m => m.Ready)) return [];
        var links = await LinkAsync(materials, await QueriesAsync(wrong.Take(MaxWrongForSpots).ToList(), ct), ct);
        return links.Values
            .GroupBy(l => (l.Material.DocumentId, l.Chunk.Ordinal))
            .Select(g => (Hit: g.First(), Count: g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Hit.Chunk.Ordinal)
            .Take(MaxWeakSpots)
            .Select(x => new WeakSpot(x.Hit.Material.DocumentId, x.Hit.Material.Name, x.Hit.Material.Format, x.Hit.Material.ChatId,
                x.Hit.Chunk.Section, x.Hit.Chunk.PageFrom, x.Hit.Chunk.PageTo, x.Hit.Chunk.Ordinal, x.Count))
            .ToList();
    }
}

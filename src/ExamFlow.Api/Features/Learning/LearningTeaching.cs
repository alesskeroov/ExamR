using System.Text.RegularExpressions;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

/// <summary>What the student asked the teacher to do. <see cref="Ask"/> is the normal free question.</summary>
public enum TutorAction { Ask, Teach, Next, Quiz, MultipleChoice, Answer, Hint, Simplify, Reveal }
public enum TutorDifficulty { Easy, Medium, Hard }
public enum TopicStatus { NotStarted, InProgress, Mastered }
public enum AnswerResult { Correct, Partial, Incorrect }

/// <summary>A teachable part of the document: consecutive chunks, taught one chunk (step) at a time.</summary>
public sealed record TutorTopic(string Key, string Title, IReadOnlyList<int> ChunkOrdinals, int? PageFrom, int? PageTo);

/// <summary>Learning progress of one user on one topic of one document (shared by all chats on that document).</summary>
public sealed class LearningTopicProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    public string TopicKey { get; set; } = "";
    public string Title { get; set; } = "";
    public TopicStatus Status { get; set; }
    public int CurrentStep { get; set; }
    public int StepCount { get; set; }
    public int Attempts { get; set; }
    public int Correct { get; set; }
    public int Partial { get; set; }
    /// <summary>A check question was asked and the next free-text message is its answer.</summary>
    public bool AwaitingAnswer { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LearningTopicProgressConfiguration : IEntityTypeConfiguration<LearningTopicProgress>
{
    public void Configure(EntityTypeBuilder<LearningTopicProgress> builder)
    {
        builder.ToTable("LearningTopicProgress");
        builder.Property(p => p.TopicKey).HasMaxLength(32);
        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(p => new { p.UserId, p.LearningDocumentId, p.TopicKey }).IsUnique();
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(p => p.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Splits a document into topics from index metadata only (no model call): headings when the document has them,
/// otherwise fixed groups of chunks named after their first sentence.
/// </summary>
public static partial class TopicPlanner
{
    public const int ChunksPerUntitledTopic = 3;
    public const int MaxStepsPerTopic = 6;

    public static IReadOnlyList<TutorTopic> Plan(IReadOnlyList<LearningDocumentChunk> chunks)
    {
        var ordered = chunks.OrderBy(c => c.Ordinal).ToList();
        var sections = ordered.Select(c => c.Section).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Count();
        var groups = new List<List<LearningDocumentChunk>>();
        if (sections >= 2)
        {
            foreach (var chunk in ordered)
            {
                if (groups.Count == 0 || groups[^1][^1].Section != chunk.Section || groups[^1].Count >= MaxStepsPerTopic) groups.Add([]);
                groups[^1].Add(chunk);
            }
        }
        else
        {
            groups.AddRange(ordered.Chunk(ChunksPerUntitledTopic).Select(g => g.ToList()));
        }

        return groups.Select((g, i) =>
        {
            var pages = g.Where(c => c.PageFrom is not null).ToList();
            var title = !string.IsNullOrWhiteSpace(g[0].Section) && sections >= 2 ? g[0].Section!.Trim() : FirstSentence(g[0].Text);
            return new TutorTopic($"t{g[0].Ordinal}", Shorten(title, 90), g.Select(c => c.Ordinal).ToList(),
                pages.Count > 0 ? pages.Min(c => c.PageFrom) : null, pages.Count > 0 ? pages.Max(c => c.PageTo ?? c.PageFrom) : null);
        }).ToList();
    }

    private static string FirstSentence(string text)
    {
        // List markers and numbering ("• ", "12. ") are not part of a title; a bullet also ends it.
        var clean = Spaces().Replace(text, " ").TrimStart('#', ' ', '•', '-', '–', '*', '.', ')', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9').Trim();
        var end = clean.IndexOfAny(['.', '?', '!', '•']);
        return end > 10 ? clean[..end] : clean;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
}

/// <summary>
/// Turns a teacher request into the topic part to work on. The progress row is not changed here: <see cref="Apply"/>
/// writes it only after the answer reached the student, so a failed or stopped answer leaves progress untouched.
/// </summary>
public sealed class TeachingStep
{
    public TutorAction Action { get; private init; }
    public TutorTopic? Topic { get; private init; }
    public LearningTopicProgress? Progress { get; private init; }
    public int Step { get; private init; }
    public IReadOnlyList<RetrievedChunk> Sources { get; private init; } = [];
    public string? Error { get; private init; }
    private bool _isNew;

    public static Task<List<LearningDocumentChunk>> ChunksAsync(AppDbContext db, Guid learningDocumentId, CancellationToken ct) =>
        db.Set<LearningDocumentChunk>().AsNoTracking().Where(c => c.LearningDocumentId == learningDocumentId).OrderBy(c => c.Ordinal).ToListAsync(ct);

    /// <summary>Null for an ordinary question (free mode, or nothing pending in teacher mode).</summary>
    public static async Task<TeachingStep?> ResolveAsync(AppDbContext db, Guid userId, Guid learningDocumentId, SendMessageRequest request,
        DateTimeOffset now, CancellationToken ct)
    {
        var rows = db.Set<LearningTopicProgress>().Where(p => p.UserId == userId && p.LearningDocumentId == learningDocumentId);
        var action = request.Action;
        LearningTopicProgress? progress = null;
        if (action == TutorAction.Ask)
        {
            if (request.Mode != TutorMode.StepByStep) return null;
            progress = await rows.Where(p => p.AwaitingAnswer).OrderByDescending(p => p.UpdatedAt).FirstOrDefaultAsync(ct);
            if (progress is null) return null;
            action = TutorAction.Answer;
        }

        var topics = TopicPlanner.Plan(await ChunksAsync(db, learningDocumentId, ct));
        if (topics.Count == 0) return new TeachingStep { Error = "Bu sənəddə öyrədiləcək mətn tapılmadı." };
        string? key = request.TopicKey ?? progress?.TopicKey
            ?? await rows.OrderByDescending(p => p.UpdatedAt).Select(p => p.TopicKey).FirstOrDefaultAsync(ct);
        var topic = key is null ? topics[0] : topics.FirstOrDefault(t => t.Key == key);
        if (topic is null) return new TeachingStep { Error = "Mövzu tapılmadı. Mövzu siyahısını yeniləyin." };
        progress ??= await rows.FirstOrDefaultAsync(p => p.TopicKey == topic.Key, ct);

        var step = Math.Clamp(progress?.CurrentStep ?? 0, 0, topic.ChunkOrdinals.Count - 1);
        if (action == TutorAction.Next)
        {
            if (step + 1 < topic.ChunkOrdinals.Count) step++;
            else
            {
                // Last part done: continue with the next topic where the student left it (or at its start).
                var index = topics.ToList().IndexOf(topic);
                if (index + 1 < topics.Count)
                {
                    topic = topics[index + 1];
                    progress = await rows.FirstOrDefaultAsync(p => p.TopicKey == topic.Key, ct);
                    step = Math.Clamp(progress?.CurrentStep ?? 0, 0, topic.ChunkOrdinals.Count - 1);
                }
            }
        }

        var isNew = progress is null;
        progress ??= new LearningTopicProgress
        {
            Id = Guid.NewGuid(), UserId = userId, LearningDocumentId = learningDocumentId, TopicKey = topic.Key, Status = TopicStatus.NotStarted, UpdatedAt = now,
        };
        var ordinal = topic.ChunkOrdinals[step];
        var chunk = await db.Set<LearningDocumentChunk>().AsNoTracking().FirstAsync(c => c.LearningDocumentId == learningDocumentId && c.Ordinal == ordinal, ct);
        return new TeachingStep
        {
            Action = action, Topic = topic, Progress = progress, Step = step, _isNew = isNew,
            Sources = [new RetrievedChunk(chunk.Ordinal, 1, 1, chunk.PageFrom, chunk.PageTo, chunk.Section, chunk.ParagraphFrom, chunk.ParagraphTo, chunk.Text)],
        };
    }

    public void Apply(AppDbContext db, AnswerResult? verdict, DateTimeOffset now)
    {
        var progress = Progress!;
        if (_isNew) db.Add(progress);
        progress.Title = Topic!.Title;
        progress.StepCount = Topic.ChunkOrdinals.Count;
        progress.CurrentStep = Step;
        switch (Action)
        {
            case TutorAction.Teach or TutorAction.Next or TutorAction.Quiz or TutorAction.MultipleChoice:
                progress.AwaitingAnswer = true;
                if (progress.Status == TopicStatus.NotStarted) progress.Status = TopicStatus.InProgress;
                break;
            case TutorAction.Answer when verdict is { } result:
                TeachingPrompt.Record(progress, result);
                break;
            case TutorAction.Answer or TutorAction.Reveal:
                progress.AwaitingAnswer = false;
                break;
        }
        progress.UpdatedAt = now;
    }
}

/// <summary>Teacher instructions per action and the machine-readable verdict the model appends to evaluations.</summary>
public static partial class TeachingPrompt
{
    public static string Instruction(TutorAction action, TutorTopic topic, int step, TutorDifficulty difficulty)
    {
        var where = $"Topic: \"{TutorPrompt.Neutralise(topic.Title)}\", part {step + 1} of {topic.ChunkOrdinals.Count}.";
        var level = difficulty switch
        {
            TutorDifficulty.Easy => "easy (recall of a single fact)",
            TutorDifficulty.Hard => "hard (connect or apply several facts)",
            _ => "medium (understanding of the main idea)",
        };
        var body = action switch
        {
            TutorAction.Teach or TutorAction.Next =>
                "Teach this part of the sources to the student in small, clear pieces (short paragraphs or numbered steps). " +
                $"Then ask exactly ONE short check question of {level} difficulty whose answer is in the sources. Do not give its answer.",
            TutorAction.Quiz =>
                $"Ask exactly ONE new check question of {level} difficulty about this part of the sources. Do not give its answer. No explanation before it.",
            TutorAction.MultipleChoice =>
                $"Write ONE multiple-choice question of {level} difficulty about this part of the sources, with options A) B) C) D) on separate lines. " +
                "Exactly one option is correct and must be supported by the sources; wrong options must be plausible. Do not reveal the answer.",
            TutorAction.Answer =>
                "In this turn the text inside <question> is the student's ANSWER to the last question the tutor asked in <history>. It is not a new question: " +
                "never reply that the document does not contain it. Compare it with the sources. " +
                "Line 1: only the verdict, translated into the reply language (in Azerbaijani: \"Düzdür\", \"Qismən düzdür\" or \"Düz deyil\"). " +
                "If not fully correct: do NOT write the correct answer or its key fact anywhere in this reply (the student can ask for it separately); " +
                "give one sentence with a clue about what to look for in the sources, then ask the student to try again. " +
                "If correct: one sentence confirming why, with a citation. " +
                "On the very last line write exactly one tag: [[result:correct]], [[result:partial]] or [[result:incorrect]].",
            TutorAction.Hint =>
                "Give ONE short clue for the last question the tutor asked in <history>. The clue must NOT contain the answer or its key words; " +
                "point to the idea or the part of the sources to think about, then ask the student to answer.",
            TutorAction.Simplify =>
                "Explain this part of the sources again in simpler words, shorter sentences and a step-by-step order. Do not add facts that are not in the sources.",
            TutorAction.Reveal =>
                "Show the correct answer to the most recent question in <history> and briefly explain why, citing the sources.",
            _ => "",
        };
        return $"\n\nTeacher mode. {where}\n{body}";
    }

    /// <summary>Visible text of the student's turn for actions triggered by buttons (stored in the chat history).</summary>
    public static string StudentLine(TutorAction action, TutorTopic topic, TutorDifficulty difficulty) => action switch
    {
        TutorAction.Teach => $"Mövzunu öyrət: {topic.Title}",
        TutorAction.Next => "Növbəti hissəyə keç",
        TutorAction.Quiz => $"Məni yoxla ({DifficultyLabel(difficulty)})",
        TutorAction.MultipleChoice => $"Variantlı sual hazırla ({DifficultyLabel(difficulty)})",
        TutorAction.Hint => "İpucu ver",
        TutorAction.Simplify => "Daha sadə izah et",
        TutorAction.Reveal => "Doğru cavabı göstər",
        _ => "",
    };

    private static string DifficultyLabel(TutorDifficulty difficulty) =>
        difficulty switch { TutorDifficulty.Easy => "asan", TutorDifficulty.Hard => "çətin", _ => "orta" };

    /// <summary>Reads and removes the verdict tag; null when the model did not add one.</summary>
    public static (string Text, AnswerResult? Result) ExtractResult(string answer)
    {
        var match = ResultTag().Match(answer);
        if (!match.Success) return (answer, null);
        var result = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "correct" => AnswerResult.Correct,
            "partial" => AnswerResult.Partial,
            _ => AnswerResult.Incorrect,
        };
        return (ResultTag().Replace(answer, "").TrimEnd(), result);
    }

    /// <summary>Length of text that is safe to stream now: anything from a possible "[[" onwards is held back.</summary>
    public static int SafeLength(string text)
    {
        var tag = text.IndexOf("[[", StringComparison.Ordinal);
        if (tag >= 0) return tag;
        return text.EndsWith('[') ? text.Length - 1 : text.Length;
    }

    /// <summary>Applies one evaluated answer. Mastered = last part reached and at least 70% of answers correct.</summary>
    public static void Record(LearningTopicProgress progress, AnswerResult result)
    {
        progress.Attempts++;
        if (result == AnswerResult.Correct) progress.Correct++;
        else if (result == AnswerResult.Partial) progress.Partial++;
        // A wrong or partial answer keeps the question open for a hint, another try or the revealed answer.
        progress.AwaitingAnswer = result != AnswerResult.Correct;
        var score = (progress.Correct + progress.Partial * 0.5) / progress.Attempts;
        if (progress.CurrentStep >= progress.StepCount - 1 && score >= 0.7 && result == AnswerResult.Correct) progress.Status = TopicStatus.Mastered;
        else if (progress.Status == TopicStatus.NotStarted) progress.Status = TopicStatus.InProgress;
    }

    /// <summary>Weak = answered at least twice with under 60% success.</summary>
    public static bool IsWeak(LearningTopicProgress progress) =>
        progress.Attempts >= 2 && (progress.Correct + progress.Partial * 0.5) / progress.Attempts < 0.6;

    [GeneratedRegex(@"\s*\[\[\s*result\s*:\s*(correct|partial|incorrect)\s*\]\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ResultTag();
}

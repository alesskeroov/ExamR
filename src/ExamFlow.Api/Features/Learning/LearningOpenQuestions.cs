using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExamFlow.Api.Features.Learning;

/// <summary>
/// What the AI tutor may do with a document. Only <see cref="Open"/> and <see cref="Mixed"/> are supported.
/// NotPdf = a format the tutor does not read (anything but PDF and DOCX); the name is kept because stored indexes use the value.
/// </summary>
public enum OpenDocumentKind { Open, Mixed, MultipleChoice, NoOpenQuestions, Unverifiable, NotPdf }

/// <summary>One open question with the answer text the document gives under it. SharedAnswer: the document answers it together
/// with the next question heading(s), so the answer may also cover those.</summary>
public sealed record IndexedQuestion(int Number, string Text, string Answer, int ChunkOrdinal, int? Page, bool SharedAnswer = false);

/// <summary>Why a found question is not used for learning. It is kept (and shown to the student), never deleted.</summary>
public enum ExclusionReason { NoAnswer, MultipleChoice, Unclear }

public sealed record ExcludedQuestion(int Number, string Text, int? Page, ExclusionReason Reason);

/// <summary>What the exam parser saw for one question number of the PDF.</summary>
public sealed record ParsedQuestionInfo(int Number, int OptionCount, bool HasMarkedAnswer, bool HasWrittenAnswer, int? Page);

/// <summary>The document's reliable open questions, built once per indexed document version and reused by every tutor tool.</summary>
/// <summary>Questions = usable for learning (with a reliable answer). Excluded = found but not usable, each with its reason.</summary>
public sealed record OpenQuestionIndex(string Version, OpenDocumentKind Kind, int ChoiceCount, int UnclearCount, int UnansweredCount,
    IReadOnlyList<IndexedQuestion> Questions, IReadOnlyList<ExcludedQuestion>? Excluded = null)
{
    public bool Supported => Kind is OpenDocumentKind.Open or OpenDocumentKind.Mixed;

    /// <summary>Changes whenever a question or answer text changes, so cached AI results built from them are not reused.</summary>
    public string Fingerprint()
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var q in Questions)
            sha.AppendData(Encoding.UTF8.GetBytes($"{q.Number}\u001f{q.Text}\u001f{q.Answer}\u001f{q.Page}\u001e"));
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    /// <summary>Why the tutor refuses the document, or null when it is supported.</summary>
    public string? Rejection => Kind switch
    {
        OpenDocumentKind.NotPdf => "AI Tutor yalnız PDF və Word (.docx) sənədləri ilə işləyir. TXT və köhnə .doc faylları dəstəklənmir.",
        OpenDocumentKind.MultipleChoice => "Bu sənəd variantlı testdir. AI Tutor yalnız açıq suallı sənədlər üçündür.",
        OpenDocumentKind.NoOpenQuestions => "Sənəddə cavabı ilə verilmiş açıq sual tapılmadı. Suallar nömrələnməli, cavab mətni isə sualın altında olmalıdır.",
        OpenDocumentKind.Unverifiable => "Sənədin sual quruluşu yoxlanıla bilmədi, ona görə AI Tutor üçün qəbul edilmədi.",
        _ => null,
    };

    public string? Warning
    {
        get
        {
            var parts = new List<string>();
            if (Kind == OpenDocumentKind.Mixed)
                parts.Add($"Qarışıq sənəd: yalnız {Questions.Count} açıq sual istifadə olunur"
                    + (ChoiceCount > 0 ? $", {ChoiceCount} variantlı sual" : "") + (UnclearCount > 0 ? $", {UnclearCount} növü müəyyən olmayan sual" : "") + " çıxarıldı.");
            if (UnansweredCount > 0) parts.Add($"Sənəddə cavabı tapılmayan {UnansweredCount} sual saxlanılıb, amma öyrənmə və yoxlamada istifadə edilmir.");
            if (Questions.Count >= OpenQuestionExtractor.MaxQuestions) parts.Add($"İlk {OpenQuestionExtractor.MaxQuestions} sual götürülür.");
            return parts.Count == 0 ? null : string.Join(' ', parts);
        }
    }

    /// <summary>The shape the study tools already work with.</summary>
    public OpenQuestionScan ToScan() => new(
        Questions.Select(q => new OpenQuestion(q.Number, q.Text, q.ChunkOrdinal, q.Page, true)).ToList(),
        false, Questions.ToDictionary(q => q.Number, q => q.Answer));

    /// <summary>The indexed question a student's message is about, when its words clearly match one question.</summary>
    public IndexedQuestion? Match(string message)
    {
        var terms = TextNormalizer.Terms(message).ToHashSet();
        if (terms.Count < 2) return null;
        IndexedQuestion? best = null;
        var bestScore = 0.0;
        foreach (var q in Questions)
        {
            var own = TextNormalizer.Terms(q.Text).ToHashSet();
            if (own.Count == 0) continue;
            var shared = own.Count(terms.Contains);
            // Most of the question's words, and most of the message's words, must match.
            var score = Math.Min(shared / (double)own.Count, shared / (double)terms.Count);
            if (score > bestScore) (best, bestScore) = (q, score);
        }
        return bestScore >= 0.6 ? best : null;
    }
}

/// <summary>
/// Decides per question, not per document. The exam parser marks a question as multiple-choice only when it has options AND a
/// marked correct answer (√, bold, colour); letters such as "A)" alone are never enough. Open questions come from the text index,
/// so answers written without a "Cavab:" label are kept.
/// </summary>
public static partial class OpenQuestionClassifier
{
    public const string Version = "oq2";
    public const int MinOpenQuestions = 3;
    public const int MinAnswerCharacters = 20;
    /// <summary>A few "open" hits inside a large test are noise, not a mixed document.</summary>
    public const int MaxChoicePerOpen = 10;

    public static OpenQuestionIndex Classify(IReadOnlyList<LearningDocumentChunk> chunks, IReadOnlyList<ParsedQuestionInfo>? parsed)
    {
        if (parsed is null) return new(Version, OpenDocumentKind.Unverifiable, 0, 0, 0, []);

        var choice = parsed.Where(p => p.OptionCount >= 2 && p.HasMarkedAnswer && !p.HasWrittenAnswer).Select(p => p.Number).ToHashSet();
        var unclear = parsed.Where(p => p.OptionCount >= 2 && !p.HasMarkedAnswer && !p.HasWrittenAnswer).Select(p => p.Number).ToHashSet();
        var pages = parsed.GroupBy(p => p.Number).ToDictionary(g => g.Key, g => g.First().Page);

        var scan = OpenQuestionExtractor.Scan(chunks);
        var sharedAnswers = scan.SharedAnswers?.ToHashSet() ?? [];
        string AnswerOf(OpenQuestion q) => AnswerLabel().Replace(scan.Answers.GetValueOrDefault(q.Number) ?? "", "").Trim();
        // Options without a marked answer in a document that has no real test question are a list inside a written answer
        // (e.g. "1. Strategiya fərqi …"), as long as such questions are a small minority. An unmarked test stays unclear.
        var openCandidates = scan.Questions.Count(q => !choice.Contains(q.Number) && !unclear.Contains(q.Number));
        var listsInAnswers = choice.Count == 0 && unclear.Count > 0 && unclear.Count * 5 <= openCandidates;

        var open = new List<IndexedQuestion>();
        var excluded = new List<ExcludedQuestion>();
        foreach (var q in scan.Questions)
        {
            var page = pages.GetValueOrDefault(q.Number) ?? q.Page;
            var answer = AnswerOf(q);
            if (choice.Contains(q.Number)) { excluded.Add(new(q.Number, q.Text, page, ExclusionReason.MultipleChoice)); continue; }
            if (unclear.Contains(q.Number) && !(listsInAnswers && answer.Length >= 60)) { excluded.Add(new(q.Number, q.Text, page, ExclusionReason.Unclear)); continue; }
            // Never graded or explained without the document's own answer: kept as "no answer" instead.
            if (answer.Length < MinAnswerCharacters) { excluded.Add(new(q.Number, q.Text, page, ExclusionReason.NoAnswer)); continue; }
            open.Add(new IndexedQuestion(q.Number, q.Text, answer, q.ChunkOrdinal, page, sharedAnswers.Contains(q.Number)));
        }

        int Count(ExclusionReason reason) => excluded.Count(e => e.Reason == reason);
        // The parser's own counts decide the document type: test questions the text scan did not list still count.
        var usedNumbers = open.Select(o => o.Number).ToHashSet();
        var choiceCount = choice.Count;
        var unclearCount = unclear.Count(n => !usedNumbers.Contains(n));
        var kind = open.Count >= MinOpenQuestions && choiceCount <= open.Count * MaxChoicePerOpen
            ? choiceCount > 0 || unclearCount > 0 ? OpenDocumentKind.Mixed : OpenDocumentKind.Open
            : choiceCount > 0 ? OpenDocumentKind.MultipleChoice : OpenDocumentKind.NoOpenQuestions;
        var supported = kind is OpenDocumentKind.Open or OpenDocumentKind.Mixed;
        return new(Version, kind, choiceCount, unclearCount, Count(ExclusionReason.NoAnswer), supported ? open : [], supported ? excluded : []);
    }

    /// <summary>Runs the exam parser over the PDF only to see which questions are tests. Null when the PDF cannot be parsed.</summary>
    public static IReadOnlyList<ParsedQuestionInfo>? Inspect(PdfQuestionParser parser, Stream pdf, CancellationToken ct) =>
        Infos(parser.Parse(pdf, ct), withPages: true);

    /// <summary>The same check for a Word (DOCX) file; Word has no page numbers, so none are kept.</summary>
    public static IReadOnlyList<ParsedQuestionInfo>? InspectDocx(DocxQuestionParser parser, Stream docx, CancellationToken ct) =>
        Infos(parser.Parse(docx, ct), withPages: false);

    private static IReadOnlyList<ParsedQuestionInfo>? Infos(ParseResult result, bool withPages)
    {
        if (result.Failure is ParseFailure.Encrypted or ParseFailure.Unreadable or ParseFailure.Scanned) return null;
        return result.Questions.Select(q => new ParsedQuestionInfo(q.Number, q.Options.Count, q.CorrectIndex is not null,
            !string.IsNullOrWhiteSpace(q.Explanation), withPages ? q.Anchor?.PageNumber : null)).ToList();
    }

    [GeneratedRegex(@"^\s*(?:cavab|[iİıI]zah|answer)\s*[:：]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnswerLabel();
}

/// <summary>Loads the stored question index, or builds and stores it once (documents indexed before it existed).</summary>
public sealed class OpenQuestionIndexer(AppDbContext db, IFileStorage storage, PdfQuestionParser parser, DocxQuestionParser docxParser, IMemoryCache cache,
    TimeProvider clock, ILogger<OpenQuestionIndexer> logger)
{
    /// <summary>Formats the tutor reads ("pdf", "docx"); TXT and anything else are refused.</summary>
    public static bool IsSupportedFormat(string? format) =>
        string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(180);

    private static string Numbers(OpenQuestionIndex index, ExclusionReason reason) =>
        string.Join(',', (index.Excluded ?? []).Where(e => e.Reason == reason).Select(e => e.Number));

    public static string CacheKey(Guid learningId, byte[] sha256, DateTimeOffset? processedAt) =>
        StudyPrompts.CacheKey("question-index", OpenQuestionClassifier.Version, learningId, sha256, processedAt?.UtcTicks);

    public static LearningMaterial Row(Guid learningId, byte[] sha256, DateTimeOffset? processedAt, OpenQuestionIndex index, DateTimeOffset now) => new()
    {
        // Document-level data, not a user's material: never listed (no user owns Guid.Empty).
        Id = Guid.NewGuid(), UserId = Guid.Empty, LearningDocumentId = learningId, DocumentSha256 = sha256, Kind = MaterialKind.QuestionIndex,
        CacheKey = CacheKey(learningId, sha256, processedAt), PromptVersion = OpenQuestionClassifier.Version,
        PayloadJson = JsonSerializer.Serialize(index, Json), CreatedAt = now,
    };

    /// <summary>Null when the learning document does not exist or is not indexed yet.</summary>
    public async Task<OpenQuestionIndex?> GetAsync(Guid learningId, CancellationToken ct)
    {
        var learning = await db.Set<LearningDocument>().AsNoTracking().Where(l => l.Id == learningId && l.Status == LearningStatus.Completed)
            .Select(l => new { l.Format, l.ProcessedAt, l.Document.Sha256, l.Document.StoredFileName }).FirstOrDefaultAsync(ct);
        if (learning is null) return null;
        if (!IsSupportedFormat(learning.Format)) return new(OpenQuestionClassifier.Version, OpenDocumentKind.NotPdf, 0, 0, 0, []);

        var key = CacheKey(learningId, learning.Sha256, learning.ProcessedAt);
        if (cache.TryGetValue(key, out OpenQuestionIndex? hit) && hit is not null) return hit;

        var stored = await db.Set<LearningMaterial>().AsNoTracking().Where(m => m.LearningDocumentId == learningId && m.CacheKey == key)
            .Select(m => m.PayloadJson).FirstOrDefaultAsync(ct);
        var index = stored is null ? null : JsonSerializer.Deserialize<OpenQuestionIndex>(stored, Json);
        if (index is null)
        {
            (index, var final) = await BuildAsync(learningId, learning.StoredFileName, learning.Format, ct);
            // A timeout or storage error is not a property of the document: answer "unverifiable" now, try again next time.
            if (!final) return index;
            // Diagnostics without document text: counts and question numbers per reason only.
            logger.LogInformation(
                "Question index {LearningId}: {Kind}, {Used} usable ({Shared} with a shared answer), no answer [{NoAnswer}], test [{Choice}], unclear [{Unclear}]",
                learningId, index.Kind, index.Questions.Count, index.Questions.Count(q => q.SharedAnswer),
                Numbers(index, ExclusionReason.NoAnswer), Numbers(index, ExclusionReason.MultipleChoice), Numbers(index, ExclusionReason.Unclear));
            db.Add(Row(learningId, learning.Sha256, learning.ProcessedAt, index, clock.GetUtcNow()));
            await db.SaveChangesAsync(ct);
        }
        cache.Set(key, index, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
        return index;
    }

    private async Task<(OpenQuestionIndex Index, bool Final)> BuildAsync(Guid learningId, string storedFileName, string format, CancellationToken ct)
    {
        var chunks = await TeachingStep.ChunksAsync(db, learningId, ct);
        IReadOnlyList<ParsedQuestionInfo>? parsed = null;
        var final = false;
        var tempPath = Path.GetTempFileName();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(BuildTimeout);
            await using (var source = storage.OpenRead(storedFileName))
            await using (var target = File.Create(tempPath))
                await source.CopyToAsync(target, timeout.Token);
            await using var file = File.OpenRead(tempPath);
            var docx = string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase);
            parsed = await Task.Run(() => docx
                ? OpenQuestionClassifier.InspectDocx(docxParser, file, timeout.Token)
                : OpenQuestionClassifier.Inspect(parser, file, timeout.Token), timeout.Token);
            final = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Question index for learning document {LearningId} timed out", learningId);
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            logger.LogWarning("Question index for learning document {LearningId} could not read the file", learningId);
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
        }
        return (OpenQuestionClassifier.Classify(chunks, parsed), final);
    }
}

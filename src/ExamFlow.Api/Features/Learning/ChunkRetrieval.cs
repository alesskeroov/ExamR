using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Learning;

public sealed record RetrievedChunk(int Ordinal, double Score, double Coverage, int? PageFrom, int? PageTo, string? Section, int ParagraphFrom, int ParagraphTo, string Text);

/// <summary><see cref="Found"/> is false when nothing in the document matches well enough; callers must then say so instead of guessing.</summary>
public sealed record RetrievalResult(bool Found, IReadOnlyList<RetrievedChunk> Chunks, string Method);

/// <summary>Finds the chunks of one indexed document that answer a query. Lexical today; an embedding/hybrid retriever can implement the same contract.</summary>
public interface IChunkRetriever
{
    Task<RetrievalResult> SearchAsync(Guid learningDocumentId, string query, int topK, CancellationToken ct);
}

public sealed class RetrievalOptions
{
    public int TopK { get; set; } = 5;
    public int MaxTopK { get; set; } = 8;
    /// <summary>Share of the query's distinct terms a chunk must contain to count as a source.</summary>
    public double MinCoverage { get; set; } = 0.5;
    /// <summary>Long questions carry many filler words: matching this many distinct terms is enough even below MinCoverage.</summary>
    public int MinMatchedTerms { get; set; } = 2;
    /// <summary>Two results sharing at least this share of terms are treated as duplicates (overlap, repeated pages).</summary>
    public double DuplicateSimilarity { get; set; } = 0.8;
    public int MaxIndexedChunks { get; set; } = 6000;
}

/// <summary>BM25 over one document's chunks, cached per index version (re-indexing produces a new cache key).</summary>
public sealed class LexicalChunkRetriever(AppDbContext db, IMemoryCache cache, IOptions<LearningOptions> options) : IChunkRetriever
{
    public async Task<RetrievalResult> SearchAsync(Guid learningDocumentId, string query, int topK, CancellationToken ct)
    {
        var settings = options.Value.Retrieval;
        var version = await db.Set<LearningDocument>().AsNoTracking().Where(d => d.Id == learningDocumentId && d.Status == LearningStatus.Completed)
            .Select(d => d.ProcessedAt).FirstOrDefaultAsync(ct);
        if (version is null) return new RetrievalResult(false, [], "lexical");

        var index = await cache.GetOrCreateAsync(("learning-index", learningDocumentId, version.Value), async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);
            entry.Size = 1;
            var chunks = await db.Set<LearningDocumentChunk>().AsNoTracking().Where(c => c.LearningDocumentId == learningDocumentId)
                .OrderBy(c => c.Ordinal).Take(settings.MaxIndexedChunks).ToListAsync(ct);
            return LexicalIndex.Build(chunks);
        });
        return index!.Search(query, Math.Clamp(topK, 1, settings.MaxTopK), settings);
    }
}

/// <summary>Pure, testable BM25 index with Azerbaijani-aware normalisation.</summary>
public sealed class LexicalIndex
{
    private const double K1 = 1.2, B = 0.75, PhraseBonus = 2.0;
    private readonly List<(LearningDocumentChunk Chunk, Dictionary<string, int> Terms, int Length, string Folded)> _docs;
    private readonly Dictionary<string, int> _documentFrequency;
    private readonly double _averageLength;

    private LexicalIndex(List<(LearningDocumentChunk, Dictionary<string, int>, int, string)> docs)
    {
        _docs = docs;
        _documentFrequency = docs.SelectMany(d => d.Item2.Keys).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        _averageLength = docs.Count == 0 ? 1 : Math.Max(1, docs.Average(d => d.Item3));
    }

    public static LexicalIndex Build(IEnumerable<LearningDocumentChunk> chunks) =>
        new(chunks.Select(c =>
        {
            var terms = TextNormalizer.Terms(c.Text).ToList();
            return (c, terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count()), terms.Count, TextNormalizer.FoldedPhrase(c.Text));
        }).ToList());

    public RetrievalResult Search(string query, int topK, RetrievalOptions options)
    {
        var queryTerms = TextNormalizer.Terms(query).Distinct().ToList();
        if (queryTerms.Count == 0 || _docs.Count == 0) return new RetrievalResult(false, [], "lexical");
        var phrase = TextNormalizer.FoldedPhrase(query);
        var n = _docs.Count;

        var scored = _docs.Select(d =>
        {
            double score = 0;
            var matched = 0;
            foreach (var term in queryTerms)
            {
                if (!d.Terms.TryGetValue(term, out var tf)) continue;
                matched++;
                var df = _documentFrequency[term];
                var idf = Math.Log(1 + (n - df + 0.5) / (df + 0.5));
                score += idf * tf * (K1 + 1) / (tf + K1 * (1 - B + B * d.Length / _averageLength));
            }
            // Exact multi-word phrase present in the chunk (e.g. a definition being looked up).
            if (queryTerms.Count > 1 && phrase.Length > 0 && d.Folded.Contains(phrase, StringComparison.Ordinal)) score += PhraseBonus;
            return (Doc: d, Score: score, Coverage: (double)matched / queryTerms.Count, Matched: matched);
        })
        .Where(s => s.Score > 0 && (s.Coverage >= options.MinCoverage - 1e-9 || s.Matched >= options.MinMatchedTerms))
        .OrderByDescending(s => s.Score).ThenBy(s => s.Doc.Chunk.Ordinal)
        .ToList();

        // Near-duplicate suppression: overlapping neighbours or repeated pages should not fill the context twice.
        var selected = new List<(LearningDocumentChunk Chunk, Dictionary<string, int> Terms, double Score, double Coverage)>();
        foreach (var candidate in scored)
        {
            if (selected.Any(s => Jaccard(s.Terms, candidate.Doc.Terms) >= options.DuplicateSimilarity)) continue;
            selected.Add((candidate.Doc.Chunk, candidate.Doc.Terms, candidate.Score, candidate.Coverage));
            if (selected.Count == topK) break;
        }

        var results = selected.Select(s => new RetrievedChunk(s.Chunk.Ordinal, Math.Round(s.Score, 3), Math.Round(s.Coverage, 2), s.Chunk.PageFrom, s.Chunk.PageTo,
            s.Chunk.Section, s.Chunk.ParagraphFrom, s.Chunk.ParagraphTo, s.Chunk.Text)).ToList();
        return new RetrievalResult(results.Count > 0, results, "lexical");
    }

    private static double Jaccard(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        var intersection = a.Keys.Count(b.ContainsKey);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}

/// <summary>
/// Lower-cases with Azerbaijani casing (İ→i, I→ı), folds letters to ASCII (ə→e, ş→s, …) so spelling variants and
/// Turkish/English transliterations meet, drops stop words, and keeps a short prefix as stem so suffixes
/// (hüceyrə, hüceyrənin, hüceyrədə) collapse to one term. No external language resources are needed.
/// </summary>
public static partial class TextNormalizer
{
    private static readonly CultureInfo Az = CultureInfo.GetCultureInfo("az-Latn-AZ");
    private const int StemLength = 6;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        // Azerbaijani (folded)
        "ve", "ile", "bu", "o", "bir", "da", "de", "ki", "ne", "nedir", "nece", "hansi", "kim", "ucun", "olan", "olur", "edir", "var", "yox",
        "mi", "mu", "ise", "amma", "lakin", "hem", "ya", "ve", "en", "cox", "az", "her", "kimi", "gore", "sonra", "evvel", "harada", "niye",
        // English
        "the", "a", "an", "is", "are", "of", "to", "in", "on", "for", "and", "or", "what", "how", "why", "which", "who", "does", "do",
        // Russian
        "и", "в", "на", "что", "как", "это", "по", "для", "не",
    };

    public static IEnumerable<string> Terms(string text)
    {
        foreach (Match word in Words().Matches(text.ToLower(Az)))
        {
            var folded = Fold(word.Value);
            if (folded.Length < 2 || StopWords.Contains(folded)) continue;
            yield return folded.Length > StemLength && !char.IsDigit(folded[0]) ? folded[..StemLength] : folded;
        }
    }

    /// <summary>Folded words joined by single spaces, for exact-phrase matching.</summary>
    public static string FoldedPhrase(string text) =>
        string.Join(' ', Words().Matches(text.ToLower(Az)).Select(m => Fold(m.Value)));

    private static string Fold(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var c in word)
            builder.Append(c switch { 'ə' => 'e', 'ı' => 'i', 'ş' => 's', 'ç' => 'c', 'ğ' => 'g', 'ö' => 'o', 'ü' => 'u', _ => c });
        return builder.ToString();
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")] private static partial Regex Words();
}

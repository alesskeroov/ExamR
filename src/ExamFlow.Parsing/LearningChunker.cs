using System.Text.RegularExpressions;

namespace ExamFlow.Parsing;

/// <summary>Sizes in estimated tokens; tune from retrieval tests via configuration.</summary>
public sealed record ChunkingOptions(int TargetTokens = 650, int MinTokens = 500, int MaxTokens = 800, int OverlapTokens = 100, double CharactersPerToken = 3.5);

public sealed record TextChunk(int Ordinal, string Text, int Tokens, int? PageFrom, int? PageTo, string? Section, int ParagraphFrom, int ParagraphTo);

/// <summary>
/// Packs paragraphs into retrieval chunks: paragraphs stay whole when they fit, long ones split on sentences,
/// a section change closes the chunk early, and each chunk starts with the tail of the previous one (overlap).
/// </summary>
public static partial class LearningChunker
{
    private sealed record Unit(string Text, int Tokens, TextBlock Block);

    public static int EstimateTokens(string text, ChunkingOptions options) => (int)Math.Ceiling(text.Length / options.CharactersPerToken);

    public static IReadOnlyList<TextChunk> Chunk(IReadOnlyList<TextBlock> blocks, ChunkingOptions options)
    {
        var units = blocks.SelectMany(b => Split(b, options)).ToList();
        var chunks = new List<TextChunk>();
        var current = new List<Unit>();
        var currentTokens = 0;
        var overlapCount = 0; // leading units copied from the previous chunk

        void Flush()
        {
            if (current.Count <= overlapCount) return; // nothing new besides overlap
            chunks.Add(Build(chunks.Count + 1, current));
            // Overlap: whole trailing units of the finished chunk, up to OverlapTokens.
            var tail = new List<Unit>();
            var tailTokens = 0;
            for (var i = current.Count - 1; i >= 0 && tailTokens + current[i].Tokens <= options.OverlapTokens; i--)
            {
                tail.Insert(0, current[i]);
                tailTokens += current[i].Tokens;
            }
            // Last paragraph alone is longer than the overlap: carry its closing sentences instead of nothing.
            if (tail.Count == 0 && current.Count > 0 && TailSentences(current[^1], options) is { } partial)
            {
                tail.Add(partial);
                tailTokens = partial.Tokens;
            }
            current = tail;
            currentTokens = tailTokens;
            overlapCount = tail.Count;
        }

        var pending = new LinkedList<Unit>(units);
        while (pending.First is { } node)
        {
            pending.RemoveFirst();
            var unit = node.Value;
            // A whole paragraph would overflow a chunk that is still too small: feed it sentence by sentence instead.
            if (current.Count > overlapCount && currentTokens < options.MinTokens && currentTokens + unit.Tokens > options.MaxTokens
                && Sentences().Split(unit.Text).Where(s => s.Length > 0).ToList() is { Count: > 1 } parts)
            {
                for (var i = parts.Count - 1; i >= 0; i--)
                    pending.AddFirst(new Unit(parts[i], EstimateTokens(parts[i], options), unit.Block));
                continue;
            }
            var sectionChanged = current.Count > overlapCount && current[^1].Block.Section != unit.Block.Section;
            if (current.Count > overlapCount && (currentTokens + unit.Tokens > options.MaxTokens
                || (currentTokens >= options.MinTokens && currentTokens + unit.Tokens > options.TargetTokens)
                || (sectionChanged && currentTokens >= options.MinTokens / 2)))
            {
                Flush();
                // A new section should not inherit text from the previous one.
                if (sectionChanged) { current.Clear(); currentTokens = 0; overlapCount = 0; }
            }
            current.Add(unit);
            currentTokens += unit.Tokens;
        }
        Flush();
        return chunks;
    }

    private static TextChunk Build(int ordinal, List<Unit> units)
    {
        var pages = units.Select(u => u.Block.Page).Where(p => p is not null).Select(p => p!.Value).ToList();
        return new TextChunk(ordinal, string.Join("\n\n", MergeSameBlock(units)), units.Sum(u => u.Tokens),
            pages.Count > 0 ? pages.Min() : null, pages.Count > 0 ? pages.Max() : null,
            units[^1].Block.Section, units.Min(u => u.Block.Paragraph), units.Max(u => u.Block.Paragraph));
    }

    // Sentences of one paragraph are rejoined with spaces; different paragraphs keep a blank line between them.
    private static IEnumerable<string> MergeSameBlock(List<Unit> units)
    {
        for (var i = 0; i < units.Count;)
        {
            var j = i;
            while (j + 1 < units.Count && ReferenceEquals(units[j + 1].Block, units[i].Block)) j++;
            yield return string.Join(" ", units.Skip(i).Take(j - i + 1).Select(u => u.Text));
            i = j + 1;
        }
    }

    private static Unit? TailSentences(Unit unit, ChunkingOptions options)
    {
        var sentences = Sentences().Split(unit.Text).Where(s => s.Length > 0).ToList();
        var taken = new List<string>();
        var tokens = 0;
        for (var i = sentences.Count - 1; i > 0; i--) // never the whole unit (i > 0)
        {
            var t = EstimateTokens(sentences[i], options);
            if (tokens + t > options.OverlapTokens) break;
            taken.Insert(0, sentences[i]);
            tokens += t;
        }
        return taken.Count == 0 ? null : new Unit(string.Join(" ", taken), tokens, unit.Block);
    }

    private static IEnumerable<Unit> Split(TextBlock block, ChunkingOptions options)
    {
        // Room is left for the overlap that may precede the unit, so overlap + unit never exceeds MaxTokens.
        var limit = Math.Max(1, options.MaxTokens - options.OverlapTokens);
        var tokens = EstimateTokens(block.Text, options);
        if (tokens <= limit) { yield return new Unit(block.Text, tokens, block); yield break; }
        foreach (var sentence in Sentences().Split(block.Text).Where(s => s.Length > 0))
        {
            var sentenceTokens = EstimateTokens(sentence, options);
            if (sentenceTokens <= limit) { yield return new Unit(sentence, sentenceTokens, block); continue; }
            // Very long sentence (tables, lists without punctuation): cut on word boundaries.
            var words = sentence.Split(' ');
            var part = new List<string>();
            foreach (var word in words)
            {
                part.Add(word);
                if (EstimateTokens(string.Join(' ', part), options) >= Math.Min(options.TargetTokens, limit))
                {
                    var text = string.Join(' ', part);
                    yield return new Unit(text, EstimateTokens(text, options), block);
                    part.Clear();
                }
            }
            if (part.Count > 0) { var rest = string.Join(' ', part); yield return new Unit(rest, EstimateTokens(rest, options), block); }
        }
    }

    [GeneratedRegex(@"(?<=[.!?…])\s+")] private static partial Regex Sentences();
}

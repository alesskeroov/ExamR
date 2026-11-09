using System.Text;
using ExamFlow.Api.Features.Learning;
using ExamFlow.Parsing;

namespace ExamFlow.Api.Tests;

/// <summary>
/// Regression set for study cards: every PDF/DOCX in <c>samples/learning/</c> (real course material, not in Git) is turned
/// into a deck the same way the API does, and compared with its reviewed snapshot <c>&lt;file&gt;.deck.txt</c>.
/// A missing snapshot is written; a changed deck fails and leaves <c>&lt;file&gt;.deck.actual.txt</c> to diff and review.
/// Accept reviewed changes with the environment variable DECK_SNAPSHOT_UPDATE=1.
/// </summary>
public class SampleDeckTests
{
    private static string? SamplesDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ExamFlow.sln")))
                return Path.Combine(dir.FullName, "samples", "learning") is var path && Directory.Exists(path) ? path : null;
        return null;
    }

    public static IEnumerable<object[]> Samples() =>
        SamplesDir() is { } dir
            ? Directory.GetFiles(dir).Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                .Select(f => new object[] { Path.GetFileName(f) })
            : [new object[] { "" }];

    [Theory]
    [MemberData(nameof(Samples))]
    public void Deck_matches_its_reviewed_snapshot(string file)
    {
        if (file.Length == 0 || SamplesDir() is not { } dir) return; // No samples on this machine.
        var deck = Build(Path.Combine(dir, file));

        // Rules every deck must keep, whatever the document.
        var topics = deck.Topics.Select(t => t.Key).ToHashSet();
        Assert.All(deck.Cards, c => Assert.Contains(c.TopicKey, topics));
        Assert.All(deck.Cards, c => Assert.False(string.IsNullOrWhiteSpace(c.Front)));
        Assert.All(deck.Cards, c => Assert.True(c.Back.Length > 0 || c.Items.Count > 0, $"Card without an answer: {c.Front}"));
        Assert.Equal(deck.Cards.Count, deck.Cards.Select(c => c.Id).Distinct().Count());

        var actual = Snapshot(deck);
        var expectedPath = Path.Combine(dir, file + ".deck.txt");
        var actualPath = Path.Combine(dir, file + ".deck.actual.txt");
        if (!File.Exists(expectedPath) || Environment.GetEnvironmentVariable("DECK_SNAPSHOT_UPDATE") == "1")
        {
            File.WriteAllText(expectedPath, actual);
            File.Delete(actualPath);
            return;
        }
        var expected = File.ReadAllText(expectedPath);
        if (expected == actual) { File.Delete(actualPath); return; }
        File.WriteAllText(actualPath, actual);
        Assert.Fail($"The deck of {file} changed. Compare {file}.deck.txt with {file}.deck.actual.txt; accept with DECK_SNAPSHOT_UPDATE=1.");
    }

    /// <summary>The API's pipeline without the database: extract, chunk, classify, then question, test or notes cards.</summary>
    public static StudyDeck Build(string path)
    {
        var options = new LearningOptions();
        var docx = path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);
        ExtractedText text;
        using (var stream = File.OpenRead(path)) text = LearningTextExtractor.Extract(stream, Path.GetExtension(path), options.Limits);
        var chunks = LearningChunker.Chunk(text.Blocks, options.Chunking).Select(c => new LearningDocumentChunk
        {
            Ordinal = c.Ordinal, Text = c.Text, TokenEstimate = c.Tokens, PageFrom = c.PageFrom, PageTo = c.PageTo,
            Section = c.Section is { Length: > 200 } s ? s[..200] : c.Section, ParagraphFrom = c.ParagraphFrom, ParagraphTo = c.ParagraphTo,
        }).ToList();

        var pdfParser = new PdfQuestionParser();
        var docxParser = new DocxQuestionParser(pdfParser);
        ParseResult Parse() { using var stream = File.OpenRead(path); return docx ? docxParser.Parse(stream) : pdfParser.Parse(stream); }
        IReadOnlyList<ParsedQuestionInfo>? parsed;
        using (var stream = File.OpenRead(path))
            parsed = docx ? OpenQuestionClassifier.InspectDocx(docxParser, stream, default) : OpenQuestionClassifier.Inspect(pdfParser, stream, default);
        var index = OpenQuestionClassifier.Classify(chunks, parsed);

        if (index.Kind == OpenDocumentKind.MultipleChoice) return CardDeckService.TestDeck(Parse(), string.Join("\n", chunks.Select(c => c.Text)));
        if (CardDeckService.QuestionAnswer(index) is { } questions) return CardDeckBuilder.FromOpenQuestions(questions);
        return NotesCardBuilder.Build(CardDeckService.Paragraphs(text, options.Chunking));
    }

    private static string Snapshot(StudyDeck deck)
    {
        static string Short(string text) { text = text.Replace('\n', ' '); return text.Length > 160 ? text[..160] + "…" : text; }
        var sb = new StringBuilder();
        sb.AppendLine($"{deck.Topics.Count} topics, {deck.Cards.Count} cards");
        foreach (var topic in deck.Topics)
        {
            var cards = deck.Cards.Where(c => c.TopicKey == topic.Key).ToList();
            sb.AppendLine().AppendLine($"## {topic.Title} ({cards.Count})");
            foreach (var c in cards)
            {
                sb.Append($"- {c.Kind}{(c.NeedsReview ? " [yoxlanmalı]" : "")}{(c.Section is { } s ? $" <{s}>" : "")}: {Short(c.Front)}");
                if (c.Back.Length > 0) sb.Append($" => {Short(c.Back)}");
                if (c.Items.Count > 0) sb.Append($" | {Short(string.Join("; ", c.Items))}");
                sb.AppendLine();
            }
        }
        return sb.ToString().Replace("\r\n", "\n");
    }
}

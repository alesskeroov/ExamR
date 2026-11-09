using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

/// <summary>The student's own verdict after seeing the back of a card: "Bilmirdim", "Qismən", "Bilirdim".</summary>
public enum CardGrade { Again, Hard, Good }

/// <summary>
/// Learning state of one card for one student. No AI and no dates: a card is mastered after
/// <see cref="CardMastery.Required"/> "Bilirdim" in a row. QuestionHash is the card id (<see cref="StudyCardItem.Id"/>),
/// derived from the card's text, so renumbering the document keeps the progress.
/// </summary>
public sealed class LearningCardState
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    public string QuestionHash { get; set; } = "";
    /// <summary>"Bilirdim" answers in a row; any other grade resets it.</summary>
    public int Streak { get; set; }
    public bool Mastered { get; set; }
    public int Reviews { get; set; }
    /// <summary>How often the student answered "Bilmirdim".</summary>
    public int Lapses { get; set; }
    public CardGrade LastGrade { get; set; }
    public DateTimeOffset LastReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LearningCardStateConfiguration : IEntityTypeConfiguration<LearningCardState>
{
    public void Configure(EntityTypeBuilder<LearningCardState> builder)
    {
        builder.ToTable("LearningCardStates");
        builder.Property(c => c.QuestionHash).HasMaxLength(64);
        builder.Property(c => c.LastGrade).HasConversion<string>().HasMaxLength(8);
        builder.HasIndex(c => new { c.UserId, c.LearningDocumentId, c.QuestionHash }).IsUnique();
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(c => c.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public static class CardMastery
{
    /// <summary>"Bilirdim" answers in a row that make a card mastered.</summary>
    public const int Required = 2;

    /// <summary>Id of an open-question card; unchanged from the first card version so earlier progress is kept.</summary>
    public static string Hash(string questionText) => Sha(QuizLogic.Key(questionText).Trim());

    public static string Hash(CardKind kind, string front) => Sha($"{kind}\u001f{front.Trim()}");

    public static (int Streak, bool Mastered) Next(int streak, CardGrade grade) =>
        grade == CardGrade.Good ? (streak + 1, streak + 1 >= Required) : (0, false);

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>Question = open question and its answer; Term = "X — definition" inside an answer; List = "…:" and its items; Test = test question and its correct option;
/// Table = one row (or a two-column comparison) of a table; Custom = a card the student wrote.</summary>
public enum CardKind { Question, Term, List, Test, Table, Custom }

/// <summary>
/// One flashcard. Front/Back are the two sides; Items = list items (List) or the terms explained in the answer (Question);
/// Others = the wrong options of a test question; Source = the document question the card comes from;
/// Section = the subsection heading the card was taken from, when it is narrower than the topic.
/// </summary>
public sealed record StudyCardItem(string Id, CardKind Kind, string TopicKey, string Front, string Back, IReadOnlyList<string> Items,
    IReadOnlyList<string> Others, int Source, int? Page, int ChunkOrdinal, bool IsSection = false, string? Section = null, bool NeedsReview = false);

public sealed record CardTopic(string Key, string Title, string? Notes = null);

public sealed record StudyDeck(IReadOnlyList<CardTopic> Topics, IReadOnlyList<StudyCardItem> Cards);

/// <summary>A test question as the exam parser saw it (only the parts a card needs).</summary>
public sealed record TestQuestion(int Number, string Text, string Correct, IReadOnlyList<string> Others, string? Explanation, int? Page);

/// <summary>
/// Builds flashcards from a document without AI. Open-question documents give one card per question plus a card per
/// definition and per list found in the answers; test documents give one card per question with its marked answer.
/// </summary>
public static partial class CardDeckBuilder
{
    public const string Version = "cards6";
    public const int MaxCards = 2000;
    private const int MaxTermWords = 6;
    private const int MaxTermLength = 70;
    private const int MinDefinitionLength = 25;
    private const int MaxListItemLength = 260;
    private const int MaxListItems = 12;
    private const int TestBlock = 20;

    // ---------- Open questions ----------

    /// <summary>Every exam question is a topic (chunk headings proved unreliable); its answer, terms and lists are its cards.</summary>
    public static StudyDeck FromOpenQuestions(IReadOnlyList<IndexedQuestion> questions)
    {
        var topics = new List<CardTopic>();
        var topicOf = new Dictionary<int, string>();
        foreach (var q in questions)
        {
            topics.Add(new(Key($"{q.Number}|{q.Text}"), Title(q.Text)));
            topicOf[q.Number] = topics[^1].Key;
        }
        var cards = new List<StudyCardItem>();
        var seen = new HashSet<string>();
        void Add(StudyCardItem card) { if (cards.Count < MaxCards && seen.Add(card.Id)) cards.Add(NotesCardBuilder.Review(card)); }

        foreach (var q in questions)
        {
            // The answer goes through the same cleaning as whole documents: captions, citations, code and table debris
            // leave; table rows become table cards instead of loose words in the answer.
            var cleaned = DocumentCleaner.Clean(q.Answer.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(l => new DocParagraph(l, q.Page, q.ChunkOrdinal)).ToList(), dropTail: false);
            var lines = cleaned.Where(p => !p.IsTableRow).Select(p => p.Text).ToList();
            if (lines.Count == 0) continue;
            var terms = new List<(string Term, string Definition)>();
            foreach (var line in lines)
                if (Definition(line) is { } d) terms.Add(d);

            Add(new(CardMastery.Hash(q.Text), CardKind.Question, topicOf[q.Number], q.Text, string.Join('\n', lines),
                terms.Select(t => t.Term).Distinct().Take(10).ToList(), [], q.Number, q.Page, q.ChunkOrdinal));
            foreach (var card in NotesCardBuilder.TableCards(cleaned, topicOf[q.Number], q.Number)) Add(card);
            foreach (var (term, definition) in terms)
                Add(new(CardMastery.Hash(CardKind.Term, term), CardKind.Term, topicOf[q.Number], $"{term} nədir?", definition, [], [], q.Number, q.Page, q.ChunkOrdinal));
            foreach (var (title, items) in Lists(lines))
                Add(new(CardMastery.Hash(CardKind.List, title), CardKind.List, topicOf[q.Number], $"{title}: hansılardır?", "", items, [], q.Number, q.Page, q.ChunkOrdinal));
        }
        return new(topics, cards);
    }

    /// <summary>"Term — definition" (also "–" or " - "): a short noun phrase before the dash and a real sentence after it.</summary>
    public static (string Term, string Definition)? Definition(string line)
    {
        var m = DefinitionLine().Match(line);
        if (!m.Success) return null;
        var term = m.Groups["term"].Value.Trim().TrimEnd('.', ',');
        var definition = m.Groups["def"].Value.Trim();
        if (term.Length < 2 || term.Length > MaxTermLength || definition.Length < MinDefinitionLength) return null;
        // ", burada …" (a formula the text extraction lost) and "Bəli, amma …" (an answer) are not definitions.
        if (!char.IsLetterOrDigit(definition[0]) || NotDefinitionStart().IsMatch(definition)) return null;
        if (term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MaxTermWords) return null;
        // "Beləliklə – …", "Məsələn – …" are connectives, not terms.
        if (Connectives.Contains(term.ToLowerInvariant())) return null;
        // "Brauzer DNS sorğusu göndərir – …" is a step of a process (a verb ends it), not a term; it stays in its list card.
        if (term.Contains(' ') && VerbEnding().IsMatch(term)) return null;
        // "RRS - lərin əsas növləri…": the hyphen joins an abbreviation and its case ending; it is not a definition dash.
        if (m.Groups["dash"].Value.Trim() == "-" && SuffixAfterHyphen().IsMatch(definition)) return null;
        // "RRX - nın …", "API - lər …": an abbreviation, a hyphen and a short lowercase ending.
        if (m.Groups["dash"].Value.Trim() == "-" && AbbreviationEnd().IsMatch(term) && ShortLowerWord().IsMatch(definition)) return null;
        if (NotesCardBuilder.CleanTerm(term) is null || !NotesCardBuilder.ReliableDefinition(definition)) return null;
        return (term, char.ToUpperInvariant(definition[0]) + definition[1..]);
    }

    /// <summary>A lead-in line ending with ":" followed by at least two short lines: "…aşağıdakılardır:" / item / item.</summary>
    public static IEnumerable<(string Title, IReadOnlyList<string> Items)> Lists(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var lead = lines[i].TrimEnd();
            if (!lead.EndsWith(':') || lead.Length < 12 || lead.Length > 180 || !ListCue().IsMatch(lead)) continue;
            var items = new List<string>();
            var j = i + 1;
            ItemStyle? style = null;
            for (; j < lines.Count && items.Count < MaxListItems; j++)
            {
                var line = lines[j];
                if (!ItemMark().IsMatch(line) && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 5) break;
                if (line.Length > MaxListItemLength || line.TrimEnd().EndsWith(':') || line.TrimEnd().EndsWith('?')) break;
                // A closing sentence ("Beləliklə, …", "Bu texnologiyalar …") ends the list.
                var first = line.Split(' ', 2)[0].Trim(',', '.').ToLowerInvariant();
                if (Closers.Contains(first)) break;
                var lineStyle = Style(line);
                // Items of one list look alike: "Term – …" / "Name: …" items end at the first line written differently.
                if (style is ItemStyle.Term or ItemStyle.Labelled && lineStyle != style) break;
                if (style is ItemStyle.Plain && line.Length > 160) break;
                style ??= lineStyle;
                // A definition item is listed by its term; its explanation has its own card.
                items.Add(Definition(line)?.Term ?? (lineStyle == ItemStyle.Term ? DefinitionLine().Match(line).Groups["term"].Value.Trim() : ItemMark().Replace(line, "").Trim()));
            }
            if (items.Count >= 2 && items.Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count
                && (j == lines.Count || items.Count < MaxListItems)
                && items.All(item => !Regex.IsMatch(item, @"\bvə s\.|\betc\.", RegexOptions.IgnoreCase) && !DocumentCleaner.IsNoise(item) && !DocumentCleaner.IsCode(item)))
                yield return (lead.TrimEnd(':', ' '), items.Select(item => item.TrimEnd(';', '.')).ToList());
            i = Math.Max(i, j - 1);
        }
    }

    // ---------- Test questions ----------

    /// <param name="text">The document text in reading order, used only to find "Mövzu 1. …" style headings.</param>
    public static StudyDeck FromTests(IReadOnlyList<TestQuestion> tests, string text)
    {
        var headings = TestHeadings(text);
        var topics = new List<CardTopic>();
        var cards = new List<StudyCardItem>();
        var seen = new HashSet<string>();
        foreach (var t in tests.OrderBy(t => t.Number))
        {
            string title;
            if (headings.Count > 0)
                title = headings.LastOrDefault(h => h.FirstNumber <= t.Number).Title ?? headings[0].Title;
            else
            {
                var start = (t.Number - 1) / TestBlock * TestBlock + 1;
                title = $"Testlər {start}–{start + TestBlock - 1}";
            }
            if (topics.Count == 0 || topics[^1].Title != title) topics.Add(new(Key(title + "|" + topics.Count), title));
            var id = CardMastery.Hash(CardKind.Test, t.Text);
            if (cards.Count >= MaxCards || !seen.Add(id)) continue;
            cards.Add(new(id, CardKind.Test, topics[^1].Key, t.Text, t.Correct, t.Explanation is { Length: > 0 } e ? [e] : [], t.Others, t.Number, t.Page, 0));
        }
        return new(topics, cards);
    }

    /// <summary>"Mövzu 2. Bulud hesablama xidmət modelləri 46. …" → topic "Mövzu 2. Bulud hesablama xidmət modelləri" from question 46 on.</summary>
    public static List<(int FirstNumber, string Title)> TestHeadings(string text)
    {
        var list = new List<(int, string)>();
        foreach (Match m in TestHeading().Matches(text))
        {
            var title = Regex.Replace(m.Groups["title"].Value, @"\s+", " ").Trim().TrimEnd('.', ':');
            if (title.Length is < 3 or > 120) continue;
            var label = $"{m.Groups["word"].Value} {m.Groups["n"].Value}. {title}";
            if (int.TryParse(m.Groups["first"].Value, out var first) && (list.Count == 0 || first > list[^1].Item1)) list.Add((first, label));
        }
        return list;
    }

    // ---------- helpers ----------

    private enum ItemStyle { Term, Labelled, Plain }

    private static ItemStyle Style(string line) =>
        DefinitionLine().IsMatch(line) ? ItemStyle.Term : LabelledLine().IsMatch(line) ? ItemStyle.Labelled : ItemStyle.Plain;

    private static readonly HashSet<string> Closers = ["beləliklə", "nəticədə", "yəni", "ümumiyyətlə", "deməli", "qısaca", "lakin", "amma", "bu", "bunlar", "həmçinin"];

    private static readonly HashSet<string> Connectives = ["beləliklə", "məsələn", "nəticədə", "yəni", "qısası", "ümumiyyətlə", "deməli", "bundan əlavə", "həmçinin"];

    /// <summary>A question as a topic name: the first sentence, cut at a word boundary.</summary>
    public static string Title(string question)
    {
        var text = question.Trim();
        var end = text.IndexOfAny(['.', '?', '!']);
        if (end > 15) text = text[..(end + (text[end] == '?' ? 1 : 0))];
        if (text.Length <= 90) return text;
        var cut = text.LastIndexOf(' ', 87);
        return text[..(cut > 40 ? cut : 87)].TrimEnd(',', ';', ' ') + "…";
    }

    private static string Key(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

    [GeneratedRegex(@"(?:aşağıdakı|növlər|komponent|mərhələ|hissələr|elementlər|üstünlüklər|çatışmazlıqlar|daxildir|ibarətdir)", RegexOptions.IgnoreCase)]
    private static partial Regex ListCue();

    [GeneratedRegex(@"^\s*(?:[-•▪●◦*]\s*|\d{1,2}[.)]\s*)?(?<term>[^—–:\n]{2,80}?)\s*(?<dash>[—–]|\s-\s)\s*(?<def>\S.+)$")]
    private static partial Regex DefinitionLine();

    [GeneratedRegex(@"^(?:l[əa]r|n[ıiuü]n|[ıiuü]n|n[ıiuü]|y[ıiuü]|d[əa]n?|t[əa]n?|n[əa]|y[əa]|s[ıiuü]|[ıiuü]|m[ıiuü]z|l[ıiuü])\b")]
    private static partial Regex SuffixAfterHyphen();

    [GeneratedRegex(@"(?:^|\s)[\p{Lu}\d]{2,}\d*$")] private static partial Regex AbbreviationEnd();
    [GeneratedRegex(@"^\p{Ll}{1,6}(?:\s|[,.;:]|$)")] private static partial Regex ShortLowerWord();

    [GeneratedRegex(@"^\s*[^:]{2,60}:\s+\S")]
    private static partial Regex LabelledLine();

    [GeneratedRegex(@"(?:ir|ır|ur|ür|(?<!l)ar|(?<!l)ər)$")]
    private static partial Regex VerbEnding();

    [GeneratedRegex(@"^(?:bəli|xeyr|yox|hə|yes|no|да|нет)(?![\p{L}\d])", RegexOptions.IgnoreCase)]
    private static partial Regex NotDefinitionStart();

    [GeneratedRegex(@"^\s*(?:[-•▪●◦*]|\d{1,2}[.)]|[a-zA-Z][.)])\s*")]
    private static partial Regex ItemMark();

    [GeneratedRegex(@"(?<word>Mövzu|MÖVZU|Fəsil|FƏSİL|Bölmə|BÖLMƏ|Mühazirə|MÜHAZİRƏ|Тема|ТЕМА|Глава|Chapter|Topic)\s*(?<n>\d{1,3})\s*[.:)\-–]\s*(?<title>[^\n]{3,160}?)\s+(?<first>\d{1,4})\s*[.)]\s", RegexOptions.CultureInvariant)]
    private static partial Regex TestHeading();
}

/// <summary>Builds and caches the deck of one document version. Test documents are parsed with the exam parser once.</summary>
public sealed class CardDeckService(AppDbContext db, OpenQuestionIndexer indexer, Infrastructure.Storage.IFileStorage storage, PdfQuestionParser parser,
    DocxQuestionParser docxParser, IMemoryCache cache, IOptions<LearningOptions> options, ILogger<CardDeckService> logger)
{
    private static readonly TimeSpan ParseTimeout = TimeSpan.FromSeconds(180);
    /// <summary>From this many open questions on, a document is a question-and-answer file.</summary>
    private const int QaQuestions = 10;

    /// <summary>Deck, or null with the reason the document gives no cards.</summary>
    public async Task<(StudyDeck? Deck, string? Error)> GetAsync(Guid learningId, CancellationToken ct)
    {
        var learning = await db.Set<LearningDocument>().AsNoTracking().Where(l => l.Id == learningId && l.Status == LearningStatus.Completed)
            .Select(l => new { l.Format, l.ProcessedAt, l.Document.StoredFileName }).FirstOrDefaultAsync(ct);
        if (learning is null) return (null, "Sənəd hələ hazır deyil.");
        var key = $"{CardDeckBuilder.Version}:{learningId}:{learning.ProcessedAt?.UtcTicks}";
        if (cache.TryGetValue(key, out StudyDeck? hit) && hit is not null) return (hit, null);

        var index = await indexer.GetAsync(learningId, ct);
        if (index is null) return (null, "Sənəd hələ hazır deyil.");
        if (index.Kind == OpenDocumentKind.NotPdf) return (null, index.Rejection);
        StudyDeck? deck;
        if (index.Kind == OpenDocumentKind.MultipleChoice)
        {
            deck = await TestsAsync(learningId, learning.StoredFileName, learning.Format, ct);
            if (deck is null) return (null, "Test sualları oxuna bilmədi.");
        }
        else
        {
            // Question-and-answer files give question cards. Lecture notes (and files whose few "questions" are really
            // section headings) give section, term, list and table cards; with few questions the richer deck wins.
            // Only explicit source questions use the Q&A path. Never choose a deck because it contains more cards.
            if (QuestionAnswer(index) is { } questions)
                deck = CardDeckBuilder.FromOpenQuestions(questions);
            else
                deck = NotesCardBuilder.Build(await ParagraphsAsync(learningId, learning.StoredFileName, learning.Format, ct));
        }
        if (deck.Cards.Count == 0 && !deck.Topics.Any(t => !string.IsNullOrWhiteSpace(t.Notes)))
            return (null, "Bu sənəddən etibarlı kart və ya oxunaqlı konspekt hazırlamaq mümkün olmadı.");

        cache.Set(key, deck, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
        return (deck, null);
    }

    /// <summary>The questions of a question-and-answer file, or null when the document is read as lecture notes.</summary>
    public static List<IndexedQuestion>? QuestionAnswer(OpenQuestionIndex index)
    {
        if (!index.Supported) return null;
        var questions = index.Questions.Where(q => NotesCardBuilder.ExplicitQuestion(q.Text)).ToList();
        if (questions.Count >= QaQuestions && questions.Count >= index.Questions.Count * 0.8) return questions;
        // Exam topic lists ("1. Süni intellekt, tərifləri və əsas məqsədləri.") ask without question words. They are
        // numbered 1, 2, 3 … with a real answer each, unlike section numbers ("1 Annotasiya", "3.1 …") of lecture notes.
        var all = index.Questions;
        var numbers = all.Select(q => q.Number).Distinct().Order().ToList();
        if (all.Count < QaQuestions || numbers[0] > 2) return null;
        var steps = numbers.Zip(numbers.Skip(1)).Count(p => p.Second == p.First + 1);
        return steps >= (numbers.Count - 1) * 0.9 && all.Average(q => q.Answer.Length) >= MinTopicAnswer ? all.ToList() : null;
    }

    /// <summary>Average answer length (characters) that makes a numbered list of statements a list of exam questions.</summary>
    private const int MinTopicAnswer = 150;

    /// <summary>Test cards from the exam parser's result; <paramref name="text"/> is the document text, read only for topic headings.</summary>
    public static StudyDeck TestDeck(ParseResult result, string text)
    {
        var tests = result.Questions.Where(q => q.CorrectIndex is int i && i >= 0 && i < q.Options.Count)
            .Select(q => new TestQuestion(q.Number, q.Text, q.Options[q.CorrectIndex!.Value].Text,
                q.Options.Where((_, i) => i != q.CorrectIndex).Select(o => o.Text).ToList(), q.Explanation, q.Anchor?.PageNumber))
            .ToList();
        return CardDeckBuilder.FromTests(tests, text);
    }

    /// <summary>The document's paragraphs in reading order, each with the chunk it was stored in.</summary>
    public static IReadOnlyList<DocParagraph> Paragraphs(ExtractedText text, ChunkingOptions chunking)
    {
        var ordinals = new Dictionary<int, int>();
        foreach (var chunk in LearningChunker.Chunk(text.Blocks, chunking))
            for (var paragraph = chunk.ParagraphFrom; paragraph <= chunk.ParagraphTo; paragraph++)
                ordinals.TryAdd(paragraph, chunk.Ordinal);
        return text.Blocks.Select(b => new DocParagraph(b.Text, b.Page, ordinals.GetValueOrDefault(b.Paragraph)) { HeadingLevel = b.HeadingLevel }).ToList();
    }

    private async Task<StudyDeck?> TestsAsync(Guid learningId, string storedFileName, string format, CancellationToken ct)
    {
        try
        {
            var docx = string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase);
            var result = await ReadFileAsync(storedFileName,
                (file, token) => docx ? docxParser.Parse(file, token) : parser.Parse(file, token), ct);
            var chunks = await db.Set<LearningDocumentChunk>().AsNoTracking().Where(c => c.LearningDocumentId == learningId)
                .OrderBy(c => c.Ordinal).Select(c => c.Text).ToListAsync(ct);
            return TestDeck(result, string.Join("\n", chunks));
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Test cards for learning document {LearningId} could not be built", learningId);
            return null;
        }
    }

    private async Task<IReadOnlyList<DocParagraph>> ParagraphsAsync(Guid learningId, string storedFileName, string format, CancellationToken ct)
    {
        try
        {
            return await ReadFileAsync(storedFileName, (file, token) =>
            {
                var text = LearningTextExtractor.Extract(file, "." + format.TrimStart('.'), options.Value.Limits, token);
                if (text.Failure != TextExtractionFailure.None || text.Blocks.Count == 0)
                    throw new InvalidDataException("Document text extraction failed.");
                return Paragraphs(text, options.Value.Chunking);
            }, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Using stored chunks for learning document {LearningId} after file extraction failed", learningId);
            var chunks = await db.Set<LearningDocumentChunk>().AsNoTracking().Where(c => c.LearningDocumentId == learningId)
                .OrderBy(c => c.Ordinal).ToListAsync(ct);
            return DocumentCleaner.Paragraphs(chunks);
        }
    }

    private async Task<T> ReadFileAsync<T>(string storedFileName, Func<Stream, CancellationToken, T> read, CancellationToken ct)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ParseTimeout);
            await using (var source = storage.OpenRead(storedFileName))
            await using (var target = File.Create(tempPath))
                await source.CopyToAsync(target, timeout.Token);
            await using var file = File.OpenRead(tempPath);
            return await Task.Run(() => read(file, timeout.Token), timeout.Token);
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
        }
    }
}

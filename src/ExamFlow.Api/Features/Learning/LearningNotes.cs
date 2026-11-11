using System.Text.RegularExpressions;
using ExamFlow.Parsing;

namespace ExamFlow.Api.Features.Learning;

/// <summary>One paragraph of a document in reading order. Table rows keep their <see cref="LearningTextExtractor.TableRowMark"/>.</summary>
public sealed record DocParagraph(string Text, int? Page, int ChunkOrdinal)
{
    /// <summary>On the first row of a table: its caption ("Cədvəl 3. …"), when the document gives one.</summary>
    public string? Caption { get; init; }
    /// <summary>Word heading level from the paragraph's style (1 = top, 0 = document title); null for body text and PDF.</summary>
    public int? HeadingLevel { get; init; }
    public bool IsTableStart => Text.StartsWith(LearningTextExtractor.TableStartMark, StringComparison.Ordinal);
    public bool IsTableRow => IsTableStart || Text.StartsWith(LearningTextExtractor.TableRowMark, StringComparison.Ordinal);
    public string TableText => Text[(IsTableStart ? LearningTextExtractor.TableStartMark.Length : LearningTextExtractor.TableRowMark.Length)..];
}

/// <summary>
/// Removes what makes cards meaningless, for any PDF or Word file: table of contents, title-page details, figure and table
/// captions, citation markers, page headers, references and end-of-chapter question lists, code, symbol noise and the
/// one-word fragments a flattened PDF table leaves behind. Word tables arrive as marked rows and are kept for table cards.
/// </summary>
public static partial class DocumentCleaner
{
    /// <summary>Paragraphs of the stored chunks in reading order, without the overlap consecutive chunks share.</summary>
    public static List<DocParagraph> Paragraphs(IEnumerable<LearningDocumentChunk> chunks)
    {
        var list = new List<DocParagraph>();
        foreach (var chunk in chunks.OrderBy(c => c.Ordinal))
        {
            var previousTail = list.TakeLast(8).ToList();
            var inOverlap = true;
            foreach (var part in chunk.Text.Split("\n\n"))
            {
                var text = part.Trim();
                if (text.Length == 0) continue;
                if (inOverlap && previousTail.Any(p => p.Text == text || (text.Length > 30 && p.Text.Contains(text, StringComparison.Ordinal)))) continue;
                inOverlap = false;
                list.Add(new DocParagraph(text, chunk.PageFrom, chunk.Ordinal));
            }
        }
        return list;
    }

    /// <param name="dropTail">Cut references and end-of-chapter questions (whole documents; not one answer of a Q&amp;A file).</param>
    public static List<DocParagraph> Clean(IReadOnlyList<DocParagraph> input, bool dropTail = true)
    {
        var pages = Repeated(input);
        var tailFrom = dropTail ? TailStart(input) : input.Count;
        var output = new List<DocParagraph>();
        string? pendingCaption = null;
        for (var i = 0; i < input.Count && i < tailFrom; i++)
        {
            var p = input[i];
            if (p.IsTableRow)
            {
                var cells = p.TableText.Split('|').Select(c => Tidy(c)).ToList();
                if (cells.All(c => c.Length == 0)) continue;
                var first = p.IsTableStart || output.Count == 0 || !output[^1].IsTableRow;
                output.Add(p with { Text = (p.IsTableStart ? LearningTextExtractor.TableStartMark : LearningTextExtractor.TableRowMark) + string.Join(" | ", cells), Caption = first ? pendingCaption : null });
                if (first) pendingCaption = null;
                continue;
            }

            var text = Tidy(p.Text);
            if (text.Length == 0 || pages.Contains(text)) continue;
            if (Caption().Match(text) is { Success: true } caption)
            {
                // "Cədvəl 3. …" names the table that follows; figure captions describe a picture the card cannot show.
                if (caption.Groups["kind"].Value.StartsWith("C", StringComparison.OrdinalIgnoreCase) || caption.Groups["kind"].Value.StartsWith("T", StringComparison.OrdinalIgnoreCase)
                    || caption.Groups["kind"].Value.StartsWith("Т", StringComparison.Ordinal))
                    pendingCaption = Tidy(text[caption.Length..]).TrimEnd('.');
                continue;
            }
            if (IsMetadata(text) || IsNoise(text) || IsCode(text) || IsTocLine(input, i)) continue;
            output.Add(p with { Text = text });
        }
        return DropFragmentRuns(output);
    }

    /// <summary>Citation markers, page labels and doubled spaces removed.</summary>
    public static string Tidy(string text)
    {
        text = Citation().Replace(text, "");
        text = PageLabel().Replace(text, " ");
        return Spaces().Replace(text, " ").Trim();
    }

    /// <summary>Text the reader cannot learn from: few letters (formulas, diagram labels), lone numbers, broken characters.</summary>
    public static bool IsNoise(string text)
    {
        var visible = text.Count(c => !char.IsWhiteSpace(c));
        if (visible == 0) return true;
        var letters = text.Count(char.IsLetter);
        if (letters < 2 || letters < visible * 0.45) return true;
        if (text.Count(c => c == '�') > 2) return true;
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= 5 && tokens.Average(t => t.Length) < 2.3;
    }

    /// <summary>Source code, commands and configuration lines.</summary>
    public static bool IsCode(string text)
    {
        if (CodeShape().IsMatch(text) || MarkupLine().IsMatch(text)) return true;
        var symbols = text.Count(c => "{}[]=;<>_\\$`|".Contains(c));
        return text.Length >= 8 && symbols >= Math.Max(3, text.Length * 0.08);
    }

    // Standalone HTML tags are code even when too short for the symbol-density heuristic.
    // Prose such as "<head> teqi metadatanı saxlayır" must remain an explanation.
    [GeneratedRegex(@"^\s*</?[a-zA-Z][\w:-]*(?:\s+[^<>]*)?\s*/?>\s*$")]
    private static partial Regex MarkupLine();

    /// <summary>Title-page lines: teacher, department, e-mail, links, check dates.</summary>
    public static bool IsMetadata(string text) =>
        text.Length < 220 && (Url().IsMatch(text) || Metadata().IsMatch(text) || (text.Length < 90 && text.Contains("universitet", StringComparison.OrdinalIgnoreCase)));

    /// <summary>"Annotasiya 2", "3.1 Marşrutlaşdırma ........ 7": a line ending in a page number among other such lines.</summary>
    private static bool IsTocLine(IReadOnlyList<DocParagraph> all, int index)
    {
        static bool Toc(string t) => t.Length <= 140 && TocLine().IsMatch(t);
        if (!Toc(all[index].Text)) return false;
        var near = 0;
        for (var j = Math.Max(0, index - 3); j <= Math.Min(all.Count - 1, index + 3); j++)
            if (j != index && Toc(all[j].Text)) near++;
        return near >= 2;
    }

    /// <summary>Short lines that repeat on three or more pages: running headers and footers.</summary>
    private static HashSet<string> Repeated(IReadOnlyList<DocParagraph> input) =>
        input.Where(p => !p.IsTableRow && p.Text.Length is > 0 and < 120 && p.Page is not null)
            .GroupBy(p => p.Text).Where(g => g.Select(p => p.Page).Distinct().Count() >= 3).Select(g => g.Key).ToHashSet();

    /// <summary>Index where references / literature / end-of-chapter questions start (only in the second half), or the end.</summary>
    private static int TailStart(IReadOnlyList<DocParagraph> input)
    {
        for (var i = input.Count / 2; i < input.Count; i++)
            if (input[i].Text.Length < 60 && TailHeading().IsMatch(input[i].Text.Trim().TrimEnd(':', '.')))
                return i;
        return input.Count;
    }

    /// <summary>Five or more one-to-four-word fragments in a row (not after a "…:" lead): the cells of a table a PDF flattened.</summary>
    private static List<DocParagraph> DropFragmentRuns(List<DocParagraph> list)
    {
        static bool Fragment(DocParagraph p) => !p.IsTableRow && p.HeadingLevel is null && p.Text.Length <= 45 && p.Text.Split(' ').Length <= 4
            && !ListMark().IsMatch(p.Text) && !".:;?!".Contains(p.Text[^1]);
        var keep = new bool[list.Count];
        Array.Fill(keep, true);
        for (var i = 0; i < list.Count;)
        {
            if (!Fragment(list[i])) { i++; continue; }
            var j = i;
            while (j < list.Count && Fragment(list[j])) j++;
            var afterLead = i > 0 && list[i - 1].Text.EndsWith(':');
            if (j - i >= 5 && !afterLead) for (var k = i; k < j; k++) keep[k] = false;
            i = j;
        }
        return list.Where((_, i) => keep[i]).ToList();
    }

    [GeneratedRegex(@"\s?\[\d{1,3}(?:\s*[,–-]\s*\d{1,3})*\]")] private static partial Regex Citation();
    [GeneratedRegex(@"(?:Səhifə|Səh\.|Page|Стр\.|Страница)\s*\d{1,4}(?:\s*(?:/|of|из)\s*\d{1,4})?", RegexOptions.IgnoreCase)] private static partial Regex PageLabel();
    [GeneratedRegex(@"[  \t]{2,}")] private static partial Regex Spaces();
    [GeneratedRegex(@"^(?<kind>Şəkil|Şək\.|Cədvəl|Diaqram|Sxem|Rəsm|Qrafik|Figure|Fig\.|Table|Рис\.|Рисунок|Таблица|Схема)\s*\d{1,3}(?:\.\d{1,3})?\s*[.:)–-]?\s*", RegexOptions.IgnoreCase)] private static partial Regex Caption();
    [GeneratedRegex(@"https?://|www\.|[\w.+-]+@[\w-]+\.[\w.]+", RegexOptions.IgnoreCase)] private static partial Regex Url();
    [GeneratedRegex(@"^(?:müəllim|kafedra|fakültə|fənn|tələbə|qrup|hazırladı|hazırlayan|mühazirəçi|yoxlama tarixi|prof\.|dos\.|baş müəllim)\b|kafedrası", RegexOptions.IgnoreCase)] private static partial Regex Metadata();
    [GeneratedRegex(@"^[\p{L}\d][^\n]{1,120}?(?:\s|\.{2,}|…)\s*\d{1,3}$")] private static partial Regex TocLine();
    [GeneratedRegex(@"^(?:ədəbiyyat(?: siyahısı)?|istifadə olunmuş ədəbiyyat|istinadlar|mənbələr|əlavə oxu|references|bibliography|литература|список литературы|источники|suallar|nəzarət sualları|özünü yoxlama sualları|yoxlama sualları|test sualları|tapşırıqlar|ev tapşırığı)$", RegexOptions.IgnoreCase)] private static partial Regex TailHeading();
    [GeneratedRegex(@"^\s*(?:[-•▪●◦*]|\d{1,2}[.)])\s")] private static partial Regex ListMark();
    [GeneratedRegex(@"^\s*(?:[.\[~/$#>]|export\s|ixrac\s|sudo\s|npm\s|pip\s|git\s|docker\s|kubectl\s|aws\s)|[{}]\s*$|\);\s*$|\w\.\w+\(.*\)|^\w+\s*=\s*\S+$|=>|::", RegexOptions.IgnoreCase)] private static partial Regex CodeShape();
}

/// <summary>Cards from lecture notes and other documents without numbered questions: sections, terms, lists and tables.</summary>
public static partial class NotesCardBuilder
{
    private const int TopicBlock = 8;
    /// <summary>From this many Word heading-styled paragraphs on, the styles alone decide the headings.</summary>
    private const int MinStyledHeadings = 3;

    /// <param name="Major">"3" of a "3.2 …" heading: the numbered chapter the section belongs to, when the document numbers them.</param>
    private sealed record Section(string Heading, int Level, List<DocParagraph> Body, int Index, int? Major = null);

    public static StudyDeck Build(IReadOnlyList<DocParagraph> raw)
    {
        var paragraphs = DocumentCleaner.Clean(raw);
        var plan = PlanHeadings(paragraphs, out var planRange);
        var sections = Sections(paragraphs, plan, planRange);
        // A heading typed in capitals loses acronyms and "ı" when lowered; the body text shows how its words are really written.
        var spellings = Spellings(paragraphs);
        sections = sections.Select(s => s.Heading.Length > 0 && Shouting(paragraphs[s.Index].Text) ? s with { Heading = Respell(s.Heading, spellings) } : s).ToList();

        var topics = new List<CardTopic>();
        var cards = new List<StudyCardItem>();
        var seen = new HashSet<string>();
        void AddCard(StudyCardItem card) { if (cards.Count < CardDeckBuilder.MaxCards && seen.Add(card.Id)) cards.Add(Review(card)); }

        CardTopic? topic = null;
        int? topicMajor = null;
        var withBody = new HashSet<string>();
        var number = 0;
        foreach (var section in sections)
        {
            // A top-level heading opens a topic; its subsections stay in it. Without headings, blocks of paragraphs do.
            // "4.1 …" after chapter 3 starts chapter 4 even when the "4 …" heading itself was not recognised, so its
            // cards never sit under the previous chapter's title.
            if (topic is null || section.Level <= 1 || (section.Major is int major && major != topicMajor))
            {
                // Text before the first heading is the introduction; a document without headings is read in parts.
                var title = section.Heading.Length > 0 ? section.Heading
                    : topics.Count == 0 && sections.Any(s => s.Heading.Length > 0) ? "Giriş" : $"Hissə {topics.Count + 1}";
                topic = new CardTopic(Key($"{title}|{topics.Count}"), title);
                topics.Add(topic);
                topicMajor = section.Major;
            }
            number++;
            if (section.Body.Count == 0) continue; // a chapter heading followed directly by its first subsection
            withBody.Add(topic.Key);
            // The card names the subsection it comes from when that is narrower than the topic.
            var label = section.Heading.Length > 0 && !string.Equals(section.Heading, topic.Title, StringComparison.OrdinalIgnoreCase) ? section.Heading : null;
            void Add(StudyCardItem card) => AddCard(card with { Section = label });
            var lines = section.Body.Where(p => !p.IsTableRow).Select(p => p.Text).ToList();
            // Reading material is not a flashcard: keep it accessible even when no reliable card can be extracted.
            var reading = string.Join("\n\n", section.Body.Select(p => p.IsTableRow
                ? p.TableText : p.Text));
            var previous = topics[^1];
            topics[^1] = previous with { Notes = string.Join("\n\n", new[] { previous.Notes, section.Heading, reading }.Where(t => !string.IsNullOrWhiteSpace(t))) };
            // Lecture notes rarely write "X — definition"; every titled section still has an explanation to recall.
            if (section.Heading.Length > 0 && char.IsUpper(section.Heading[0]) && !GenericHeading().IsMatch(section.Heading) && Summary(section.Body) is { } summary)
                AddCard(new(CardMastery.Hash(CardKind.Question, "section\u001f" + section.Heading), CardKind.Question, topic.Key, section.Heading,
                    summary, [], [], number, section.Body[0].Page, section.Body[0].ChunkOrdinal, IsSection: true));
            foreach (var paragraph in section.Body.Where(p => !p.IsTableRow))
            {
                foreach (var (term, definition) in Terms(paragraph.Text).Concat(Namings(paragraph.Text)))
                {
                    var front = $"{term} nədir?";
                    Add(new(CardMastery.Hash(CardKind.Term, front + "\u001f" + definition), CardKind.Term, topic.Key,
                        front, definition, [], [], number, paragraph.Page, paragraph.ChunkOrdinal));
                }
                if (FunctionStatement().Match(paragraph.Text) is { Success: true } function
                    && paragraph.Text.Length is >= 30 and <= 250 && !DocumentCleaner.IsCode(paragraph.Text)
                    && !DependentStart().IsMatch(function.Groups["rest"].Value))
                {
                    var front = $"{function.Groups["term"].Value} nə edir?";
                    Add(new(CardMastery.Hash(CardKind.Term, front + "\u001f" + paragraph.Text), CardKind.Term, topic.Key,
                        front, paragraph.Text, [], [], number, paragraph.Page, paragraph.ChunkOrdinal));
                }
            }
            foreach (var (title, items) in CardDeckBuilder.Lists(lines))
                Add(new(CardMastery.Hash(CardKind.List, title), CardKind.List, topic.Key, $"{title}: hansılardır?", "", items, [],
                    number, section.Body[0].Page, section.Body[0].ChunkOrdinal));
            foreach (var card in TableCards(section.Body, topic.Key, number)) Add(card);
        }
        // A prompt with conflicting answers needs context we cannot safely infer.
        var unambiguous = cards.GroupBy(c => c.Front, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(c => c.Back + "\u001f" + string.Join("\u001f", c.Items)).Distinct().Count() == 1)
            .Select(g => g.First()).ToList();
        return new(topics.Where(t => withBody.Contains(t.Key)).ToList(), unambiguous);
    }

    // ---------- sections ----------

    private const int MinSummary = 60;
    private const int MaxSummary = 1800;
    private const int SummarySentences = 6;
    /// <summary>Prose paragraphs of a section looked at for its summary.</summary>
    private const int SummaryCandidates = 3;
    /// <summary>A first "sentence" shorter than this is a run-in label ("Site-to-site VPN.").</summary>
    private const int RunInLabel = 25;

    /// <summary>
    /// The opening of a section's explanation: its first real prose paragraph, up to six sentences. Null when the section
    /// is only lists, tables, examples or fragments, so no card asks for something the back cannot answer.
    /// </summary>
    public static string? Summary(IReadOnlyList<DocParagraph> body)
    {
        var tried = 0;
        foreach (var p in body)
        {
            if (p.IsTableRow) continue;
            var text = p.Text.Trim();
            if (text.Length < MinSummary || text.EndsWith(':') || ItemStart().IsMatch(text)) continue;
            if (++tried > SummaryCandidates) break;
            // "burada - …" (a formula's legend), examples and "Bu bölmədə …" need what the card cannot show.
            if (!char.IsUpper(text[0]) || DocumentCleaner.IsNoise(text) || DocumentCleaner.IsCode(text) || ExampleStart().IsMatch(text) || OpenQuestionExtractor.IsPromptOnly(text) || text.Contains('�')) continue;
            if (!text.EndsWith('.') && !text.EndsWith('!')) continue;
            var ends = SentenceEnd().Matches(text).Select(m => m.Index + 1).Append(text.Length).ToList();
            // "Site-to-site VPN. VPN …": a run-in label is not part of the explanation.
            var from = ends[0] < RunInLabel ? ends[0] : 0;
            var summary = "";
            foreach (var end in ends.Where(e => e > from).Take(SummarySentences))
            {
                if (end - from > MaxSummary) break;
                summary = text[from..end].Trim();
            }
            // "(Cədvəl 4)" points at a picture or table the card does not show; "…mı?" is a question, not an explanation.
            // "Təqribən ." / "kəmiyyəti , uyğun …": a formula the text extraction lost.
            if (summary.Length >= MinSummary && !summary.Contains('?') && !Reference().IsMatch(summary)
                && !summary.Contains(" .", StringComparison.Ordinal) && !summary.Contains(" ,", StringComparison.Ordinal)) return summary;
        }
        return null;
    }

    // ---------- review ----------

    /// <summary>
    /// Marks a card the extraction is unsure of as "yoxlanmalı": it stays in the deck, the student sees the mark and can hide
    /// the card. Formula-bearing definitions, phrases taken for terms and lists whose title needs its context are such cards.
    /// </summary>
    public static StudyCardItem Review(StudyCardItem card)
    {
        var unsure = card.Kind switch
        {
            CardKind.Term => UnsureTerm(TermOf(card.Front)) || card.Back.Contains(@"\(", StringComparison.Ordinal) || card.Back.Contains('�'),
            // "Üstünlükləri: hansılardır?": whose advantages is clear only from the document around it.
            CardKind.List => card.Front.Split(' ').Length <= 2 || card.Items.Any(i => i.Length > 200),
            _ => false,
        };
        return unsure ? card with { NeedsReview = true } : card;
    }

    /// <summary>Five or more words outside brackets, a lowercase start or "X və Y": a phrase, not one named thing.</summary>
    private static bool UnsureTerm(string term)
    {
        var bare = Regex.Replace(term, @"\([^)]*\)", " ");
        var words = bare.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words >= 5 || (char.IsLower(term[0]) && words >= 3)
            || Regex.IsMatch(bare, @"\s(?:və|and|и)\s", RegexOptions.IgnoreCase);
    }

    private static string TermOf(string front) =>
        front.EndsWith(" nədir?", StringComparison.Ordinal) ? front[..^7] : front.EndsWith(" nə edir?", StringComparison.Ordinal) ? front[..^9] : front;

    // ---------- terms ----------

    /// <summary>Definitions written in the usual ways: "X — …", "X: …", "X (english) … bildirir", "X dedikdə …", "… X adlanır".</summary>
    public static IEnumerable<(string Term, string Definition)> Terms(string line)
    {
        // A delimiter is evidence of a definition; parenthetical expansions and guessed last words are not.
        if (CardDeckBuilder.Definition(line) is { } dash) { yield return dash; yield break; }
        if (Labelled().Match(line) is { Success: true } labelled && Term(labelled.Groups["term"].Value) is { } term
            && ReliableDefinition(labelled.Groups["def"].Value))
            yield return (term, labelled.Groups["def"].Value.Trim());
        else if (Meaning().Match(line) is { Success: true } meaning && Term(meaning.Groups["term"].Value) is { } named
            && ReliableDefinition(meaning.Groups["def"].Value))
            yield return (named, line.Trim());
    }

    /// <summary>
    /// "… çıxarır. Bu prosesə render etmək deyilir." / "… azaldılır - bu xüsusiyyətə “auto-scaling” deyilir.": the term is named right
    /// after "bu …", so its bounds are certain; the explanation is the sentence before (or the clause before the dash).
    /// "… kompleksi radiorele sistemi adlanır" is not read: where its term starts is a guess.
    /// </summary>
    public static IEnumerable<(string Term, string Definition)> Namings(string paragraph)
    {
        var text = paragraph.Trim();
        var ends = SentenceEnd().Matches(text).Select(m => m.Index + 1).Append(text.Length).ToList();
        var start = 0;
        string? previous = null;
        foreach (var end in ends)
        {
            var sentence = text[start..end].Trim();
            start = end;
            var before = previous;
            previous = sentence;
            if (NamingSentence().Match(sentence) is not { Success: true } m) continue;
            var raw = m.Groups["term"].Value;
            if (Quoted().Match(raw) is { Success: true } quoted) raw = quoted.Groups[1].Value;
            else if (raw.Contains('"') || raw.Contains('“') || raw.Contains('«')) continue;
            // "Session ID və ya qısaca SID" → "Session ID (SID)".
            raw = Regex.Replace(raw, @"^(?<name>.+?)\s+və ya qısaca\s+(?<short>\S+)$", "${name} (${short})");
            if (Regex.IsMatch(raw, @"\b(?:bu|yəni|isə|görə)\b", RegexOptions.IgnoreCase) || Term(raw) is not { } term) continue;
            var explanation = m.Groups["inline"].Success ? m.Groups["inline"].Value.Trim().TrimEnd(',', ';') + "." : before;
            if (explanation is null || explanation.Length is < 25 or > 350 || !char.IsUpper(explanation[0]) || explanation.Contains('?')
                || !explanation.EndsWith('.') || DocumentCleaner.IsNoise(explanation) || DocumentCleaner.IsCode(explanation)
                || ExampleStart().IsMatch(explanation) || DependentStart().IsMatch(explanation) || Reference().IsMatch(explanation)) continue;
            yield return (term, m.Groups["inline"].Success ? sentence : $"{explanation} {sentence}");
        }
    }

    /// <summary>Reject fragments, dangling references and multi-paragraph answers rather than inventing an explanation.</summary>
    public static bool ReliableDefinition(string text) => text.Length is >= 25 and <= 350
        && !DocumentCleaner.IsNoise(text) && !DocumentCleaner.IsCode(text)
        && !DependentStart().IsMatch(text.Trim()) && !text.Contains('\n')
        && text.TrimEnd().EndsWith('.') && Predicate().IsMatch(FirstSentence(text))
        && !Regex.IsMatch(FirstSentence(text), @"\b(?:yoxdur|vardır|bağlıdır|əlçatandır)\.$", RegexOptions.IgnoreCase);

    public static bool ExplicitQuestion(string text) => text.Length is >= 8 and <= 400
        && !DocumentCleaner.IsNoise(text) && QuestionCue().IsMatch(text);

    /// <summary>The term when <paramref name="raw"/> names something (not a clause, adverb or location phrase), else null.</summary>
    public static string? CleanTerm(string raw) => Term(raw);

    private static string? Term(string raw)
    {
        var term = raw.Trim().Trim('"', '“', '”', '«', '»', '*', '-', '•').TrimEnd('.', ',');
        if (term.Length is < 2 or > 70 || term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 6) return null;
        if (!char.IsLetter(term[0]) || DocumentCleaner.IsNoise(term) || term.IndexOfAny(['.', ':', ';', '?', '!']) >= 0
            || Regex.IsMatch(term, @"\s-\s|\d|\b(?:birinci|ikinci|üçüncü|dördüncü|əvvəlki|əsas mesaj|tipik təkamül)\b", RegexOptions.IgnoreCase)) return null;
        if (term.Count(c => c == '(') != term.Count(c => c == ')')) return null;
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // "Bu iki hadisə … gətirir", "Əgər …": a clause, not the name of something.
        if (NotTermStart().IsMatch(words[0]) || (words.Length > 1 && VerbEnding().IsMatch(words[^1]))) return null;
        // "Normal yayılma şəraitində RRS1": a word in the locative case makes it a circumstance, not a term.
        if (words.Length > 1 && words.Any(w => Locative().IsMatch(w))) return null;
        return term;
    }

    private static string FirstSentence(string text)
    {
        var m = SentenceEnd().Match(text);
        return m.Success ? text[..(m.Index + 1)] : text;
    }

    /// <summary>First letter in capitals; Azerbaijani "i" becomes "İ" (culture-free, so it also works without ICU).</summary>
    private static string Capital(string text) => text.Length == 0 ? text : (text[0] == 'i' ? 'İ' : char.ToUpperInvariant(text[0])) + text[1..];

    /// <summary>Word key that ignores case and the I/İ/ı/i confusion of text typed in capitals.</summary>
    private static string SpellingKey(string word) => string.Concat(word.Select(c => c is 'İ' or 'I' or 'ı' ? 'i' : char.ToLowerInvariant(c)));

    /// <summary>
    /// How the body writes each word in mid-sentence ("IoT", "AWS", "qurğularının"), by <see cref="SpellingKey"/>. Words at a
    /// sentence start are skipped: their capital says nothing about the word.
    /// </summary>
    public static Dictionary<string, string> Spellings(IReadOnlyList<DocParagraph> paragraphs)
    {
        var counts = new Dictionary<string, Dictionary<string, int>>();
        foreach (var p in paragraphs)
        {
            if (p.IsTableRow || p.Text.Length < 40 || Shouting(p.Text)) continue;
            foreach (Match m in Word().Matches(p.Text))
            {
                var before = p.Text[..m.Index].TrimEnd();
                if (before.Length == 0 || ".!?:•-–—\"“«(".Contains(before[^1])) continue;
                var forms = counts.TryGetValue(SpellingKey(m.Value), out var f) ? f : counts[SpellingKey(m.Value)] = [];
                forms[m.Value] = forms.GetValueOrDefault(m.Value) + 1;
            }
        }
        return counts.ToDictionary(c => c.Key, c => c.Value.MaxBy(f => f.Value).Key);
    }

    /// <summary>A calmed heading with each word spelled as the body spells it; the first letter stays capital.</summary>
    public static string Respell(string heading, IReadOnlyDictionary<string, string> spellings) =>
        Capital(Word().Replace(heading, m => spellings.TryGetValue(SpellingKey(m.Value), out var body) ? body : m.Value));

    // ---------- sections ----------

    /// <summary>A "PLAN" / "Mündəricat" list: its lines name the headings that follow.</summary>
    private static HashSet<string> PlanHeadings(IReadOnlyList<DocParagraph> paragraphs, out (int From, int To) range)
    {
        range = (-1, -1);
        for (var i = 0; i < Math.Min(paragraphs.Count, 60); i++)
        {
            if (!PlanTitle().IsMatch(paragraphs[i].Text.Trim().TrimEnd(':', '.'))) continue;
            var lines = new List<string>();
            var j = i + 1;
            for (; j < paragraphs.Count && paragraphs[j].Text.Length <= 120 && !paragraphs[j].IsTableRow; j++) lines.Add(Fold(StripNumber(paragraphs[j].Text)));
            if (lines.Count >= 3) { range = (i, j); return lines.ToHashSet(); }
        }

        // No "PLAN" title: a run of short lines near the start whose lines come back later as headings is the plan.
        for (var i = 0; i < Math.Min(paragraphs.Count, 40); i++)
        {
            var j = i;
            while (j < paragraphs.Count && paragraphs[j].Text.Length <= 120 && !paragraphs[j].IsTableRow && !paragraphs[j].Text.EndsWith(':')) j++;
            if (j - i >= 3)
            {
                var lines = paragraphs.Skip(i).Take(j - i).Select(p => Fold(StripNumber(p.Text))).ToList();
                var later = paragraphs.Skip(j).Where(p => p.Text.Length <= 120).Select(p => Fold(StripNumber(p.Text))).ToHashSet();
                if (lines.Count(later.Contains) >= Math.Max(2, lines.Count / 2)) { range = (i, j); return lines.ToHashSet(); }
            }
            i = Math.Max(i, j);
        }
        return [];
    }

    private static List<Section> Sections(IReadOnlyList<DocParagraph> paragraphs, HashSet<string> plan, (int From, int To) planRange)
    {
        var sections = new List<Section>();
        Section? current = null;
        // A Word file that marks its headings with heading styles is read by those styles only: guessing from the text
        // would add false headings (short lines) and miss real ones (long titles, titles with ":").
        var styled = paragraphs.Where(p => p.HeadingLevel > 0 && !p.IsTableRow).Select(p => p.HeadingLevel!.Value).ToList();
        var topLevel = styled.Count >= MinStyledHeadings ? styled.Min() : (int?)null;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (i >= planRange.From && i < planRange.To && paragraphs[i].HeadingLevel is null) continue;
            var p = paragraphs[i];
            if (topLevel is int top && !p.IsTableRow)
            {
                if (p.HeadingLevel == 0) continue; // the document title is not a topic
                if (StyledHeading(p, top) is { } styledHeading)
                {
                    current = new Section(styledHeading.Title, styledHeading.Level, [], i, styledHeading.Major);
                    sections.Add(current);
                    continue;
                }
            }
            else if (!p.IsTableRow && Heading(paragraphs, i, plan) is { } heading)
            {
                current = new Section(heading.Title, heading.Level, [], i, heading.Major);
                sections.Add(current);
                continue;
            }
            if (current is null || (current.Heading.Length == 0 && current.Body.Count >= TopicBlock))
            {
                current = new Section("", 1, [], i);
                sections.Add(current);
            }
            current.Body.Add(p);
        }
        // A heading without body text is kept: "7 …" directly followed by "7.1 …" still opens chapter 7.
        return sections.Where(s => s.Body.Count > 0 || s.Heading.Length > 0).ToList();
    }

    /// <summary>A paragraph with a Word heading style, its level counted from the document's top heading level.</summary>
    private static (string Title, int Level, int? Major)? StyledHeading(DocParagraph p, int topLevel)
    {
        if (p.HeadingLevel is not int styleLevel || styleLevel < topLevel) return null;
        var text = p.Text.Trim();
        var title = StripNumber(text).Trim().TrimEnd('.', ':', ' ');
        // A whole paragraph styled as a heading is body text in a heading's clothes.
        if (title.Length is < 2 or > 150 || title.Count(char.IsLetter) < 2) return null;
        var numbered = NumberedHeading().Match(text);
        int? major = numbered.Success && int.TryParse(numbered.Groups["n"].Value.Split('.')[0], out var n) ? n : null;
        return (Shouting(title) ? Calm(title) : title, styleLevel - topLevel + 1, major);
    }

    /// <summary>Numbered ("3.2 Marşrutlaşdırma"), listed in the plan, ALL CAPS, or a short line followed by real text.</summary>
    private static (string Title, int Level, int? Major)? Heading(IReadOnlyList<DocParagraph> paragraphs, int i, HashSet<string> plan)
    {
        var text = paragraphs[i].Text.Trim();
        // Arrows and dashes between names ("Anlayış → AWS → Azure") are not words.
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(w => w.Any(char.IsLetterOrDigit));
        // "3 İş mexanizmi: komponentlər…" / "3.2 Marşrutlaşdırma: …" are chapter titles; "1. Brauzer: …" stays a list item.
        var chapterNumber = ChapterNumber().IsMatch(text);
        if (text.EndsWith('.') || text.Contains('—') || (text.Contains(": ") && !chapterNumber) || Meaning().IsMatch(text)) return null;
        if (text.Length > 120 || words > 14 || text.EndsWith(':') || text.EndsWith(',') || text.EndsWith(';')) return null;
        var next = i + 1 < paragraphs.Count ? paragraphs[i + 1] : null;
        var numbered = NumberedHeading().Match(text);
        // "4 …" directly followed by "4.1 …" opens chapter 4 even after a "…:" line (e.g. code dropped by cleaning).
        var opensChapter = chapterNumber && next is not null && NumberedHeading().Match(next.Text) is { Success: true } sub
            && sub.Groups["n"].Value.StartsWith(numbered.Groups["n"].Value + ".", StringComparison.Ordinal);
        if (i > 0 && paragraphs[i - 1].Text.EndsWith(':') && !opensChapter) return null; // an item of a list
        // A chapter-numbered title is also a heading before a short complete sentence ("3.11 Requests və limits").
        var followedByText = next is not null && (next.IsTableRow || next.Text.Length >= 100 || next.Text.EndsWith(':')
            || (chapterNumber && next.Text.Length >= 40 && next.Text.EndsWith('.')));
        var title = StripNumber(text).Trim().TrimEnd('.');
        if (title.Length < 3 || !char.IsLetter(title[0])) return null;

        var level = numbered.Success ? numbered.Groups["n"].Value.Count(c => c == '.') + 1 : 1;
        int? major = numbered.Success && int.TryParse(numbered.Groups["n"].Value.Split('.')[0], out var n) ? n : null;
        if (plan.Contains(Fold(StripNumber(text)))) return (Shouting(title) ? Calm(title) : title, level, major);
        if (numbered.Success && words <= 12 && (followedByText || (next is not null && NumberedHeading().IsMatch(next.Text)))) return (title, level, major);
        if (text.Contains(": ")) return null;
        if (words <= 10 && text.Length >= 4 && Shouting(text) && followedByText) return (Calm(title), 1, null);
        if (!numbered.Success && words <= 9 && char.IsUpper(text[0]) && followedByText && !text.Contains(". ")) return (title, 1, null);
        return null;
    }

    // ---------- tables ----------

    /// <summary>One card per table row: the row's name in front, "column: value" pairs behind, the caption as context.</summary>
    public static IEnumerable<StudyCardItem> TableCards(IReadOnlyList<DocParagraph> body, string topicKey, int number)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (!body[i].IsTableRow) continue;
            var start = i;
            var rows = new List<List<string>>();
            while (i < body.Count && body[i].IsTableRow && (i == start || !body[i].IsTableStart))
            {
                rows.Add(body[i].TableText.Split('|').Select(c => c.Trim()).ToList());
                i++;
            }
            i--;
            foreach (var card in TableRows(rows, body[start].Caption, topicKey, number, body[start]))
                yield return card;
        }
    }

    public static IEnumerable<StudyCardItem> TableRows(List<List<string>> rows, string? caption, string topicKey, int number, DocParagraph at)
    {
        if (rows.Count < 2) yield break;
        var header = rows[0];
        var width = header.Count;
        if (width < 2 || rows.Any(r => r.Count != width)) yield break;
        var body = rows.Skip(1).ToList();
        // Measurements and datasets ("Aşağı | 0", "10.20.0.0/16 | local") are looked up, not recalled.
        var cells = body.SelectMany(r => r).ToList();
        if (cells.Count(c => c.Length <= 3 || NumberLike().IsMatch(c)) * 2 > cells.Count) yield break;
        var labelColumn = body.All(r => int.TryParse(r[0].TrimEnd('.'), out _)) && width > 2 ? 1 : 0;
        if (header.Skip(labelColumn).Any(h => h.Length is < 2 or > 100 || DocumentCleaner.IsNoise(h))
            || header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != width) yield break;
        string? context = string.IsNullOrWhiteSpace(caption) ? null : caption.Length <= 120 ? caption : caption.Split('(')[0].Trim();
        if (context?.Length > 120) context = null;
        bool Usable(string value, string label, string column) => !(value.TrimEnd().EndsWith(',') || value.TrimEnd().EndsWith(':') || value.Length is < 1 or > 350
            || value == label || value == column || value is "—" or "-" or "..." || DocumentCleaner.IsCode(value) || DependentStart().IsMatch(value));

        // "Word2Vec | GloVe", "RNN | Transformer": both columns name the things compared, so the table is one comparison card.
        if (width == 2 && !LabelHeader().IsMatch(header[0]) && !AttributeHeader().IsMatch(header[1]))
        {
            var pairs = body.Where(r => Usable(r[0], r[1], header[0]) && Usable(r[1], r[0], header[1]))
                .Select(r => $"{header[0]}: {r[0]} — {header[1]}: {r[1]}").Take(12).ToList();
            if (pairs.Count < 2) yield break;
            var front = $"{header[0]} və {header[1]}: fərqləri nələrdir?";
            yield return new(CardMastery.Hash(CardKind.Table, front + "\u001f" + string.Join("\u001f", pairs)), CardKind.Table, topicKey,
                front, context ?? $"{header[0]} / {header[1]}", pairs, [], number, at.Page, at.ChunkOrdinal);
            yield break;
        }

        // Otherwise one card per row: the row's name in front, every column of that row behind.
        var repeatedLabels = body.GroupBy(r => r[labelColumn], StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in body)
        {
            var label = row[labelColumn];
            if (repeatedLabels.Contains(label) || label.Length is < 2 or > 100 || DocumentCleaner.IsNoise(label) || DependentStart().IsMatch(label)) continue;
            var values = Enumerable.Range(labelColumn + 1, width - labelColumn - 1)
                .Where(c => Usable(row[c], label, header[c])).Select(c => $"{header[c]}: {row[c]}").ToList();
            if (values.Count == 0) continue;
            // "Termin | Mənası": a glossary row asks for the meaning; wider rows ask for the row as a whole.
            var column = header[labelColumn + 1];
            // "BFS (Breadth-First Search)" → "BFS": the columns named shortly, "«Məqsəd» — BFS / DFS".
            var columns = string.Join(" / ", header.Skip(labelColumn + 1).Select(h => Regex.Replace(h, @"\s*\([^)]*\)", "").Trim()));
            var front = width - labelColumn == 2 && column.Length <= 25 && !column.Contains(':') ? $"«{label}»: {Lower(column)} nədir?"
                : columns.Length <= 60 ? $"«{label}» — {columns}"
                : $"{context ?? header[labelColumn]}: «{label}»";
            yield return new(CardMastery.Hash(CardKind.Table, front + "\u001f" + string.Join("\u001f", values)), CardKind.Table, topicKey,
                front, context ?? header[labelColumn], values, [], number, at.Page, at.ChunkOrdinal);
        }
    }

    private static string Lower(string text) => text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    // ---------- helpers ----------

    /// <summary>Mostly capital letters ("İoT QURĞULARININ TƏHLÜKƏSİZLİYİ").</summary>
    private static bool Shouting(string text)
    {
        var letters = text.Where(char.IsLetter).ToList();
        return letters.Count > 3 && letters.Count(char.IsUpper) >= letters.Count * 0.8;
    }

    /// <summary>
    /// A shouted heading in sentence case. Capital "I" is lowered to "i": documents typed in capitals often use the Latin
    /// "I" for "İ" ("TƏHLÜKƏSIZLIK"), and "ı" there would misspell most words.
    /// </summary>
    private static string Calm(string text) => Capital(string.Concat(text.Select(c => c is 'İ' or 'I' ? 'i' : char.ToLowerInvariant(c))));

    private static string StripNumber(string text) => NumberedHeading().Match(text) is { Success: true } m ? m.Groups["title"].Value : text;
    private static string Fold(string text) => TextNormalizer.FoldedPhrase(text.Trim().TrimEnd('.', ':')).Trim();
    private static string Key(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

    [GeneratedRegex(@"^(?:məsələn|misal|nümunə|bu|bunlar|həmin|onun|onlar|belə|yuxarıda|aşağıda|şəkildə|cədvəldə|it|this|these|those)\b", RegexOptions.IgnoreCase)] private static partial Regex DependentStart();
    [GeneratedRegex(@"(?:dır|dir|dur|dür|deyilir|adlanır)[.!]$", RegexOptions.IgnoreCase)] private static partial Regex Predicate();
    [GeneratedRegex(@"\b(?:nədir|nə|necə|niyə|hansı|hansılardır|izah edin|izahı|tərifi|haqqında|mahiyyəti|what|why|how|define|explain)\b", RegexOptions.IgnoreCase)] private static partial Regex QuestionCue();
    [GeneratedRegex(@"^(?<term>[\p{Lu}][\p{Lu}\d]{1,11})\s+(?<rest>[^.!?]{15,}\b(?:çevirir|ötürür|saxlayır|yoxlayır|idarə edir|təmin edir))\.$")]
    private static partial Regex FunctionStatement();
    [GeneratedRegex(@"^(?<n>\d{1,2}(?:\.\d{1,2}){0,3})\.?\s+(?<title>\S.*)$")] private static partial Regex NumberedHeading();
    [GeneratedRegex(@"^[\d\s.,:/%+\-–×x≤≥<>()]+$")] private static partial Regex NumberLike();
    /// <summary>First-column headers that name the rows' subject, not one of two compared things.</summary>
    [GeneratedRegex(@"^(?:termin|anlayış|xüsusiyyət|meyar|kriteriya|parametr|ad|adı|komponent|element|növ|növü|sahə|mərhələ|addım|xidmət|model|qat|səviyyə|simptom|yanlış anlaşılma|ehtiyac|yoxlama|miqyas|term|concept|feature|criterion|property|component|name|type|step|layer|термин|понятие|характеристика|критерий|параметр|название)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LabelHeader();
    /// <summary>Second-column headers that describe the first column ("Termin | Mənası").</summary>
    [GeneratedRegex(@"^(?:mənası|məna|tərif|tərifi|izah|izahı|qısa izah|təsvir|təsviri|açıqlama|nümunə|funksiya|funksiyası|məqsəd|məqsədi|dəyər|qeyd|rolu|vəzifəsi|meaning|definition|description|explanation|example|purpose|value|role|значение|определение|описание|пример)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AttributeHeader();
    /// <summary>"3 Title" or "3.2 Title": a chapter number without the "." or ")" that numbered list items carry.</summary>
    [GeneratedRegex(@"^\d{1,2}(?:\.\d{1,2}){0,3}\s+\p{L}")] private static partial Regex ChapterNumber();
    [GeneratedRegex(@"^(?:plan|mövzunun planı|mühazirənin planı|mündəricat|məzmun|содержание|план|contents)$", RegexOptions.IgnoreCase)] private static partial Regex PlanTitle();
    [GeneratedRegex(@"^\s*(?:[-•▪●◦*]\s*)?(?<term>[^:()\n]{2,60}?)\s*:\s+(?<def>\S.{20,})$")] private static partial Regex Labelled();
    [GeneratedRegex(@"^(?<term>[\p{Lu}][^,.:;\n]{1,60}?)\s+dedikdə\s+(?<def>.+)$")] private static partial Regex Meaning();
    [GeneratedRegex(@"[.!?](?=\s+[\p{Lu}\d])")] private static partial Regex SentenceEnd();
    [GeneratedRegex(@"(?:ir|ır|ur|ür|(?<!l)ar|(?<!l)ər)$")] private static partial Regex VerbEnding();
    [GeneratedRegex(@"^(?:bu|bunlar|həmin|onun|onlar|belə|əgər|məsələn|lakin|amma|beləliklə|burada|sonra|əvvəl|hər|bütün|bəzi|biz|siz|mən|nəticədə|yəni|misal|adətən|praktikada|qeyd|diqqət|nümunə|əsasən|xüsusilə|əksər|bir|iki|üç|çox|daha|ən)$", RegexOptions.IgnoreCase)]
    private static partial Regex NotTermStart();
    [GeneratedRegex(@"\p{L}(?:nda|ndə|ında|ində|unda|ündə|də|da|dən|dan)$", RegexOptions.IgnoreCase)] private static partial Regex Locative();
    [GeneratedRegex(@"^\s*(?:[-•▪●◦*]|\d{1,2}[.)])\s")] private static partial Regex ItemStart();
    [GeneratedRegex(@"\p{L}+")] private static partial Regex Word();
    [GeneratedRegex(@"^(?:(?<inline>\p{Lu}.{20,}?)\s+[-–—]\s+)?(?:buna|(?:bu|belə)\s+(?!görə\b)\p{L}+)\s+(?<term>\S.{1,60}?)\s+(?:deyilir|adlanır|adlandırılır)\.$", RegexOptions.IgnoreCase)]
    private static partial Regex NamingSentence();
    [GeneratedRegex(@"^[""“«]([^""“”«»]{2,60})[""”»]$")] private static partial Regex Quoted();
    [GeneratedRegex(@"^(?:məsələn|misal|nümunə|yuxarıda|aşağıda|şəkildə|cədvəldə|bu (?:bölmədə|fəsildə|mövzuda|hissədə|mühazirədə))\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExampleStart();
    [GeneratedRegex(@"\b(?:Şəkil|Şək\.|Cədvəl|Diaqram|Sxem|Figure|Fig\.|Table|Рис\.|Рисунок|Таблица)\s*\d", RegexOptions.IgnoreCase)] private static partial Regex Reference();
    /// <summary>Headings that ask nothing on their own ("Giriş", "Nəticə").</summary>
    [GeneratedRegex(@"^(?:giriş|nəticə|nəticələr|xülasə|yekun|annotasiya|ümumi məlumat|introduction|conclusion|summary|введение|заключение)$", RegexOptions.IgnoreCase)]
    private static partial Regex GenericHeading();
}

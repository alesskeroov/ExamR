using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ExamFlow.Parsing;

/// <summary>Reads DOCX package text without Word automation or external resources.</summary>
public sealed partial class DocxQuestionParser(PdfQuestionParser questionParser)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string MainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const long MaxExpandedBytes = 64 * 1024 * 1024;
    private const long MaxXmlCharacters = 16 * 1024 * 1024;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace StrictW = "http://purl.oclc.org/ooxml/wordprocessingml/main";

    [GeneratedRegex(@"^\d{1,4}[.)]?$")]
    private static partial Regex NumberOnly();
    [GeneratedRegex(@"^(?:\d{1,4}\s*[.):\-]|sual\s*\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuestionStart();
    [GeneratedRegex(@"^(?:[A-Fa-f][).]|[•●○◦▪■□√✓✔☑])")]
    private static partial Regex OptionStart();

    public static bool Inspect(Stream stream)
    {
        try
        {
            using var archive = Open(stream);
            _ = ReadDocument(archive);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    public ParseResult Parse(Stream stream, CancellationToken ct = default)
    {
        try
        {
            using var archive = Open(stream);
            var document = ReadDocument(archive);
            var styles = ReadXml(archive, "word/styles.xml");
            var numbering = ReadXml(archive, "word/numbering.xml");
            var state = new ExtractionState(styles, numbering);
            var lines = new List<TextLine>();
            foreach (var block in document.Root!.Element(W + "body")!.Elements())
            {
                ct.ThrowIfCancellationRequested();
                state.ReadBlock(block, lines, ct);
            }
            ct.ThrowIfCancellationRequested();
            // Reuse segmentation, answer detection, deduplication and length limits, not PDF extraction.
            return questionParser.ParseLines(SeparateOpenAnswers(lines), 0);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or InvalidOperationException or NotSupportedException)
        {
            return ParseResult.Failed(ParseFailure.Unreadable);
        }
    }

    private static IReadOnlyList<TextLine> SeparateOpenAnswers(List<TextLine> lines)
    {
        var result = new List<TextLine>();
        for (var i = 0; i < lines.Count;)
        {
            if (!QuestionStart().IsMatch(lines[i].Text)) { result.Add(lines[i++]); continue; }
            var end = i + 1;
            while (end < lines.Count && !QuestionStart().IsMatch(lines[end].Text)) end++;
            var group = lines.GetRange(i, end - i);
            // Lists inside a prose answer are answer content, not multiple-choice options.
            var hasOptions = group.Skip(1).Take(1).Any(l => OptionStart().IsMatch(l.Text));
            var hasAnswer = group.Any(l => Regex.IsMatch(l.Text, @"(?:^|\s)(?:cavab|[iİıI]zah|answer)\s*[:：]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            result.Add(group[0]);
            // In an open Word question, subsequent paragraphs form the answer unless explicitly labelled.
            if (group.Count > 1 && !hasOptions && !hasAnswer)
                result.Add(new TextLine(1, [TextToken.Plain("Cavab:")]));
            result.AddRange(group.Skip(1));
            i = end;
        }
        return result;
    }

    private static ZipArchive Open(Stream stream)
    {
        var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        try
        {
            long expanded = 0;
            if (archive.Entries.Count > 2048) throw new InvalidDataException("Too many package entries.");
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxExpandedBytes - expanded) throw new InvalidDataException("Package is too large.");
                expanded += entry.Length;
            }
            if (archive.Entries.GroupBy(e => e.FullName, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidDataException("Duplicate package entries.");
            var types = ReadXml(archive, "[Content_Types].xml");
            XNamespace package = "http://schemas.openxmlformats.org/package/2006/content-types";
            if (types?.Root?.Elements(package + "Override").Any(e =>
                (string?)e.Attribute("PartName") == "/word/document.xml" && (string?)e.Attribute("ContentType") == MainContentType) != true)
                throw new InvalidDataException("Not a DOCX document.");
            return archive;
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private static XDocument ReadDocument(ZipArchive archive)
    {
        var document = ReadXml(archive, "word/document.xml");
        if (document?.Root?.Name != W + "document" || document.Root.Element(W + "body") is null)
            throw new InvalidDataException("Missing document body.");
        return document;
    }

    private static XDocument? ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null) return null;
        if (entry.Length > MaxXmlCharacters) throw new InvalidDataException("XML part is too large.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxXmlCharacters,
        });
        var xml = XDocument.Load(reader);
        // Word can save the same vocabulary using the strict OOXML namespace.
        foreach (var element in xml.Root!.DescendantsAndSelf())
        {
            if (element.Name.Namespace == StrictW) element.Name = W + element.Name.LocalName;
            foreach (var attribute in element.Attributes().Where(a => a.Name.Namespace == StrictW).ToList())
            {
                element.SetAttributeValue(W + attribute.Name.LocalName, attribute.Value);
                attribute.Remove();
            }
        }
        return xml;
    }

    private sealed class ExtractionState(XDocument? styles, XDocument? numbering)
    {
        private readonly Dictionary<(string Id, int Level), int> _counters = [];
        private int _nextQuestion = 1;

        private IEnumerable<XElement> StyleChain(string? id)
        {
            var seen = new HashSet<string>();
            while (id is not null && seen.Add(id))
            {
                var style = styles?.Root?.Elements(W + "style").FirstOrDefault(s => (string?)s.Attribute(W + "styleId") == id);
                if (style is null) yield break;
                yield return style;
                id = (string?)style.Element(W + "basedOn")?.Attribute(W + "val");
            }
        }

        public void ReadBlock(XElement block, List<TextLine> lines, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (block.Name == W + "p")
            {
                ReadParagraph(block, lines);
            }
            else if (block.Name == W + "tbl")
            {
                int questionColumn = -1, answerColumn = -1, numberColumn = -1;
                foreach (var row in block.Elements(W + "tr"))
                {
                    var cells = row.Elements(W + "tc").ToList();
                    var texts = cells.Select(CellText).ToList();
                    var headers = texts.Select(t => t.Trim().TrimEnd(':').ToLowerInvariant()).ToList();
                    var q = headers.FindIndex(t => t is "sual" or "suallar" or "question");
                    var a = headers.FindIndex(t => t is "cavab" or "cavablar" or "izah" or "i̇zah" or "answer");
                    if (q >= 0 && a >= 0)
                    {
                        questionColumn = q; answerColumn = a;
                        numberColumn = headers.FindIndex(t => t is "№" or "no" or "nömrə" or "#");
                        continue;
                    }
                    var numberedCells = cells.Count >= 3 && NumberOnly().IsMatch(texts[0]);
                    var paired = cells.Count == 2 && QuestionStart().IsMatch(texts[0]) && !OptionStart().IsMatch(texts[1]);
                    if ((questionColumn >= 0 && questionColumn < cells.Count && answerColumn < cells.Count) || numberedCells || paired)
                    {
                        var qi = questionColumn >= 0 ? questionColumn : numberedCells ? 1 : 0;
                        var ai = answerColumn >= 0 ? answerColumn : numberedCells ? 2 : 1;
                        var ni = numberColumn >= 0 ? numberColumn : numberedCells ? 0 : -1;
                        var questionLines = new List<TextLine>();
                        foreach (var child in cells[qi].Elements()) ReadBlock(child, questionLines, ct);
                        if (questionLines.Count == 0) continue;
                        if (!QuestionStart().IsMatch(questionLines[0].Text))
                        {
                            var number = ni >= 0 && ni < texts.Count && int.TryParse(texts[ni].TrimEnd('.', ')'), out var n) ? n : _nextQuestion;
                            questionLines[0] = new TextLine(1, [TextToken.Plain($"{number}."), .. questionLines[0].Tokens]);
                        }
                        lines.AddRange(questionLines);
                        if (questionColumn >= 0 || !OptionStart().IsMatch(texts[ai]))
                            lines.Add(new TextLine(1, [TextToken.Plain("Cavab:")]));
                        foreach (var index in new[] { ai }.Concat(Enumerable.Range(0, cells.Count).Where(c => c != qi && c != ni && c != ai)))
                            foreach (var child in cells[index].Elements()) ReadBlock(child, lines, ct);
                        TrackNumber(questionLines[0].Text);
                    }
                    else
                    {
                        foreach (var cell in cells)
                            foreach (var child in cell.Elements()) ReadBlock(child, lines, ct);
                    }
                }
            }
            else if (block.Name != W + "del")
            {
                foreach (var child in block.Elements()) ReadBlock(child, lines, ct);
            }
        }

        private static string CellText(XElement cell) => string.Join(" ", cell.Descendants(W + "t")
            .Where(t => !t.Ancestors(W + "del").Any()).Select(t => t.Value)).Trim();

        private void TrackNumber(string text)
        {
            var number = Regex.Match(text, @"^(?:sual\s*)?(\d{1,4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (number.Success && int.TryParse(number.Groups[1].Value, out var n)) _nextQuestion = n + 1;
        }

        private string? ListPrefix(XElement paragraph, List<XElement> paragraphStyles)
        {
            var properties = new[] { paragraph.Element(W + "pPr") }.Concat(paragraphStyles.Select(s => s.Element(W + "pPr"))).ToList();
            var id = properties.Select(p => (string?)p?.Element(W + "numPr")?.Element(W + "numId")?.Attribute(W + "val")).FirstOrDefault(v => v is not null);
            if (id is null or "0") return null;
            var levelText = properties.Select(p => (string?)p?.Element(W + "numPr")?.Element(W + "ilvl")?.Attribute(W + "val")).FirstOrDefault(v => v is not null);
            var level = int.TryParse(levelText, out var l) ? Math.Clamp(l, 0, 8) : 0;
            var num = numbering?.Root?.Elements(W + "num").FirstOrDefault(e => (string?)e.Attribute(W + "numId") == id);
            var abstractId = (string?)num?.Element(W + "abstractNumId")?.Attribute(W + "val");
            var definition = numbering?.Root?.Elements(W + "abstractNum").FirstOrDefault(e => (string?)e.Attribute(W + "abstractNumId") == abstractId);
            var over = num?.Elements(W + "lvlOverride").FirstOrDefault(e => (string?)e.Attribute(W + "ilvl") == level.ToString());
            var lvl = over?.Element(W + "lvl") ?? definition?.Elements(W + "lvl").FirstOrDefault(e => (string?)e.Attribute(W + "ilvl") == level.ToString());
            var startValue = (string?)over?.Element(W + "startOverride")?.Attribute(W + "val") ?? (string?)lvl?.Element(W + "start")?.Attribute(W + "val");
            var key = (id, level);
            var value = _counters.TryGetValue(key, out var count) ? count + 1 : int.TryParse(startValue, out var start) ? start : 1;
            _counters[key] = value;
            foreach (var deeper in _counters.Keys.Where(k => k.Id == id && k.Level > level).ToList()) _counters.Remove(deeper);
            var format = (string?)lvl?.Element(W + "numFmt")?.Attribute(W + "val");
            return format switch
            {
                "lowerLetter" or "upperLetter" when value is >= 1 and <= 6 => $"{(char)('A' + value - 1)})",
                "bullet" => "•",
                "decimal" or "decimalZero" when level == 0 => $"{value}.",
                _ => null,
            };
        }

        private void ReadParagraph(XElement paragraph, List<TextLine> lines)
        {
            var styleId = (string?)paragraph.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")
                ?? (string?)styles?.Root?.Elements(W + "style").FirstOrDefault(s =>
                    (string?)s.Attribute(W + "type") == "paragraph" && (string?)s.Attribute(W + "default") is "1" or "true")?.Attribute(W + "styleId");
            var paragraphStyles = StyleChain(styleId).ToList();
            var tokens = new List<TextToken>();
            var word = new StringBuilder();
            var boldCount = 0;
            var colors = new Dictionary<string, int>();
            void FlushWord()
            {
                if (word.Length == 0) return;
                tokens.Add(new TextToken(TextNormalizer.Normalize(word.ToString()), boldCount * 2 >= word.Length,
                    colors.OrderByDescending(c => c.Value).First().Key));
                word.Clear(); boldCount = 0; colors.Clear();
            }
            void FlushLine()
            {
                FlushWord();
                if (tokens.Count > 0) { lines.Add(new TextLine(1, tokens.ToArray())); tokens.Clear(); }
            }
            var prefix = ListPrefix(paragraph, paragraphStyles);
            if (prefix is not null) tokens.Add(TextToken.Plain(prefix));
            foreach (var run in paragraph.Descendants(W + "r").Where(r => r.Ancestors(W + "p").FirstOrDefault() == paragraph && !r.Ancestors(W + "del").Any()))
            {
                var runProperties = run.Element(W + "rPr");
                var properties = new[] { runProperties }
                    .Concat(StyleChain((string?)runProperties?.Element(W + "rStyle")?.Attribute(W + "val")).Select(s => s.Element(W + "rPr")))
                    .Concat(paragraphStyles.Select(s => s.Element(W + "rPr")))
                    .Append(styles?.Root?.Element(W + "docDefaults")?.Element(W + "rPrDefault")?.Element(W + "rPr")).ToList();
                var bold = properties.Select(p => p?.Element(W + "b")).FirstOrDefault(p => p is not null);
                var isBold = bold is not null && (string?)bold.Attribute(W + "val") is not ("0" or "false" or "off");
                var color = properties.Select(p => (string?)p?.Element(W + "color")?.Attribute(W + "val")).FirstOrDefault(c => c is not null)?.ToUpperInvariant();
                color = color is null or "AUTO" or "000000" ? TextToken.DefaultColor : color;
                foreach (var element in run.Descendants().Where(e => e.Name == W + "t" || e.Name == W + "tab" || e.Name == W + "br" || e.Name == W + "cr"))
                {
                    if (element.Name == W + "br" || element.Name == W + "cr") { FlushLine(); continue; }
                    var text = element.Name == W + "tab" ? " " : element.Value;
                    foreach (var c in text)
                    {
                        if (char.IsWhiteSpace(c)) { FlushWord(); continue; }
                        word.Append(c);
                        if (isBold) boldCount++;
                        colors[color] = colors.GetValueOrDefault(color) + 1;
                    }
                }
            }
            FlushLine();
            if (lines.Count > 0) TrackNumber(lines[^1].Text);
        }
    }
}

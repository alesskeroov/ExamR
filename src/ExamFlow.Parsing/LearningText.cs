using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ExamFlow.Parsing;

/// <summary>One paragraph of source text with its position: page for PDF, section heading for DOCX/TXT.</summary>
public sealed record TextBlock(int Paragraph, int? Page, string? Section, string Text)
{
    /// <summary>DOCX: the outline level of a heading paragraph (1 = top, 0 = the document title), from its Word style; null for body text and PDF.</summary>
    public int? HeadingLevel { get; init; }
    /// <summary>DOCX/PPTX: phrases the author set in bold inside running text (not fully bold paragraphs), in order.</summary>
    public IReadOnlyList<string> Bold { get; init; } = [];
    /// <summary>PPTX: bullet indent level of a slide text paragraph (0 = top); null for titles, notes, tables and other formats.</summary>
    public int? Level { get; init; }
}

public enum TextExtractionFailure { None, Unreadable, Encrypted, NoText, TooLarge }

public sealed class ExtractedText
{
    public required IReadOnlyList<TextBlock> Blocks { get; init; }
    public int? PageCount { get; init; }
    public long Characters { get; init; }
    public TextExtractionFailure Failure { get; init; }
    /// <summary>Most pages have images but almost no text: a scanned document that needs OCR.</summary>
    public bool LooksScanned { get; init; }
    /// <summary>Pages (PDF) or slides (PPTX) with images but almost no text: their content is only in the picture.</summary>
    public IReadOnlyList<int> VisualPages { get; init; } = [];

    public static ExtractedText Failed(TextExtractionFailure failure, int? pages = null, bool scanned = false) =>
        new() { Blocks = [], PageCount = pages, Failure = failure, LooksScanned = scanned };
}

/// <summary>Limits that keep a hostile or huge file from exhausting memory.</summary>
public sealed record TextExtractionLimits(int MaxPages = 1000, long MaxCharacters = 5_000_000, long MaxDocxExpandedBytes = 64 * 1024 * 1024);

/// <summary>Plain text and structure for the learning tutor (not the exam question parser).</summary>
public static partial class LearningTextExtractor
{
    public static ExtractedText Extract(Stream content, string extension, TextExtractionLimits limits, CancellationToken ct = default) =>
        extension.ToLowerInvariant() switch
        {
            ".pdf" => FromPdf(content, limits, ct),
            ".docx" => FromDocx(content, limits, ct),
            ".pptx" => FromPptx(content, limits, ct),
            ".txt" => FromTxt(content, limits, ct),
            _ => ExtractedText.Failed(TextExtractionFailure.Unreadable),
        };

    public static ExtractedText FromPdf(Stream pdf, TextExtractionLimits limits, CancellationToken ct = default)
    {
        PdfDocument document;
        try { document = PdfDocument.Open(pdf); }
        catch (Exception ex) when (ex.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase)) { return ExtractedText.Failed(TextExtractionFailure.Encrypted); }
        catch { return ExtractedText.Failed(TextExtractionFailure.Unreadable); }

        using (document)
        {
            if (document.IsEncrypted) return ExtractedText.Failed(TextExtractionFailure.Encrypted);
            var pages = document.NumberOfPages;
            if (pages > limits.MaxPages) return ExtractedText.Failed(TextExtractionFailure.TooLarge, pages);

            var blocks = new List<TextBlock>();
            long characters = 0;
            var visualPages = new List<int>();
            var textPages = 0;
            for (var number = 1; number <= pages; number++)
            {
                ct.ThrowIfCancellationRequested();
                var page = document.GetPage(number);
                var text = ContentOrderTextExtractor.GetText(page);
                var letters = text.Count(char.IsLetterOrDigit);
                if (letters < MinPageLetters && page.NumberOfImages > 0) visualPages.Add(number);
                if (letters >= MinPageLetters) textPages++;
                foreach (var paragraph in Paragraphs(text))
                {
                    characters += paragraph.Length;
                    if (characters > limits.MaxCharacters) return ExtractedText.Failed(TextExtractionFailure.TooLarge, pages);
                    blocks.Add(new TextBlock(blocks.Count + 1, number, null, paragraph));
                }
            }

            // Decided per page: slides full of diagrams keep their text pages. Only a document where (almost) no page has
            // text is a scan; picture pages are listed so they can be read from the image later.
            var scanned = pages > 0 && visualPages.Count * 2 >= pages;
            if (blocks.Count == 0 || (visualPages.Count > 0 && textPages * 5 < pages))
                return ExtractedText.Failed(TextExtractionFailure.NoText, pages, visualPages.Count > 0);
            return new ExtractedText { Blocks = blocks, PageCount = pages, Characters = characters, LooksScanned = scanned, VisualPages = visualPages };
        }
    }

    /// <summary>A page or slide with fewer letters and digits than this (and an image) carries its content in the image.</summary>
    private const int MinPageLetters = 20;

    public static ExtractedText FromDocx(Stream docx, TextExtractionLimits limits, CancellationToken ct = default)
    {
        XDocument xml;
        XDocument? styles = null;
        try
        {
            using var archive = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 2048) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            // Zip-bomb guard: declared sizes first, then the real decompressed bytes are counted while reading.
            if (archive.Entries.Sum(e => e.Length) > limits.MaxDocxExpandedBytes) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null) return ExtractedText.Failed(TextExtractionFailure.Unreadable);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var bounded = new BoundedStream(entry.Open(), limits.MaxDocxExpandedBytes))
                xml = XDocument.Load(XmlReader.Create(bounded, settings));
            if (archive.GetEntry("word/styles.xml") is { } stylesEntry)
            {
                using var bounded = new BoundedStream(stylesEntry.Open(), limits.MaxDocxExpandedBytes);
                styles = XDocument.Load(XmlReader.Create(bounded, settings));
            }
        }
        catch (InvalidDataException ex) when (ex.Message == BoundedStream.LimitMessage) { return ExtractedText.Failed(TextExtractionFailure.TooLarge); }
        catch { return ExtractedText.Failed(TextExtractionFailure.Unreadable); }

        var body = xml.Root?.Element(W + "body");
        if (body is null) return ExtractedText.Failed(TextExtractionFailure.Unreadable);
        var blocks = new List<TextBlock>();
        var styleInfo = StyleInfo(styles);
        string? section = null;
        long characters = 0;
        var tooLarge = false;
        void Add(string text, int? headingLevel, IReadOnlyList<string>? bold = null)
        {
            if (text.Length == 0 || tooLarge) return;
            if (headingLevel is not null) section = text.Length > 200 ? text[..200] : text;
            characters += text.Length;
            if (characters > limits.MaxCharacters) { tooLarge = true; return; }
            blocks.Add(new TextBlock(blocks.Count + 1, null, section, text) { HeadingLevel = headingLevel, Bold = headingLevel is null ? bold ?? [] : [] });
        }

        // Body order: paragraphs, tables (one block per row) and content controls; a table of contents is skipped.
        void Walk(XElement container)
        {
            foreach (var element in container.Elements())
            {
                ct.ThrowIfCancellationRequested();
                if (element.Name == W + "p")
                {
                    var properties = element.Element(W + "pPr");
                    var style = properties?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? "";
                    var info = styleInfo.GetValueOrDefault(style, DefaultStyle(style));
                    if (info.Toc) continue;
                    // A paragraph's own outline level wins over its style's.
                    var level = int.TryParse(properties?.Element(W + "outlineLvl")?.Attribute(W + "val")?.Value, out var outline) && outline < 9
                        ? outline + 1 : info.HeadingLevel;
                    var text = ParagraphText(element, out var bold);
                    Add(text, level, bold);
                }
                else if (element.Name == W + "tbl") Table(element);
                else if (element.Name == W + "sdt")
                {
                    var gallery = element.Element(W + "sdtPr")?.Descendants(W + "docPartGallery").FirstOrDefault()?.Attribute(W + "val")?.Value;
                    if (gallery?.Contains("Table of Contents", StringComparison.OrdinalIgnoreCase) == true) continue;
                    if (element.Element(W + "sdtContent") is { } content) Walk(content);
                }
                else if (element.Name == W + "customXml" || element.Name == W + "smartTag") Walk(element);
            }
        }

        // A table with two or more columns becomes "▦ cell | cell | …" rows, so cards can rebuild it; a one-column
        // (layout) table is ordinary text.
        void Table(XElement table)
        {
            var rows = table.Elements(W + "tr")
                .Select(tr => tr.Elements(W + "tc").Select(tc => Normalize(string.Join(' ', tc.Descendants(W + "p").Select(ParagraphText))).Replace('|', '/')).ToList())
                .Where(cells => cells.Any(c => c.Length > 0)).ToList();
            if (rows.Count == 0) return;
            if (rows.Max(r => r.Count) < 2)
            {
                foreach (var cell in rows.SelectMany(r => r)) Add(cell, null);
                return;
            }
            for (var i = 0; i < rows.Count; i++) Add((i == 0 ? TableStartMark : TableRowMark) + string.Join(" | ", rows[i]), null);
        }

        Walk(body);
        if (tooLarge) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
        return blocks.Count == 0 ? ExtractedText.Failed(TextExtractionFailure.NoText) : new ExtractedText { Blocks = blocks, Characters = characters };
    }

    private readonly record struct ParagraphStyle(int? HeadingLevel, bool Toc);

    /// <summary>
    /// Heading level and table-of-contents flag per paragraph style id. Localized Word files use ids such as "2" or "21",
    /// so the style's name ("heading 2", "toc 2", "Title") and outline level decide, not the id.
    /// </summary>
    private static Dictionary<string, ParagraphStyle> StyleInfo(XDocument? styles)
    {
        var map = new Dictionary<string, ParagraphStyle>(StringComparer.Ordinal);
        if (styles?.Root is null) return map;
        foreach (var style in styles.Root.Elements(W + "style").Where(s => (string?)s.Attribute(W + "type") == "paragraph"))
        {
            var id = (string?)style.Attribute(W + "styleId");
            if (id is null) continue;
            var name = ((string?)style.Element(W + "name")?.Attribute(W + "val") ?? "").Trim().ToLowerInvariant();
            int? level = name == "title" ? 0
                : HeadingName().Match(name) is { Success: true } m ? int.Parse(m.Groups[1].Value)
                : int.TryParse((string?)style.Element(W + "pPr")?.Element(W + "outlineLvl")?.Attribute(W + "val"), out var outline) && outline < 9 ? outline + 1
                : null;
            map[id] = new(level, name.StartsWith("toc", StringComparison.Ordinal));
        }
        return map;
    }

    /// <summary>Without styles.xml: the English built-in ids.</summary>
    private static ParagraphStyle DefaultStyle(string id) =>
        id.StartsWith("TOC", StringComparison.OrdinalIgnoreCase) ? new(null, true)
        : id.Equals("Title", StringComparison.OrdinalIgnoreCase) ? new(0, false)
        : HeadingId().Match(id) is { Success: true } m ? new(int.Parse(m.Groups[1].Value), false)
        : new(null, false);

    [GeneratedRegex(@"^heading\s*([1-9])$")] private static partial Regex HeadingName();
    [GeneratedRegex(@"^Heading([1-9])$", RegexOptions.IgnoreCase)] private static partial Regex HeadingId();

    /// <summary>Starts a block that is one row of a Word table; cells are separated by " | ".</summary>
    public const string TableRowMark = "▦ ";
    public const string TableStartMark = "▦▦ ";

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    /// <summary>
    /// Visible paragraph text. Text inside drawings, text boxes and shapes (diagram labels, often stored twice through
    /// mc:AlternateContent) is left out: on its own it is a list of fragments without meaning.
    /// </summary>
    private static string ParagraphText(XElement paragraph) => ParagraphText(paragraph, out _);

    /// <param name="bold">Phrases of runs set in bold (direct formatting or a "Strong" character style).</param>
    private static string ParagraphText(XElement paragraph, out IReadOnlyList<string> bold)
    {
        var text = new StringBuilder();
        var ranges = new List<(int Start, int End)>();
        void Collect(XElement element)
        {
            foreach (var child in element.Elements())
            {
                var name = child.Name;
                if (name == W + "drawing" || name == W + "pict" || name == W + "txbxContent" || name == W + "object" || name.Namespace == Mc) continue;
                // Word equations: kept as linear text instead of disappearing from the sentence.
                if (name == MathText.M + "oMath" || name == MathText.M + "oMathPara") text.Append(' ').Append(MathText.Linear(child)).Append(' ');
                else if (name == W + "t") text.Append(child.Value);
                else if (name == W + "tab") text.Append('\t');
                else if (name == W + "br" || name == W + "cr") text.Append('\n');
                else if (name == W + "r")
                {
                    var start = text.Length;
                    Collect(child);
                    if (IsBoldRun(child.Element(W + "rPr"))) ranges.Add((start, text.Length));
                }
                else Collect(child);
            }
        }
        Collect(paragraph);
        var raw = text.ToString();
        bold = BoldPhrases(raw, ranges);
        return Normalize(raw);
    }

    private static bool IsBoldRun(XElement? properties)
    {
        if (properties is null) return false;
        if (properties.Element(W + "rStyle")?.Attribute(W + "val")?.Value.Contains("Strong", StringComparison.OrdinalIgnoreCase) == true) return true;
        return properties.Element(W + "b") is { } b && (string?)b.Attribute(W + "val") is null or "1" or "true" or "on";
    }

    /// <summary>
    /// Bold runs of one paragraph as phrases: runs split only by spaces are one phrase. Nothing when (almost) the whole
    /// paragraph is bold — that is a heading or a highlighted paragraph, not a marked term.
    /// </summary>
    private static IReadOnlyList<string> BoldPhrases(string raw, List<(int Start, int End)> ranges)
    {
        if (ranges.Count == 0) return [];
        var merged = new List<(int Start, int End)>();
        foreach (var range in ranges.Where(r => r.End > r.Start))
        {
            if (merged.Count > 0 && string.IsNullOrWhiteSpace(raw[merged[^1].End..range.Start])) merged[^1] = (merged[^1].Start, range.End);
            else merged.Add(range);
        }
        var letters = raw.Count(char.IsLetterOrDigit);
        var boldLetters = merged.Sum(r => raw[r.Start..r.End].Count(char.IsLetterOrDigit));
        if (letters == 0 || boldLetters >= letters * 0.9) return [];
        return merged.Select(r => Balanced(Normalize(raw[r.Start..r.End]).Trim(' ', ',', ';', ':', '.', '"', '“', '”', '«', '»', '–', '-')))
            .Where(p => p.Count(char.IsLetter) >= 2).Distinct().ToList();
    }

    /// <summary>"virtuallaşdırma (virtualization" → "virtuallaşdırma"; a bracket the bold run cut in half is dropped with its tail.</summary>
    private static string Balanced(string phrase)
    {
        var open = phrase.LastIndexOf('(');
        if (open >= 0 && phrase.IndexOf(')', open) < 0) phrase = phrase[..open];
        var close = phrase.IndexOf(')');
        if (close >= 0 && phrase.LastIndexOf('(', close) < 0) phrase = phrase[(close + 1)..];
        return phrase.Trim(' ', ',', ';', ':', '.', '–', '-');
    }

    public static ExtractedText FromTxt(Stream txt, TextExtractionLimits limits, CancellationToken ct = default)
    {
        string text;
        try
        {
            // Strict UTF-8 (BOM honoured): binary or legacy-encoded files are rejected instead of becoming garbage.
            using var bounded = new BoundedStream(txt, limits.MaxCharacters * 4);
            using var reader = new StreamReader(bounded, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }
        catch (InvalidDataException ex) when (ex.Message == BoundedStream.LimitMessage) { return ExtractedText.Failed(TextExtractionFailure.TooLarge); }
        catch (DecoderFallbackException) { return ExtractedText.Failed(TextExtractionFailure.Unreadable); }
        if (text.Contains('\0')) return ExtractedText.Failed(TextExtractionFailure.Unreadable);

        var blocks = new List<TextBlock>();
        string? section = null;
        long characters = 0;
        foreach (var paragraph in Paragraphs(text))
        {
            ct.ThrowIfCancellationRequested();
            // Markdown-style "# Heading" lines name the section of the paragraphs that follow.
            if (Heading().Match(paragraph) is { Success: true } heading && !paragraph.Contains('\n'))
                section = heading.Groups[1].Value.Trim();
            characters += paragraph.Length;
            if (characters > limits.MaxCharacters) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            blocks.Add(new TextBlock(blocks.Count + 1, null, section, paragraph));
        }
        return blocks.Count == 0 ? ExtractedText.Failed(TextExtractionFailure.NoText) : new ExtractedText { Blocks = blocks, Characters = characters };
    }

    /// <summary>Splits on blank lines; single line breaks inside a paragraph become spaces.</summary>
    private static IEnumerable<string> Paragraphs(string text) =>
        BlankLines().Split(text.Replace("\r\n", "\n").Replace('\r', '\n')).Select(Normalize).Where(p => p.Length > 0);

    private static string Normalize(string text) => Spaces().Replace(text.Replace('\n', ' ').Replace('\t', ' '), " ").Trim();

    [GeneratedRegex(@"\n\s*\n")] private static partial Regex BlankLines();
    [GeneratedRegex(@"[  ]{2,}")] private static partial Regex Spaces();
    [GeneratedRegex(@"^#{1,6}\s+(.+)$")] private static partial Regex Heading();
}

/// <summary>Read-only wrapper that fails once more than <paramref name="limit"/> bytes have been read.</summary>
internal sealed class BoundedStream(Stream inner, long limit) : Stream
{
    public const string LimitMessage = "Decompressed content exceeds the allowed size.";
    private long _read;

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        _read += n;
        if (_read > limit) throw new InvalidDataException(LimitMessage);
        return n;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}

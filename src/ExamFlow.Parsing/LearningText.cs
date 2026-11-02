using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ExamFlow.Parsing;

/// <summary>One paragraph of source text with its position: page for PDF, section heading for DOCX/TXT.</summary>
public sealed record TextBlock(int Paragraph, int? Page, string? Section, string Text);

public enum TextExtractionFailure { None, Unreadable, Encrypted, NoText, TooLarge }

public sealed class ExtractedText
{
    public required IReadOnlyList<TextBlock> Blocks { get; init; }
    public int? PageCount { get; init; }
    public long Characters { get; init; }
    public TextExtractionFailure Failure { get; init; }
    /// <summary>Most pages have images but almost no text: a scanned document that needs OCR.</summary>
    public bool LooksScanned { get; init; }

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
            var imageOnlyPages = 0;
            for (var number = 1; number <= pages; number++)
            {
                ct.ThrowIfCancellationRequested();
                var page = document.GetPage(number);
                var text = ContentOrderTextExtractor.GetText(page);
                if (text.Count(char.IsLetterOrDigit) < 20 && page.NumberOfImages > 0) imageOnlyPages++;
                foreach (var paragraph in Paragraphs(text))
                {
                    characters += paragraph.Length;
                    if (characters > limits.MaxCharacters) return ExtractedText.Failed(TextExtractionFailure.TooLarge, pages);
                    blocks.Add(new TextBlock(blocks.Count + 1, number, null, paragraph));
                }
            }

            var scanned = pages > 0 && imageOnlyPages * 2 >= pages;
            if (blocks.Count == 0 || (scanned && characters < pages * 200L))
                return ExtractedText.Failed(TextExtractionFailure.NoText, pages, scanned);
            return new ExtractedText { Blocks = blocks, PageCount = pages, Characters = characters, LooksScanned = scanned };
        }
    }

    public static ExtractedText FromDocx(Stream docx, TextExtractionLimits limits, CancellationToken ct = default)
    {
        XDocument xml;
        try
        {
            using var archive = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 2048) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            // Zip-bomb guard: declared sizes first, then the real decompressed bytes are counted while reading.
            if (archive.Entries.Sum(e => e.Length) > limits.MaxDocxExpandedBytes) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null) return ExtractedText.Failed(TextExtractionFailure.Unreadable);
            using var bounded = new BoundedStream(entry.Open(), limits.MaxDocxExpandedBytes);
            xml = XDocument.Load(XmlReader.Create(bounded, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }));
        }
        catch (InvalidDataException ex) when (ex.Message == BoundedStream.LimitMessage) { return ExtractedText.Failed(TextExtractionFailure.TooLarge); }
        catch { return ExtractedText.Failed(TextExtractionFailure.Unreadable); }

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var blocks = new List<TextBlock>();
        string? section = null;
        long characters = 0;
        foreach (var p in xml.Descendants(w + "p"))
        {
            ct.ThrowIfCancellationRequested();
            var text = Normalize(string.Concat(p.Descendants().Select(e => e.Name == w + "t" ? e.Value : e.Name == w + "tab" ? "\t" : e.Name == w + "br" ? "\n" : "")));
            if (text.Length == 0) continue;
            var style = p.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value ?? "";
            if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) || style.Equals("Title", StringComparison.OrdinalIgnoreCase))
                section = text.Length > 200 ? text[..200] : text;
            characters += text.Length;
            if (characters > limits.MaxCharacters) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            blocks.Add(new TextBlock(blocks.Count + 1, null, section, text));
        }
        return blocks.Count == 0 ? ExtractedText.Failed(TextExtractionFailure.NoText) : new ExtractedText { Blocks = blocks, Characters = characters };
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

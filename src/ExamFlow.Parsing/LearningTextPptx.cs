using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ExamFlow.Parsing;

public static partial class LearningTextExtractor
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRels = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Placeholders that repeat on every slide and carry no content.</summary>
    private static readonly HashSet<string> ChromePlaceholders = ["sldNum", "dt", "ftr", "hdr"];

    /// <summary>
    /// PowerPoint slides in presentation order: the slide title is a heading block (and names the section of what follows),
    /// then text boxes, tables and speaker notes. Page = slide number. Equations become linear text; pictures are not read,
    /// but slides that are (almost) only a picture are listed in <see cref="ExtractedText.VisualPages"/>.
    /// </summary>
    public static ExtractedText FromPptx(Stream pptx, TextExtractionLimits limits, CancellationToken ct = default)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var blocks = new List<TextBlock>();
        var visualPages = new List<int>();
        var textSlides = 0;
        long characters = 0;
        int slideCount;
        try
        {
            using var archive = new ZipArchive(pptx, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 4096) return ExtractedText.Failed(TextExtractionFailure.TooLarge);
            // Zip-bomb guard on the XML parts only: slide pictures may be large, but they are never decompressed here.
            if (archive.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Length) > limits.MaxDocxExpandedBytes)
                return ExtractedText.Failed(TextExtractionFailure.TooLarge);

            XDocument? Load(string path)
            {
                if (archive.GetEntry(path) is not { } entry) return null;
                using var bounded = new BoundedStream(entry.Open(), limits.MaxDocxExpandedBytes);
                return XDocument.Load(XmlReader.Create(bounded, settings));
            }

            var presentation = Load("ppt/presentation.xml");
            if (presentation?.Root is null) return ExtractedText.Failed(TextExtractionFailure.Unreadable);
            var targets = Relationships(Load("ppt/_rels/presentation.xml.rels"), "ppt/");
            var slides = presentation.Root.Element(P + "sldIdLst")?.Elements(P + "sldId")
                .Select(s => targets.GetValueOrDefault((string?)s.Attribute(R + "id") ?? ""))
                .Where(path => path is not null).Select(path => path!).ToList() ?? [];
            slideCount = slides.Count;
            if (slideCount > limits.MaxPages) return ExtractedText.Failed(TextExtractionFailure.TooLarge, slideCount);

            string? section = null;
            var tooLarge = false;
            void Add(int slide, string text, int? headingLevel, int? level = null, IReadOnlyList<string>? bold = null)
            {
                if (text.Length == 0 || tooLarge) return;
                if (headingLevel is not null) section = text.Length > 200 ? text[..200] : text;
                characters += text.Length;
                if (characters > limits.MaxCharacters) { tooLarge = true; return; }
                blocks.Add(new TextBlock(blocks.Count + 1, slide, section, text) { HeadingLevel = headingLevel, Level = level, Bold = bold ?? [] });
            }

            for (var index = 0; index < slides.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                var number = index + 1;
                var slide = Load(slides[index]);
                if (slide?.Root is null || (string?)slide.Root.Attribute("show") == "0") continue; // hidden slide
                var tree = slide.Root.Element(P + "cSld")?.Element(P + "spTree");
                if (tree is null) continue;

                var titles = new List<string>();
                var body = new List<SlideLine>();
                CollectShapes(tree, titles, body);

                var slash = slides[index].LastIndexOf('/');
                var relsPath = $"{slides[index][..(slash + 1)]}_rels/{slides[index][(slash + 1)..]}.rels";
                var notesPath = Relationships(Load(relsPath), slides[index][..(slash + 1)], "notesSlide").Values.FirstOrDefault();
                var notes = new List<string>();
                if (notesPath is not null && Load(notesPath)?.Root?.Element(P + "cSld")?.Element(P + "spTree") is { } notesTree)
                    foreach (var shape in notesTree.Descendants(P + "sp").Where(s => Placeholder(s) == "body"))
                        notes.AddRange(ShapeParagraphs(shape).Select(l => l.Text));

                var title = Normalize(string.Join(' ', titles));
                var letters = title.Count(char.IsLetterOrDigit) + body.Sum(b => b.Text.Count(char.IsLetterOrDigit));
                if (letters >= MinPageLetters) textSlides++;
                else if (tree.Descendants(A + "blip").Any()) visualPages.Add(number);

                Add(number, title, 1);
                foreach (var line in body)
                    Add(number, line.TableStart ? TableStartMark + line.Text : line.TableRow ? TableRowMark + line.Text : line.Text, null, line.Level, line.Bold);
                foreach (var note in notes) Add(number, note, null);
                if (tooLarge) return ExtractedText.Failed(TextExtractionFailure.TooLarge, slideCount);
            }
        }
        catch (InvalidDataException ex) when (ex.Message == BoundedStream.LimitMessage) { return ExtractedText.Failed(TextExtractionFailure.TooLarge); }
        catch (OperationCanceledException) { throw; }
        catch { return ExtractedText.Failed(TextExtractionFailure.Unreadable); }

        if (blocks.Count == 0 || (visualPages.Count > 0 && textSlides * 5 < slideCount))
            return ExtractedText.Failed(TextExtractionFailure.NoText, slideCount, visualPages.Count > 0);
        return new ExtractedText
        {
            Blocks = blocks, PageCount = slideCount, Characters = characters,
            LooksScanned = slideCount > 0 && visualPages.Count * 2 >= slideCount, VisualPages = visualPages,
        };
    }

    /// <summary>Title placeholders go to <paramref name="titles"/>; other text boxes, tables and groups to <paramref name="body"/> in document order.</summary>
    private static void CollectShapes(XElement container, List<string> titles, List<SlideLine> body)
    {
        foreach (var element in container.Elements())
        {
            if (element.Name == P + "sp")
            {
                var placeholder = Placeholder(element);
                if (placeholder is not null && ChromePlaceholders.Contains(placeholder)) continue;
                var paragraphs = ShapeParagraphs(element);
                if (placeholder is "title" or "ctrTitle") titles.AddRange(paragraphs.Select(l => l.Text));
                else body.AddRange(paragraphs);
            }
            else if (element.Name == P + "grpSp") CollectShapes(element, titles, body);
            else if (element.Name == P + "graphicFrame" && element.Descendants(A + "tbl").FirstOrDefault() is { } table)
            {
                var rows = table.Elements(A + "tr")
                    .Select(tr => tr.Elements(A + "tc").Select(tc => Normalize(string.Join(' ', ShapeParagraphs(tc).Select(l => l.Text))).Replace('|', '/')).ToList())
                    .Where(cells => cells.Any(c => c.Length > 0)).ToList();
                if (rows.Count == 0) continue;
                if (rows.Max(r => r.Count) < 2) body.AddRange(rows.SelectMany(r => r).Where(c => c.Length > 0).Select(c => new SlideLine(c, null, [])));
                else body.AddRange(rows.Select((r, i) => new SlideLine(string.Join(" | ", r), null, []) { TableStart = i == 0, TableRow = i > 0 }));
            }
            // Equation shapes are saved twice: as Office Math (mc:Choice) and as a picture fallback.
            else if (element.Name == Mc + "AlternateContent" && (element.Element(Mc + "Choice") ?? element.Element(Mc + "Fallback")) is { } choice)
                CollectShapes(choice, titles, body);
        }
    }

    private static string? Placeholder(XElement shape)
    {
        var ph = shape.Element(P + "nvSpPr")?.Element(P + "nvPr")?.Element(P + "ph");
        return ph is null ? null : (string?)ph.Attribute("type") ?? "body";
    }

    /// <summary>One text paragraph of a slide shape: its bullet level and bold phrases; table rows carry the table marks instead.</summary>
    private sealed record SlideLine(string Text, int? Level, IReadOnlyList<string> Bold)
    {
        public bool TableStart { get; init; }
        public bool TableRow { get; init; }
    }

    /// <summary>Non-empty paragraphs of a shape's or table cell's text body.</summary>
    private static List<SlideLine> ShapeParagraphs(XElement shape) =>
        (shape.Element(P + "txBody") ?? shape.Element(A + "txBody"))?.Elements(A + "p")
            .Select(p => new SlideLine(DrawingParagraphText(p, out var bold),
                int.TryParse((string?)p.Element(A + "pPr")?.Attribute("lvl"), out var level) ? Math.Clamp(level, 0, 8) : 0, bold))
            .Where(l => l.Text.Length > 0).ToList() ?? [];

    private static string DrawingParagraphText(XElement paragraph, out IReadOnlyList<string> bold)
    {
        var text = new System.Text.StringBuilder();
        var ranges = new List<(int Start, int End)>();
        void Collect(XElement element)
        {
            foreach (var child in element.Elements())
            {
                if (child.Name == A + "r" || child.Name == A + "fld")
                {
                    var start = text.Length;
                    text.Append(child.Element(A + "t")?.Value);
                    if ((string?)child.Element(A + "rPr")?.Attribute("b") is "1" or "true") ranges.Add((start, text.Length));
                }
                else if (child.Name == A + "br") text.Append(' ');
                else if (child.Name == Mc + "AlternateContent") { if (child.Element(Mc + "Choice") is { } choice) Collect(choice); }
                else if (child.Name == MathText.M + "oMathPara" || child.Name == MathText.M + "oMath") text.Append(' ').Append(MathText.Linear(child)).Append(' ');
                else if (child.Name.Namespace != A) Collect(child); // a14:m and other wrappers around equations
            }
        }
        Collect(paragraph);
        var raw = text.ToString();
        bold = BoldPhrases(raw, ranges);
        return Normalize(raw);
    }

    /// <summary>Relationship id → package path. Targets are relative to <paramref name="baseDir"/> ("ppt/", "ppt/slides/").</summary>
    private static Dictionary<string, string> Relationships(XDocument? rels, string baseDir, string? typeSuffix = null)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rels?.Root is null) return map;
        foreach (var rel in rels.Root.Elements(PackageRels + "Relationship"))
        {
            var id = (string?)rel.Attribute("Id");
            var target = (string?)rel.Attribute("Target");
            if (id is null || target is null || (string?)rel.Attribute("TargetMode") == "External") continue;
            if (typeSuffix is not null && ((string?)rel.Attribute("Type"))?.EndsWith("/" + typeSuffix, StringComparison.Ordinal) != true) continue;
            map[id] = ResolvePath(baseDir, target);
        }
        return map;
    }

    private static string ResolvePath(string baseDir, string target)
    {
        if (target.StartsWith('/')) return target.TrimStart('/');
        var parts = baseDir.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var part in target.Split('/'))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part is not ("." or "")) parts.Add(part);
        }
        return string.Join('/', parts);
    }
}

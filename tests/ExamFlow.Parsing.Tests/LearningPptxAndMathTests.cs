using System.IO.Compression;
using ExamFlow.Parsing;

namespace ExamFlow.Parsing.Tests;

public class LearningPptxAndMathTests
{
    private static readonly TextExtractionLimits Limits = new();

    private const string Ns = """xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" xmlns:a14="http://schemas.microsoft.com/office/drawing/2010/main" """;
    private const string Rels = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string M = """xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math" """;

    private static MemoryStream Zip(params (string Path, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, content) in entries)
                using (var writer = new StreamWriter(zip.CreateEntry(path).Open()))
                    writer.Write(content);
        stream.Position = 0;
        return stream;
    }

    /// <summary>A presentation whose slides are given as spTree inner XML (null = hidden slide); notes per slide optional.</summary>
    private static MemoryStream Pptx(IReadOnlyList<string?> slides, IReadOnlyDictionary<int, string>? notes = null)
    {
        var entries = new List<(string, string)>();
        var ids = string.Join("", slides.Select((_, i) => $"""<p:sldId id="{256 + i}" r:id="rId{i + 10}"/>"""));
        entries.Add(("ppt/presentation.xml", $"""<p:presentation {Ns}><p:sldIdLst>{ids}</p:sldIdLst></p:presentation>"""));
        // Slides listed in reverse in the rels: order must come from sldIdLst, not from file names.
        var rels = string.Join("", slides.Select((_, i) => $"""<Relationship Id="rId{i + 10}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide{slides.Count - i}.xml"/>"""));
        entries.Add(("ppt/_rels/presentation.xml.rels", $"""<Relationships xmlns="{Rels}">{rels}</Relationships>"""));
        for (var i = 0; i < slides.Count; i++)
        {
            var file = slides.Count - i;
            var show = slides[i] is null ? " show=\"0\"" : "";
            entries.Add(($"ppt/slides/slide{file}.xml", $"""<p:sld {Ns}{show}><p:cSld><p:spTree>{slides[i] ?? Text("Gizli slayd mətni")}</p:spTree></p:cSld></p:sld>"""));
            if (notes?.TryGetValue(i, out var note) == true)
            {
                entries.Add(($"ppt/slides/_rels/slide{file}.xml.rels", $"""<Relationships xmlns="{Rels}"><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide" Target="../notesSlides/notesSlide{file}.xml"/></Relationships>"""));
                entries.Add(($"ppt/notesSlides/notesSlide{file}.xml", $"""<p:notes {Ns}><p:cSld><p:spTree><p:sp><p:nvSpPr><p:cNvPr id="2" name="x"/><p:cNvSpPr/><p:nvPr><p:ph type="sldImg"/></p:nvPr></p:nvSpPr></p:sp>{Text(note, "body")}</p:spTree></p:cSld></p:notes>"""));
            }
        }
        return Zip([.. entries]);
    }

    private static string Text(string text, string? placeholder = null, params string[] more)
    {
        var ph = placeholder is null ? "" : $"""<p:ph type="{placeholder}"/>""";
        var paragraphs = string.Join("", new[] { text }.Concat(more).Select(t => $"<a:p><a:r><a:t>{t}</a:t></a:r></a:p>"));
        return $"""<p:sp><p:nvSpPr><p:cNvPr id="1" name="s"/><p:cNvSpPr/><p:nvPr>{ph}</p:nvPr></p:nvSpPr><p:txBody>{paragraphs}</p:txBody></p:sp>""";
    }

    private const string Picture = """<p:pic><p:blipFill><a:blip r:embed="rId5"/></p:blipFill></p:pic>""";

    [Fact]
    public void Pptx_slides_in_order_with_titles_bullets_tables_and_notes()
    {
        var table = """<p:graphicFrame><a:graphic><a:graphicData><a:tbl><a:tr><a:tc><a:txBody><a:p><a:r><a:t>Qrup</a:t></a:r></a:p></a:txBody></a:tc><a:tc><a:txBody><a:p><a:r><a:t>Hidrid</a:t></a:r></a:p></a:txBody></a:tc></a:tr><a:tr><a:tc><a:txBody><a:p><a:r><a:t>1</a:t></a:r></a:p></a:txBody></a:tc><a:tc><a:txBody><a:p><a:r><a:t>NaH</a:t></a:r></a:p></a:txBody></a:tc></a:tr></a:tbl></a:graphicData></a:graphic></p:graphicFrame>""";
        var slides = new string?[]
        {
            Text("Hidridlər haqqında giriş", "title") + Text("5", "sldNum"),
            // Body before the title in the file: the title still comes first.
            Text("Hidrid ionu elektron verir.", "body", "Qrup 1 hidridləri güclü reduksiyaedicidir.") + Text("Reaksiyalar", "title") + table,
            null,
        };
        var result = LearningTextExtractor.FromPptx(Pptx(slides, new Dictionary<int, string> { [1] = "Mühazirədə bunu misalla izah et." }), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Equal(3, result.PageCount);
        Assert.Equal(
            ["Hidridlər haqqında giriş", "Reaksiyalar", "Hidrid ionu elektron verir.", "Qrup 1 hidridləri güclü reduksiyaedicidir.",
             LearningTextExtractor.TableStartMark + "Qrup | Hidrid", LearningTextExtractor.TableRowMark + "1 | NaH", "Mühazirədə bunu misalla izah et."],
            result.Blocks.Select(b => b.Text));
        Assert.Equal([1, 2, 2, 2, 2, 2, 2], result.Blocks.Select(b => b.Page ?? 0));
        Assert.Equal(1, result.Blocks[1].HeadingLevel);
        Assert.All(result.Blocks.Skip(1), b => Assert.Equal("Reaksiyalar", b.Section));
        Assert.DoesNotContain(result.Blocks, b => b.Text.Contains("Gizli") || b.Text == "5");
    }

    [Fact]
    public void Pptx_picture_only_deck_is_reported_as_scanned()
    {
        var slides = Enumerable.Range(0, 6).Select(_ => (string?)Picture).ToList();
        slides[0] = Text("TOPIC 4", "ctrTitle") + Picture;
        var result = LearningTextExtractor.FromPptx(Pptx(slides), Limits);

        Assert.Equal(TextExtractionFailure.NoText, result.Failure);
        Assert.True(result.LooksScanned);
    }

    [Fact]
    public void Pptx_text_slides_are_kept_and_picture_slides_listed()
    {
        var slides = new string?[]
        {
            Text("Kimyəvi rabitə növləri", "title") + Text("İon rabitəsi metal və qeyri-metal arasında yaranır."),
            Picture,
            Text("Kovalent rabitə", "title") + Text("Elektron cütləri ortaqlaşdırılır, məsələn H₂ molekulunda."),
        };
        var result = LearningTextExtractor.FromPptx(Pptx(slides), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Equal([2], result.VisualPages);
        Assert.False(result.LooksScanned);
    }

    [Fact]
    public void Pptx_equation_shape_is_linear_text()
    {
        var equation = $"""
            <mc:AlternateContent><mc:Choice Requires="a14"><p:sp><p:nvSpPr><p:cNvPr id="3" name="e"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
            <p:txBody><a:p><a14:m><m:oMathPara><m:oMath><m:sSub><m:e><m:r><m:t>H</m:t></m:r></m:e><m:sub><m:r><m:t>2</m:t></m:r></m:sub></m:sSub><m:r><m:t>O</m:t></m:r></m:oMath></m:oMathPara></a14:m></a:p></p:txBody></p:sp></mc:Choice>
            <mc:Fallback>{Picture}</mc:Fallback></mc:AlternateContent>
            """;
        var result = LearningTextExtractor.FromPptx(Pptx([Text("Su molekulunun formulu belədir", "title") + equation]), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Contains(result.Blocks, b => b.Text == "H₂O");
    }

    [Fact]
    public void Pptx_that_is_not_a_presentation_is_unreadable()
    {
        Assert.Equal(TextExtractionFailure.Unreadable, LearningTextExtractor.FromPptx(Zip(("word/document.xml", "<x/>")), Limits).Failure);
        Assert.Equal(TextExtractionFailure.Unreadable, LearningTextExtractor.FromPptx(new MemoryStream([1, 2, 3]), Limits).Failure);
    }

    [Theory]
    [InlineData("<m:f><m:num><m:r><m:t>a+b</m:t></m:r></m:num><m:den><m:r><m:t>2</m:t></m:r></m:den></m:f>", "(a+b)/2")]
    [InlineData("<m:sSup><m:e><m:r><m:t>x</m:t></m:r></m:e><m:sup><m:r><m:t>2</m:t></m:r></m:sup></m:sSup>", "x²")]
    [InlineData("<m:sSup><m:e><m:r><m:t>e</m:t></m:r></m:e><m:sup><m:r><m:t>-kt</m:t></m:r></m:sup></m:sSup>", "e^(-kt)")]
    [InlineData("<m:sSup><m:e><m:r><m:t>Na</m:t></m:r></m:e><m:sup><m:r><m:t>+</m:t></m:r></m:sup></m:sSup>", "Na⁺")]
    [InlineData("<m:sSub><m:e><m:r><m:t>a</m:t></m:r></m:e><m:sub><m:r><m:t>ij</m:t></m:r></m:sub></m:sSub>", "a_(ij)")]
    [InlineData("<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e><m:r><m:t>x</m:t></m:r></m:e></m:rad>", "√(x)")]
    [InlineData("<m:nary><m:naryPr><m:chr m:val=\"∑\"/></m:naryPr><m:sub><m:r><m:t>i=1</m:t></m:r></m:sub><m:sup><m:r><m:t>n</m:t></m:r></m:sup><m:e><m:r><m:t>i</m:t></m:r></m:e></m:nary>", "∑_(i=1)ⁿ i")]
    [InlineData("<m:d><m:e><m:r><m:t>x</m:t></m:r></m:e><m:e><m:r><m:t>y</m:t></m:r></m:e></m:d>", "(x,y)")]
    public void Office_math_becomes_linear_text(string inner, string expected)
    {
        var element = System.Xml.Linq.XElement.Parse($"<m:oMath {M}>{inner}</m:oMath>");
        Assert.Equal(expected, MathText.Linear(element));
    }

    [Fact]
    public void Docx_equation_stays_in_its_sentence()
    {
        var body = $"""<w:p {M}><w:r><w:t xml:space="preserve">Dairənin sahəsi </w:t></w:r><m:oMath><m:r><m:t>S=π</m:t></m:r><m:sSup><m:e><m:r><m:t>r</m:t></m:r></m:e><m:sup><m:r><m:t>2</m:t></m:r></m:sup></m:sSup></m:oMath><w:r><w:t xml:space="preserve"> düsturu ilə tapılır.</w:t></w:r></w:p>""";
        var docx = Zip(("word/document.xml", $"""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>"""));
        var result = LearningTextExtractor.FromDocx(docx, Limits);

        Assert.Equal("Dairənin sahəsi S=πr² düsturu ilə tapılır.", Assert.Single(result.Blocks).Text);
    }

    [Fact]
    public void Pdf_with_some_picture_pages_is_kept()
    {
        var pdf = new TestPdf()
            .Line("The first page holds the main text of the lecture and is long enough.")
            .NewPage().Image(new TestImage(200, 150))
            .NewPage().Image(new TestImage(200, 150))
            .NewPage().Line("The fourth page again has readable text with the conclusions.")
            .Build();
        var result = LearningTextExtractor.FromPdf(new MemoryStream(pdf), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Equal([2, 3], result.VisualPages);
    }

    [Fact]
    public void Pdf_of_picture_pages_only_is_scanned()
    {
        var pdf = new TestPdf().Image(new TestImage(200, 150)).NewPage().Image(new TestImage(200, 150)).Build();
        var result = LearningTextExtractor.FromPdf(new MemoryStream(pdf), Limits);

        Assert.Equal(TextExtractionFailure.NoText, result.Failure);
        Assert.True(result.LooksScanned);
    }
}

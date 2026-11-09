using ExamFlow.Api.Features.Learning;
using ExamFlow.Parsing;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExamFlow.Api.Tests;

public class OpenQuestionClassifierTests
{
    private const string Answer = "This answer explains the idea in enough words to be a real answer.";

    private static LearningDocumentChunk Chunk(string text, int ordinal = 0, int? page = 1) =>
        new() { Ordinal = ordinal, Text = text, PageFrom = page, PageTo = page };

    private static List<LearningDocumentChunk> OpenChunks(int count, int from = 1) =>
        [Chunk(string.Join("\n\n", Enumerable.Range(from, count).Select(n => $"{n}. What is topic {n}?\n\n{Answer} Topic {n}.")))];

    private static ParsedQuestionInfo Open(int n, int? page = null) => new(n, 0, false, false, page);
    private static ParsedQuestionInfo Test(int n) => new(n, 4, true, false, null);
    private static ParsedQuestionInfo Lettered(int n) => new(n, 3, false, false, null);

    [Fact]
    public void Open_question_document_keeps_every_answered_question()
    {
        var index = OpenQuestionClassifier.Classify(OpenChunks(5), Enumerable.Range(1, 5).Select(n => Open(n)).ToList());
        Assert.Equal(OpenDocumentKind.Open, index.Kind);
        Assert.True(index.Supported);
        Assert.Equal([1, 2, 3, 4, 5], index.Questions.Select(q => q.Number));
        Assert.All(index.Questions, q => Assert.StartsWith("This answer explains", q.Answer));
        Assert.Null(index.Rejection);
    }

    [Fact]
    public void Multiple_choice_test_is_rejected()
    {
        var chunks = new List<LearningDocumentChunk> { Chunk(string.Join("\n\n", Enumerable.Range(1, 10).Select(n => $"{n}. Which protocol is {n}?\nA) HTTP\nB) FTP\nC) SMTP\nD) DNS"))) };
        var index = OpenQuestionClassifier.Classify(chunks, Enumerable.Range(1, 10).Select(Test).ToList());
        Assert.Equal(OpenDocumentKind.MultipleChoice, index.Kind);
        Assert.False(index.Supported);
        Assert.Empty(index.Questions);
        Assert.Contains("variantlı", index.Rejection);
    }

    [Fact]
    public void Mixed_document_uses_only_the_open_questions()
    {
        var index = OpenQuestionClassifier.Classify(OpenChunks(8), [.. Enumerable.Range(1, 5).Select(n => Open(n)), Test(6), Test(7), Test(8)]);
        Assert.Equal(OpenDocumentKind.Mixed, index.Kind);
        Assert.Equal([1, 2, 3, 4, 5], index.Questions.Select(q => q.Number));
        Assert.Equal(3, index.ChoiceCount);
        Assert.Contains("Qarışıq", index.Warning);
    }

    [Fact]
    public void Letters_without_a_marked_answer_are_not_a_test()
    {
        // The parser saw A)/B)/C) under question 2, but no correct answer is marked: that question is left out, the document is not a test.
        var index = OpenQuestionClassifier.Classify(OpenChunks(5), [Open(1), Lettered(2), Open(3), Open(4), Open(5)]);
        Assert.True(index.Supported);
        Assert.Equal(0, index.ChoiceCount);
        Assert.Equal(1, index.UnclearCount);
        Assert.DoesNotContain(index.Questions, q => q.Number == 2);
    }

    [Fact]
    public void A_few_open_hits_inside_a_large_test_do_not_make_it_mixed()
    {
        var index = OpenQuestionClassifier.Classify(OpenChunks(3), [Open(1), Open(2), Open(3), .. Enumerable.Range(4, 40).Select(Test)]);
        Assert.Equal(OpenDocumentKind.MultipleChoice, index.Kind);
    }

    [Fact]
    public void Unparseable_pdf_is_never_accepted()
    {
        var index = OpenQuestionClassifier.Classify(OpenChunks(5), parsed: null);
        Assert.Equal(OpenDocumentKind.Unverifiable, index.Kind);
        Assert.False(index.Supported);
    }

    [Fact]
    public void Questions_without_answers_or_too_few_questions_are_rejected()
    {
        var bare = new List<LearningDocumentChunk> { Chunk("1. What is A?\n\n2. What is B?\n\n3. What is C?\n\n4. What is D?") };
        Assert.Equal(OpenDocumentKind.NoOpenQuestions, OpenQuestionClassifier.Classify(bare, []).Kind);
        Assert.Equal(OpenDocumentKind.NoOpenQuestions, OpenQuestionClassifier.Classify(OpenChunks(2), []).Kind);
    }

    [Fact]
    public void Answer_label_is_removed_and_parser_page_is_kept()
    {
        var chunks = new List<LearningDocumentChunk> { Chunk(string.Join("\n\n", Enumerable.Range(1, 3).Select(n => $"{n}. What is {n}?\n\nCavab: {Answer}"))) };
        var index = OpenQuestionClassifier.Classify(chunks, [Open(1, page: 7), Open(2), Open(3)]);
        Assert.All(index.Questions, q => Assert.StartsWith("This answer", q.Answer));
        Assert.Equal(7, index.Questions[0].Page);
        Assert.Equal(1, index.Questions[1].Page);
    }

    [Fact]
    public void Fingerprint_follows_question_and_answer_text()
    {
        var a = OpenQuestionClassifier.Classify(OpenChunks(3), []);
        var b = OpenQuestionClassifier.Classify(OpenChunks(3), []);
        var changed = a with { Questions = [a.Questions[0] with { Answer = "Another answer text that is long enough." }, .. a.Questions.Skip(1)] };
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        Assert.NotEqual(a.Fingerprint(), changed.Fingerprint());
    }

    [Fact]
    public void Stored_index_round_trips_without_changing_its_fingerprint()
    {
        var index = OpenQuestionClassifier.Classify(OpenChunks(8), [.. Enumerable.Range(1, 5).Select(n => Open(n, n)), Test(6), Test(7), Test(8)]);
        var json = System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var back = System.Text.Json.JsonSerializer.Deserialize<OpenQuestionIndex>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal(index.Kind, back.Kind);
        Assert.Equal(index.Fingerprint(), back.Fingerprint());
        Assert.Equal(index.Warning, back.Warning);
    }

    [Fact]
    public void Chat_message_is_matched_to_its_question_only_when_clear()
    {
        var index = new OpenQuestionIndex("t", OpenDocumentKind.Open, 0, 0, 0,
        [
            new(1, "Kompüter şəbəkəsinin topologiyası nədir?", Answer, 0, 1),
            new(2, "Verilənlər bazasının normallaşdırılması nədir?", Answer, 0, 2),
        ]);
        Assert.Equal(2, index.Match("verilənlər bazasının normallaşdırılması nədir")?.Number);
        Assert.Null(index.Match("Bu gün hava necədir?"));
        Assert.Null(index.Match("nədir"));
    }

    // ---------- Real PDFs through the same steps the API uses ----------

    private static OpenQuestionIndex Pipeline(byte[] pdf)
    {
        var text = LearningTextExtractor.FromPdf(new MemoryStream(pdf), new TextExtractionLimits());
        Assert.Equal(TextExtractionFailure.None, text.Failure);
        var chunks = LearningChunker.Chunk(text.Blocks, new ChunkingOptions())
            .Select(c => new LearningDocumentChunk { Ordinal = c.Ordinal, Text = c.Text, PageFrom = c.PageFrom, PageTo = c.PageTo }).ToList();
        return OpenQuestionClassifier.Classify(chunks, OpenQuestionClassifier.Inspect(new PdfQuestionParser(), new MemoryStream(pdf), default));
    }

    private static IEnumerable<(string Text, bool Bold)[]> OpenLines(int from, int to) => Enumerable.Range(from, to - from + 1).SelectMany(n => new[]
    {
        new[] { ($"{n}. What does the term topic {n} mean in networks?", false) },
        [($"Topic {n} is a set of rules that devices use to exchange data.", false)],
        [($"It defines the format and the order of the messages for topic {n}.", false)],
        [("", false)],
    });

    private static IEnumerable<(string Text, bool Bold)[]> TestLines(int from, int to) => Enumerable.Range(from, to - from + 1).SelectMany(n => new[]
    {
        new[] { ($"{n}. Which port is used by service {n}?", false) },
        [("A) 21", false)], [("B) 80", true)], [("C) 443", false)], [("D) 25", false)],
    });

    [Fact]
    public void Generated_open_pdf_is_accepted_with_answers_and_pages()
    {
        var index = Pipeline(Pdf(OpenLines(1, 12)));
        Assert.Equal(OpenDocumentKind.Open, index.Kind);
        Assert.Equal(12, index.Questions.Count);
        Assert.All(index.Questions, q => Assert.Contains("set of rules", q.Answer));
        Assert.Contains(index.Questions, q => q.Page == 2); // more than one page
    }

    [Theory]
    [InlineData("What is a protocol? A protocol is a set of rules for data exchange.", "What is a protocol?")]
    [InlineData("Name the layers of model 1. Model 1 has several layers described below.", "Name the layers of model 1.")]
    public void Question_and_answer_in_one_paragraph_are_split(string paragraph, string question)
    {
        var split = OpenQuestionExtractor.SplitInline(paragraph);
        Assert.Equal(question, split?.Question);
    }

    [Theory]
    [InlineData("What is a protocol?")]
    [InlineData("Explain the term protocol and give examples of its use in networks")]
    public void Question_without_following_text_is_not_split(string paragraph) => Assert.Null(OpenQuestionExtractor.SplitInline(paragraph));

    [Fact]
    public void Generated_test_pdf_is_rejected()
    {
        var index = Pipeline(Pdf(TestLines(1, 12)));
        Assert.Equal(OpenDocumentKind.MultipleChoice, index.Kind);
    }

    [Fact]
    public void Generated_mixed_pdf_uses_only_open_questions()
    {
        var index = Pipeline(Pdf([.. OpenLines(1, 6), .. TestLines(7, 10)]));
        Assert.Equal(OpenDocumentKind.Mixed, index.Kind);
        Assert.Equal([1, 2, 3, 4, 5, 6], index.Questions.Select(q => q.Number));
    }

    [Fact]
    public void Generated_open_pdf_with_lettered_lists_is_not_a_test()
    {
        var lines = Enumerable.Range(1, 6).SelectMany(n => new[]
        {
            new[] { ($"{n}. Name the layers of model {n}.", false) },
            [($"Model {n} has several layers that are described below:", false)],
            [("A) the physical layer carries bits", false)], [("B) the network layer routes packets", false)], [("", false)],
        });
        var index = Pipeline(Pdf(lines));
        Assert.NotEqual(OpenDocumentKind.MultipleChoice, index.Kind);
        Assert.Equal(0, index.ChoiceCount);
    }

    [Fact]
    public void Unreadable_or_textless_pdf_cannot_be_inspected()
    {
        Assert.Null(OpenQuestionClassifier.Inspect(new PdfQuestionParser(), new MemoryStream("not a pdf at all"u8.ToArray()), default));
        Assert.Null(OpenQuestionClassifier.Inspect(new PdfQuestionParser(), new MemoryStream(Pdf([])), default));
    }

    // ---------- Word (DOCX) through the same steps ----------

    private static OpenQuestionIndex DocxPipeline(byte[] docx)
    {
        var text = LearningTextExtractor.FromDocx(new MemoryStream(docx), new TextExtractionLimits());
        Assert.Equal(TextExtractionFailure.None, text.Failure);
        var chunks = LearningChunker.Chunk(text.Blocks, new ChunkingOptions())
            .Select(c => new LearningDocumentChunk { Ordinal = c.Ordinal, Text = c.Text, PageFrom = c.PageFrom, PageTo = c.PageTo }).ToList();
        var parser = new DocxQuestionParser(new PdfQuestionParser());
        return OpenQuestionClassifier.Classify(chunks, OpenQuestionClassifier.InspectDocx(parser, new MemoryStream(docx), default));
    }

    [Fact]
    public void Generated_open_docx_is_accepted_without_page_numbers()
    {
        var index = DocxPipeline(Docx(OpenLines(1, 8)));
        Assert.Equal(OpenDocumentKind.Open, index.Kind);
        Assert.Equal(8, index.Questions.Count);
        Assert.All(index.Questions, q => Assert.Contains("set of rules", q.Answer));
        Assert.All(index.Questions, q => Assert.Null(q.Page));
    }

    [Fact]
    public void Generated_test_docx_is_rejected_and_mixed_docx_keeps_open_questions()
    {
        Assert.Equal(OpenDocumentKind.MultipleChoice, DocxPipeline(Docx(TestLines(1, 10))).Kind);
        var mixed = DocxPipeline(Docx([.. OpenLines(1, 6), .. TestLines(7, 10)]));
        Assert.Equal(OpenDocumentKind.Mixed, mixed.Kind);
        Assert.Equal([1, 2, 3, 4, 5, 6], mixed.Questions.Select(q => q.Number));
    }

    [Fact]
    public void Broken_docx_cannot_be_inspected() =>
        Assert.Null(OpenQuestionClassifier.InspectDocx(new DocxQuestionParser(new PdfQuestionParser()), new MemoryStream("PK not a docx"u8.ToArray()), default));

    /// <summary>Minimal Word package: one paragraph per line, bold runs for marked answers.</summary>
    private static byte[] Docx(IEnumerable<(string Text, bool Bold)[]> lines)
    {
        static string Escape(string s) => System.Security.SecurityElement.Escape(s);
        var body = string.Concat(lines.Where(l => l.Any(r => r.Text.Length > 0)).Select(l => "<w:p>" + string.Concat(l.Select(r =>
            $"<w:r>{(r.Bold ? "<w:rPr><w:b/></w:rPr>" : "")}<w:t xml:space=\"preserve\">{Escape(r.Text)}</w:t></w:r>")) + "</w:p>"));
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(xml);
            }
            Add("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
            Add("word/document.xml", $"""<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>""");
        }
        return stream.ToArray();
    }

    /// <summary>Plain Helvetica PDF, 40 lines per page; a bold run is how the parser recognises a marked answer.</summary>
    private static byte[] Pdf(IEnumerable<(string Text, bool Bold)[]> lines)
    {
        using var builder = new PdfDocumentBuilder();
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var page = builder.AddPage(PageSize.A4);
        var y = 800.0;
        var count = 0;
        foreach (var line in lines)
        {
            if (count++ == 40) { page = builder.AddPage(PageSize.A4); y = 800; count = 1; }
            var x = 50.0;
            foreach (var (text, isBold) in line)
            {
                if (text.Length == 0) continue;
                var letters = page.AddText(text, 11, new PdfPoint(x, y), isBold ? bold : regular);
                x = letters[^1].EndBaseLine.X + 3;
            }
            y -= 16;
        }
        return builder.Build();
    }
}

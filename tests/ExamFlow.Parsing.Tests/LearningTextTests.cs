using System.IO.Compression;
using System.Text;
using ExamFlow.Parsing;

namespace ExamFlow.Parsing.Tests;

public class LearningTextTests
{
    private static readonly TextExtractionLimits Limits = new();

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Txt_paragraphs_and_markdown_sections()
    {
        var result = LearningTextExtractor.FromTxt(Utf8("# Giriş\n\nBirinci abzas\nikinci sətir.\n\n## Hüceyrə\n\nMitoxondri enerji istehsal edir."), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Equal(4, result.Blocks.Count);
        Assert.Equal("Birinci abzas ikinci sətir.", result.Blocks[1].Text);
        Assert.Equal("Giriş", result.Blocks[1].Section);
        Assert.Equal("Hüceyrə", result.Blocks[3].Section);
        Assert.All(result.Blocks, b => Assert.Null(b.Page));
    }

    [Fact]
    public void Txt_rejects_invalid_utf8_and_binary()
    {
        Assert.Equal(TextExtractionFailure.Unreadable, LearningTextExtractor.FromTxt(new MemoryStream([0x41, 0xFF, 0xFE, 0x42]), Limits).Failure);
        Assert.Equal(TextExtractionFailure.Unreadable, LearningTextExtractor.FromTxt(Utf8("abc\0def"), Limits).Failure);
    }

    [Fact]
    public void Txt_over_character_limit_is_rejected()
    {
        var result = LearningTextExtractor.FromTxt(Utf8(new string('a', 5000)), Limits with { MaxCharacters = 1000 });
        Assert.Equal(TextExtractionFailure.TooLarge, result.Failure);
    }

    private static MemoryStream Docx(string bodyXml, int padBytes = 0)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
                writer.Write($"""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{bodyXml}</w:body></w:document>""");
            if (padBytes > 0)
                using (var pad = zip.CreateEntry("word/media/pad.bin", CompressionLevel.SmallestSize).Open())
                    pad.Write(new byte[padBytes]); // compresses to almost nothing: a tiny zip bomb
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Docx_headings_become_sections()
    {
        var xml = """
            <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Fəsil 1</w:t></w:r></w:p>
            <w:p><w:r><w:t xml:space="preserve">Ərazi </w:t></w:r><w:r><w:t>coğrafiyası.</w:t></w:r></w:p>
            <w:p/>
            <w:p><w:r><w:t>İkinci abzas.</w:t></w:r></w:p>
            """;
        var result = LearningTextExtractor.FromDocx(Docx(xml), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Equal(["Fəsil 1", "Ərazi coğrafiyası.", "İkinci abzas."], result.Blocks.Select(b => b.Text));
        Assert.All(result.Blocks, b => Assert.Equal("Fəsil 1", b.Section));
        Assert.Equal([1, 2, 3], result.Blocks.Select(b => b.Paragraph));
    }

    [Fact]
    public void Docx_zip_bomb_is_rejected()
    {
        var result = LearningTextExtractor.FromDocx(Docx("<w:p><w:r><w:t>x</w:t></w:r></w:p>", padBytes: 4 * 1024 * 1024), Limits with { MaxDocxExpandedBytes = 1024 * 1024 });
        Assert.Equal(TextExtractionFailure.TooLarge, result.Failure);
    }

    [Fact]
    public void Docx_that_is_not_a_zip_is_unreadable()
    {
        Assert.Equal(TextExtractionFailure.Unreadable, LearningTextExtractor.FromDocx(Utf8("not a zip"), Limits).Failure);
    }

    [Fact]
    public void Pdf_keeps_page_numbers()
    {
        var pdf = new TestPdf().Line("Birinci sehife metni burada yazilib ve kifayet qeder uzundur.")
            .NewPage().Line("Ikinci sehifenin metni de burada yazilib ve oxunmalidir.").Build();
        var result = LearningTextExtractor.FromPdf(new MemoryStream(pdf), Limits);

        Assert.Equal(TextExtractionFailure.None, result.Failure);
        Assert.Equal(2, result.PageCount);
        Assert.Contains(result.Blocks, b => b.Page == 1 && b.Text.Contains("Birinci"));
        Assert.Contains(result.Blocks, b => b.Page == 2 && b.Text.Contains("Ikinci"));
    }

    [Fact]
    public void Pdf_without_text_reports_no_text()
    {
        var pdf = new TestPdf().NewPage().Build();
        Assert.Equal(TextExtractionFailure.NoText, LearningTextExtractor.FromPdf(new MemoryStream(pdf), Limits).Failure);
    }

    [Fact]
    public void Pdf_page_limit_is_enforced()
    {
        var pdf = new TestPdf().Line("a").NewPage().Line("b").NewPage().Line("c").Build();
        Assert.Equal(TextExtractionFailure.TooLarge, LearningTextExtractor.FromPdf(new MemoryStream(pdf), Limits with { MaxPages = 2 }).Failure);
    }

    [Fact]
    public void Garbage_pdf_is_unreadable()
    {
        Assert.Equal(TextExtractionFailure.Unreadable, LearningTextExtractor.FromPdf(Utf8("%PDF-1.4 broken"), Limits).Failure);
    }
}

public class LearningChunkerTests
{
    private static readonly ChunkingOptions Options = new();

    // ~175 estimated tokens per paragraph (3.5 characters per token).
    private static TextBlock Paragraph(int number, string? section = null, int? page = null) =>
        new(number, page, section, $"P{number}. " + string.Join(" ", Enumerable.Repeat("söz cümlə.", 55)));

    [Fact]
    public void Chunks_stay_within_size_bounds_and_overlap()
    {
        var blocks = Enumerable.Range(1, 20).Select(i => Paragraph(i, page: (i + 1) / 2)).ToList();
        var chunks = LearningChunker.Chunk(blocks, Options);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.InRange(c.Tokens, 1, Options.MaxTokens));
        Assert.All(chunks.SkipLast(1), c => Assert.True(c.Tokens >= Options.MinTokens, $"chunk {c.Ordinal} has {c.Tokens} tokens"));
        Assert.Equal(Enumerable.Range(1, chunks.Count), chunks.Select(c => c.Ordinal));
        // Overlap: each chunk after the first starts with paragraphs the previous chunk already had.
        for (var i = 1; i < chunks.Count; i++) Assert.True(chunks[i].ParagraphFrom <= chunks[i - 1].ParagraphTo);
        Assert.Equal(1, chunks[0].PageFrom);
        Assert.Equal(10, chunks[^1].PageTo);
    }

    [Fact]
    public void Section_change_starts_a_fresh_chunk_without_overlap()
    {
        var blocks = new[] { Paragraph(1, "A"), Paragraph(2, "A"), Paragraph(3, "A"), Paragraph(4, "B"), Paragraph(5, "B") };
        var chunks = LearningChunker.Chunk(blocks, Options);

        var b = chunks.First(c => c.Section == "B");
        Assert.Equal(4, b.ParagraphFrom);
        Assert.DoesNotContain(chunks, c => c.ParagraphFrom <= 3 && c.ParagraphTo >= 4);
    }

    [Fact]
    public void Oversized_paragraph_is_split_on_sentences()
    {
        var huge = new TextBlock(1, 7, null, string.Join(" ", Enumerable.Range(1, 400).Select(i => $"Cümlə nömrə {i} burada bitir.")));
        var chunks = LearningChunker.Chunk([huge], Options);

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, c => Assert.True(c.Tokens <= Options.MaxTokens, $"chunk {c.Ordinal} has {c.Tokens} tokens"));
        Assert.All(chunks, c => Assert.Equal(7, c.PageFrom));
        Assert.Contains("Cümlə nömrə 400 burada bitir.", chunks[^1].Text);
    }

    [Fact]
    public void Long_paragraphs_with_overlap_still_fill_chunks_to_minimum()
    {
        // ~345-token paragraphs: overlap + one paragraph would sit below MinTokens without sentence filling.
        var text = string.Concat(Enumerable.Repeat("Mitoxondri enerji istehsal edir və ATP sintezi burada baş verir. ", 19));
        var blocks = Enumerable.Range(1, 12).Select(i => new TextBlock(i, null, null, text)).ToList();
        var chunks = LearningChunker.Chunk(blocks, Options);

        Assert.All(chunks.SkipLast(1), c => Assert.InRange(c.Tokens, Options.MinTokens, Options.MaxTokens));
        for (var i = 1; i < chunks.Count; i++) Assert.True(chunks[i].ParagraphFrom <= chunks[i - 1].ParagraphTo);
    }

    [Fact]
    public void Small_document_is_one_chunk()
    {
        var chunks = LearningChunker.Chunk([new TextBlock(1, null, null, "Qısa mətn.")], Options);
        Assert.Single(chunks);
        Assert.Equal("Qısa mətn.", chunks[0].Text);
    }
}

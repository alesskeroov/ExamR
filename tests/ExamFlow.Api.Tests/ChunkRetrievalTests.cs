using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class ChunkRetrievalTests
{
    private static readonly RetrievalOptions Options = new();

    private static LearningDocumentChunk Chunk(int ordinal, string text, int? page = null) =>
        new() { Id = Guid.NewGuid(), Ordinal = ordinal, Text = text, PageFrom = page, PageTo = page, ParagraphFrom = ordinal, ParagraphTo = ordinal };

    private static readonly LearningDocumentChunk[] Biology =
    [
        Chunk(1, "Hüceyrə canlıların əsas quruluş vahididir. Hər hüceyrənin membranı var.", 1),
        Chunk(2, "Mitoxondri hüceyrənin enerji stansiyasıdır və ATP sintezi mitoxondridə baş verir.", 2),
        Chunk(3, "Fotosintez zamanı bitkilər işıq enerjisini kimyəvi enerjiyə çevirir. Xlorofil yaşıl piqmentdir.", 3),
        Chunk(4, "Azərbaycanın paytaxtı Bakı şəhəridir və Xəzər dənizinin sahilində yerləşir.", 4),
    ];

    [Fact]
    public void Exact_phrase_ranks_its_chunk_first_and_keeps_page()
    {
        var result = LexicalIndex.Build(Biology).Search("ATP sintezi", 5, Options);
        Assert.True(result.Found);
        Assert.Equal(2, result.Chunks[0].Ordinal);
        Assert.Equal(2, result.Chunks[0].PageFrom);
    }

    [Fact]
    public void Inflected_and_unaccented_query_still_matches()
    {
        // "mitoxondrinin" / "enerjini" are other suffixes; "huceyre" has no Azerbaijani letters.
        var result = LexicalIndex.Build(Biology).Search("mitoxondrinin enerjini nece istehsal etdiyi", 5, Options);
        Assert.Equal(2, result.Chunks[0].Ordinal);
        Assert.Equal(1, LexicalIndex.Build(Biology).Search("HUCEYRE membrani", 5, Options).Chunks[0].Ordinal);
    }

    [Fact]
    public void Dotted_capital_I_is_folded_azerbaijani_way()
    {
        Assert.Equal(TextNormalizer.Terms("İşıq").Single(), TextNormalizer.Terms("isiq").Single());
    }

    [Fact]
    public void Absent_topic_returns_no_match()
    {
        var result = LexicalIndex.Build(Biology).Search("kvant mexanikası nədir?", 5, Options);
        Assert.False(result.Found);
        Assert.Empty(result.Chunks);
        Assert.False(LexicalIndex.Build(Biology).Search("nədir və necə?", 5, Options).Found); // only stop words
    }

    [Fact]
    public void Near_duplicates_are_returned_once()
    {
        var text = "Fotosintez xlorofil vasitəsilə işıq enerjisini kimyəvi enerjiyə çevirir.";
        var chunks = new[] { Chunk(1, text, 3), Chunk(2, text + " Bitki.", 4), Chunk(3, "Xlorofil yaşıl rəng verir, fotosintez üçün vacibdir.", 9) };
        var result = LexicalIndex.Build(chunks).Search("fotosintez xlorofil", 5, Options);
        Assert.Equal(2, result.Chunks.Count);
        Assert.Contains(result.Chunks, c => c.Ordinal == 3);
    }

    [Fact]
    public void Large_document_respects_top_k()
    {
        var chunks = Enumerable.Range(1, 3000)
            .Select(i => Chunk(i, $"Bölmə {i}: tarix və coğrafiya mövzusu {i % 7}. Xüsusi termin söz{i}.", i / 3 + 1)).ToList();
        chunks.Add(Chunk(3001, "Naxçıvan Muxtar Respublikasının konstitusiyası haqqında məlumat.", 999));
        var index = LexicalIndex.Build(chunks);

        Assert.Equal(3001, index.Search("Naxçıvan konstitusiyası", 5, Options).Chunks.Single().Ordinal);
        Assert.Equal(4, index.Search("tarix coğrafiya", 4, Options).Chunks.Count);
    }

    [Fact]
    public void Reindexing_same_chunks_gives_same_result()
    {
        var first = LexicalIndex.Build(Biology).Search("fotosintez işıq", 5, Options);
        var second = LexicalIndex.Build(Biology.Select(c => Chunk(c.Ordinal, c.Text, c.PageFrom))).Search("fotosintez işıq", 5, Options);
        Assert.Equal(first.Chunks.Select(c => (c.Ordinal, c.Score)), second.Chunks.Select(c => (c.Ordinal, c.Score)));
    }
}

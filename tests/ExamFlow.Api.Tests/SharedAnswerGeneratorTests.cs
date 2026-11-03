using System.Text.Json;
using System.Text.Json.Nodes;
using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class SharedAnswerGeneratorTests
{
    private static readonly string[] Sentences =
    [
        "Bulud hesablaması resursları şəbəkə üzərindən təqdim edir.",
        "Hesablama gücü tələbə uyğun ayrılır.",
        "Məlumatlar uzaq serverlərdə saxlanılır.",
        "İstifadəçi xidmətlərə internet vasitəsilə qoşulur.",
        "Resursların idarə edilməsi provayder tərəfindən aparılır.",
        "Elastiklik yük dəyişdikdə resursları artırmağa imkan verir.",
        "Ödəniş istifadə olunan resurslara uyğun hesablana bilər.",
        "Şəbəkə bağlantısından asılılıq xidmətin məhdudiyyətidir."
    ];
    private static LearningDocumentChunk Chunk(int ordinal = 1, string? text = null, int? page = 1) =>
        new() { Ordinal = ordinal, Text = text ?? string.Join(' ', Sentences), PageFrom = page, PageTo = page };
    private static JsonElement Output(int count = 8, int ordinal = 1) => JsonSerializer.SerializeToElement(new
    {
        sufficient = true, mainTopic = "Bulud hesablamaları",
        sentences = Enumerable.Range(0, count).Select(i => new { text = Sentences[i % 8], chunkOrdinal = ordinal, quote = Sentences[i % 8] })
    });
    private static JsonElement Notes() => JsonSerializer.SerializeToElement(new
    {
        subjects = "Bulud hesablamaları mətnin əsas mövzusudur; digər mövzularla qarışdırılmır.",
        facts = Sentences.Select(s => new { text = s, chunkOrdinal = 1, quote = s })
    });

    [Fact]
    public async Task Short_document_uses_one_call_and_returns_one_eight_sentence_answer()
    {
        var calls = 0;
        var result = await SharedAnswerGenerator.GenerateAsync([Chunk()], (messages, name, schema, tokens, ct) =>
        {
            calls++;
            Assert.Equal("shared_exam_answer", name);
            Assert.Contains("simple Azerbaijani", messages[0].Content);
            return Task.FromResult(new StudyAiResult(Output(), 200, null));
        }, default);
        Assert.Equal(1, calls);
        Assert.Equal(200, result.Status);
        Assert.Equal(string.Join(' ', Sentences), result.Payload!.Answer);
        Assert.Equal([1], result.Payload.SourcePages);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(35)]
    [InlineData(120)]
    public async Task Every_document_portion_is_analysed_without_sampling(int questions)
    {
        var chunks = Enumerable.Range(1, questions).Select(i => Chunk(i, $"{i}. Bulud haqqında sual? START_{i} " + string.Join(' ', Sentences) + $" END_{i}", i)).ToList();
        var inputs = new List<string>();
        var result = await SharedAnswerGenerator.GenerateAsync(chunks, (messages, name, schema, tokens, ct) =>
        {
            inputs.Add(messages[1].Content);
            return Task.FromResult(new StudyAiResult(name == "shared_exam_answer" ? Output() : Notes(), 200, null));
        }, default);
        Assert.Equal(200, result.Status);
        foreach (var i in Enumerable.Range(1, questions))
        {
            Assert.Contains(inputs, p => p.Contains($"START_{i} ", StringComparison.Ordinal));
            Assert.Contains(inputs, p => p.Contains($" END_{i}", StringComparison.Ordinal));
        }
        if (questions == 120) Assert.True(inputs.Count > 1);
    }

    [Fact]
    public void Oversized_chunks_are_split_without_losing_the_end()
    {
        var batches = SharedAnswerGenerator.SourceBatches([Chunk(text: "START " + new string('x', 100000) + " END")]);
        Assert.True(batches.Count > 1);
        Assert.StartsWith("<source", batches[0]);
        Assert.Contains(" END", batches[^1]);
        Assert.All(batches, b => Assert.True(b.Length <= SharedAnswerGenerator.BatchCharacters));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    public void Rejects_out_of_range_sentence_counts(int count) =>
        Assert.Throws<JsonException>(() => SharedAnswerGenerator.Validate(Output(count), [Chunk()]));

    [Fact]
    public async Task Invalid_output_has_only_one_repair_attempt()
    {
        var calls = 0;
        var result = await SharedAnswerGenerator.GenerateAsync([Chunk()], (messages, name, schema, tokens, ct) =>
        {
            calls++;
            return Task.FromResult(new StudyAiResult(Output(7), 200, null));
        }, default);
        Assert.Equal(2, calls);
        Assert.Equal(502, result.Status);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task Insufficient_information_returns_no_fabricated_answer()
    {
        var result = await SharedAnswerGenerator.GenerateAsync([Chunk(text: "1. Bulud nədir?\n2. Server nədir?\n3. Şəbəkə nədir?")],
            (messages, name, schema, tokens, ct) => Task.FromResult(new StudyAiResult(
                JsonSerializer.SerializeToElement(new { sufficient = false, mainTopic = "", sentences = Array.Empty<object>() }), 200, null)), default);
        Assert.Equal(409, result.Status);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Rejects_nonexistent_sources_and_made_up_quotes()
    {
        Assert.Throws<JsonException>(() => SharedAnswerGenerator.Validate(Output(ordinal: 99), [Chunk()]));
        Assert.Throws<JsonException>(() => SharedAnswerGenerator.Validate(Output(), [Chunk(text: "Tamamilə başqa mətn.")]));
        var output = JsonNode.Parse(Output().GetRawText())!;
        output["mainTopic"] = " ";
        Assert.Throws<JsonException>(() => SharedAnswerGenerator.Validate(JsonSerializer.SerializeToElement(output), [Chunk()]));
    }

    [Fact]
    public void Rejects_multiple_sentences_hidden_in_one_item()
    {
        var output = JsonNode.Parse(Output().GetRawText())!;
        output["sentences"]![0]!["text"] = Sentences[0] + " " + Sentences[1];
        Assert.Throws<JsonException>(() => SharedAnswerGenerator.Validate(JsonSerializer.SerializeToElement(output), [Chunk()]));
    }

    [Fact]
    public void Docx_sources_without_page_numbers_do_not_invent_pages()
    {
        var result = SharedAnswerGenerator.Validate(Output(), [Chunk(page: null)]);
        Assert.Empty(result.Payload!.SourcePages);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(13)]
    public void Accepts_both_sentence_count_boundaries(int count)
    {
        var sentences = Enumerable.Range(1, count).Select(i => $"Resurs {i} şəbəkə vasitəsilə istifadəçiyə təqdim olunur.").ToList();
        var json = JsonSerializer.SerializeToElement(new
        {
            sufficient = true, mainTopic = "Bulud resursları",
            sentences = sentences.Select(s => new { text = s, chunkOrdinal = 1, quote = s })
        });
        Assert.Equal(200, SharedAnswerGenerator.Validate(json, [Chunk(text: string.Join(' ', sentences))]).Status);
    }

    [Fact]
    public void Cache_is_scoped_to_user_document_content_and_indexed_text()
    {
        var user = Guid.NewGuid(); var doc = Guid.NewGuid();
        var original = SharedAnswerGenerator.CacheKey(user, doc, [1, 2], [Chunk()]);
        Assert.Equal(original, SharedAnswerGenerator.CacheKey(user, doc, [1, 2], [Chunk()]));
        Assert.NotEqual(original, SharedAnswerGenerator.CacheKey(Guid.NewGuid(), doc, [1, 2], [Chunk()]));
        Assert.NotEqual(original, SharedAnswerGenerator.CacheKey(user, Guid.NewGuid(), [1, 2], [Chunk()]));
        Assert.NotEqual(original, SharedAnswerGenerator.CacheKey(user, doc, [3, 4], [Chunk()]));
        Assert.NotEqual(original, SharedAnswerGenerator.CacheKey(user, doc, [1, 2], [Chunk(text: "Changed")]));
        Assert.NotEqual(StudyPrompts.Version, SharedAnswerGenerator.Version);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Provider_and_budget_failures_are_preserved(int status)
    {
        var result = await SharedAnswerGenerator.GenerateAsync([Chunk()], (messages, name, schema, tokens, ct) =>
            Task.FromResult(new StudyAiResult(null, status, "Xidmət müvəqqəti əlçatan deyil.")), default);
        Assert.Equal(status, result.Status);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task Empty_document_does_not_call_ai()
    {
        var result = await SharedAnswerGenerator.GenerateAsync([], (messages, name, schema, tokens, ct) => throw new Exception("Must not call"), default);
        Assert.Equal(409, result.Status);
    }
}

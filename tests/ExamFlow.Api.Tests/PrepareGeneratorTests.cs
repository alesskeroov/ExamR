using System.Text.Json;
using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class PrepareGeneratorTests
{
    private static readonly string[] Facts =
    [
        "Protokol cihazların məlumat mübadiləsi qaydalarıdır.",
        "HTTP veb səhifələrin ötürülməsi üçün istifadə olunur.",
        "TCP məlumatın etibarlı çatdırılmasını təmin edir.",
        "IP paketlərin ünvanlanmasını həyata keçirir.",
        "DNS domen adlarını IP ünvanlarına çevirir.",
        "Port xidməti kompüter daxilində müəyyən edir.",
        "Şifrələmə məlumatı icazəsiz oxunmadan qoruyur.",
        "Marşrutlaşdırıcı paketləri şəbəkələr arasında ötürür.",
    ];

    private static readonly IndexedQuestion[] Questions =
    [
        new(1, "Protokol nədir?", string.Join(' ', Facts[..4]), 0, 3),
        new(2, "DNS nə edir?", string.Join(' ', Facts[4..]), 1, 4),
        new(3, "Hava proqnozu necə verilir?", "Hava proqnozu meteoroloji stansiyaların məlumatlarına əsaslanır.", 2, 9),
    ];

    private static IReadOnlyList<LearningDocumentChunk> Units => PrepareGenerator.Units(Questions);

    private static object Item(string text, int source) => new { text, source, quote = text };

    private static JsonElement Answer(int sentences = 8, bool sufficient = true, object[]? questions = null) => JsonSerializer.SerializeToElement(new
    {
        sufficient,
        sentences = Enumerable.Range(0, sentences).Select(i => Item(Facts[i % 8], i % 8 < 4 ? 1 : 2)).ToArray(),
        questions = questions ?? [
            new { number = 1, covered = true, additions = new[] { new { text = Facts[0], quote = Facts[0] } } },
            // Claims coverage, but its addition is quoted from question 1's answer: not accepted.
            new { number = 2, covered = true, additions = new[] { new { text = Facts[1], quote = Facts[1] } } },
            new { number = 3, covered = false, additions = Array.Empty<object>() },
        ],
    });

    [Fact]
    public void Schemas_are_valid_strict_json_schemas()
    {
        Assert.Equal("object", PrepareGenerator.ExplainSchema()["type"]!.GetValue<string>());
        Assert.Equal(5, PrepareGenerator.ExplainSchema()["required"]!.AsArray().Count);
        Assert.Equal(3, PrepareGenerator.AnswerSchema()["required"]!.AsArray().Count);
    }

    [Fact]
    public void Group_answer_keeps_only_questions_its_own_answer_supports()
    {
        var answer = PrepareGenerator.ValidateAnswer(Answer(), "Şəbəkə protokolları", Questions, Units);
        Assert.True(answer.Sufficient);
        Assert.Equal(8, answer.Answer.Split(". ").Length);
        var byNumber = answer.Questions.ToDictionary(q => q.Number);
        Assert.True(byNumber[1].Covered);
        Assert.Equal(Facts[0], byNumber[1].Addition);
        Assert.Null(byNumber[1].DocumentAnswer);
        Assert.False(byNumber[2].Covered);
        Assert.Equal(Questions[1].Answer, byNumber[2].DocumentAnswer);
        Assert.False(byNumber[3].Covered);
        Assert.Equal([3, 4], answer.SourcePages);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    public void Group_answer_needs_8_to_13_sentences(int count) =>
        Assert.Throws<JsonException>(() => PrepareGenerator.ValidateAnswer(Answer(count), "t", Questions, Units));

    [Fact]
    public void Group_answer_rejects_quotes_from_outside_the_group_or_invented_text()
    {
        var foreign = JsonSerializer.SerializeToElement(new { sufficient = true, sentences = Enumerable.Range(0, 8).Select(i => Item(Facts[i], 99)), questions = Array.Empty<object>() });
        Assert.Throws<JsonException>(() => PrepareGenerator.ValidateAnswer(foreign, "t", Questions, Units));
        var invented = JsonSerializer.SerializeToElement(new { sufficient = true, sentences = Enumerable.Range(0, 8).Select(i => Item($"Uydurulmuş cümlə nömrə {i} burada yazılıb.", 1)), questions = Array.Empty<object>() });
        Assert.Throws<JsonException>(() => PrepareGenerator.ValidateAnswer(invented, "t", Questions, Units));
    }

    [Fact]
    public void Insufficient_or_uncovering_answer_shows_document_answers_only()
    {
        var insufficient = PrepareGenerator.ValidateAnswer(Answer(0, sufficient: false), "t", Questions, Units);
        Assert.False(insufficient.Sufficient);
        Assert.Equal("", insufficient.Answer);
        Assert.All(insufficient.Questions, q => Assert.NotNull(q.DocumentAnswer));

        var none = PrepareGenerator.ValidateAnswer(Answer(questions: [new { number = 1, covered = false, additions = Array.Empty<object>() }]), "t", Questions, Units);
        Assert.False(none.Sufficient);
    }

    [Fact]
    public async Task Invalid_answer_gets_one_repair_attempt_then_fails_without_a_fake_result()
    {
        var calls = 0;
        var result = await PrepareGenerator.AnswerAsync("t", Questions, (m, name, schema, tokens, ct) =>
        {
            calls++;
            Assert.Equal("topic_exam_answer", name);
            Assert.Contains("<source id=\"2\"", m[1].Content);
            return Task.FromResult(new StudyAiResult(Answer(5), 200, null));
        }, default);
        Assert.Equal(2, calls);
        Assert.Equal(502, result.Status);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task Provider_failure_is_passed_through()
    {
        var result = await PrepareGenerator.ExplainAsync("t", Questions, (m, n, s, t, ct) => Task.FromResult(new StudyAiResult(null, 429, "Limit")), default);
        Assert.Equal(429, result.Status);
        Assert.Equal("Limit", result.Error);
    }

    private static JsonElement Explanation(object[] explanation, object? example = null, bool sufficient = true) => JsonSerializer.SerializeToElement(new
    {
        sufficient, explanation,
        concepts = new object[] { new { term = "DNS", meaning = Facts[4], source = 2, quote = Facts[4] }, new { term = "Saxta", meaning = "Uydurma izah burada yazılıb.", source = 2, quote = "Uydurma izah burada yazılıb." } },
        example = example ?? new { text = "", source = 0, quote = "" },
        facts = new object[] { Item(Facts[2], 1), Item(Facts[3], 2) },
    });

    [Fact]
    public void Explanation_drops_items_the_pdf_does_not_support()
    {
        var result = PrepareGenerator.ValidateExplanation(Explanation([Item(Facts[0], 1), Item(Facts[1], 1)]), "Protokollar", Questions, Units);
        var topic = result.Payload!;
        Assert.Equal([Facts[0], Facts[1]], topic.Explanation);
        Assert.Equal("DNS", Assert.Single(topic.Concepts).Term);   // the invented concept is dropped
        Assert.Null(topic.Example);                                // no example in the PDF → none shown
        Assert.Equal([Facts[2]], topic.Facts);                    // fact quoted from the wrong question is dropped
        Assert.Equal([1, 2, 3], topic.QuestionNumbers);
    }

    [Fact]
    public void Explanation_keeps_a_supported_example()
    {
        var example = new { text = "Məsələn, DNS domen adını IP ünvanına çevirir.", source = 2, quote = Facts[4] };
        var topic = PrepareGenerator.ValidateExplanation(Explanation([Item(Facts[0], 1)], example), "t", Questions, Units).Payload!;
        Assert.Equal(example.text, topic.Example);
    }

    [Fact]
    public void Mostly_invented_explanation_is_rejected_and_insufficient_is_409()
    {
        var invented = Explanation([Item(Facts[0], 1), Item("Bu cümlə sənəddə yoxdur və uydurulub.", 1), Item("Bu da sənəddə olmayan başqa bir iddiadır.", 2)]);
        Assert.Throws<JsonException>(() => PrepareGenerator.ValidateExplanation(invented, "t", Questions, Units));
        Assert.Equal(409, PrepareGenerator.ValidateExplanation(Explanation([], sufficient: false), "t", Questions, Units).Status);
    }

    [Fact]
    public void Long_answers_are_shortened_to_the_topic_budget()
    {
        var many = Enumerable.Range(1, 12).Select(n => new IndexedQuestion(n, $"Sual {n}?", new string('x', 5000), 0, 1)).ToList();
        var units = PrepareGenerator.Units(many);
        Assert.Equal(12, units.Count);
        Assert.True(units.Sum(u => u.Text.Length) <= PrepareGenerator.SourceCharacters + 12 * 40);
    }
}

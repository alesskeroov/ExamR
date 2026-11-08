namespace ExamFlow.Parsing.Tests;

public class QuestionSegmenterTests
{
    private static TextLine L(string text) => new(1, text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(TextToken.Plain).ToList());

    [Fact]
    public void Numbers_out_of_sequence_stay_in_the_question_text()
    {
        var questions = QuestionSegmenter.Segment([
            L("1. Hansı hadisə"),
            L("1990-cı ildə baş verib?"),
            L("A) birinci"),
            L("B) ikinci"),
        ]);

        var question = Assert.Single(questions);
        Assert.Equal("Hansı hadisə 1990-cı ildə baş verib?", string.Join(' ', question.TextTokens.Select(t => t.Text)));
        Assert.Equal(2, question.Options.Count);
    }

    [Fact]
    public void Small_number_jump_after_complete_question_is_accepted_with_warning()
    {
        var questions = QuestionSegmenter.Segment([
            L("14. Birinci?"), L("A) x"), L("B) y"),
            L("16. Növbəti?"), L("A) x"), L("B) y"),
        ]);

        Assert.Equal([14, 16], questions.Select(q => q.Number));
        Assert.NotEmpty(questions[1].Warnings);
    }

    [Fact]
    public void Text_before_the_first_question_is_ignored()
    {
        var questions = QuestionSegmenter.Segment([L("Test: Informatika"), L("Variant 2026"), L("1. Sual?"), L("A) x"), L("B) y")]);

        Assert.Equal("Sual?", string.Join(' ', Assert.Single(questions).TextTokens.Select(t => t.Text)));
    }

    [Fact]
    public void Ordinal_numbers_and_contents_entries_before_the_questions_are_not_questions()
    {
        var questions = QuestionSegmenter.Segment([
            L("11-ci sinif buraxılış hazırlığı · 21 mövzu"),
            L("1. Ədədlər və bölünmə — 15 sual"),
            L("2. Adi kəsrlər — 15 sual"),
            L("3. Faiz və nisbət ........ 12"),
            L("1. ƏDƏDLƏR VƏ BÖLÜNMƏ"),
            L("001. Birinci sual?"), L("A) x"), L("B) y"),
            L("002. İkinci sual?"), L("A) x"), L("B) y"),
        ]);

        Assert.Equal([1, 2], questions.Select(q => q.Number));
        Assert.Equal("Birinci sual?", string.Join(' ', questions[0].TextTokens.Select(t => t.Text)));
    }

    [Fact]
    public void Upper_case_section_heading_is_not_glued_to_the_previous_option()
    {
        var questions = QuestionSegmenter.Segment([
            L("30. Sual?"), L("A) x"), L("B) 1/2"),
            L("3. FAİZ VƏ NİSBƏT"),
            L("31. Növbəti?"), L("A) x"), L("B) y"),
        ]);

        Assert.Equal([30, 31], questions.Select(q => q.Number));
        Assert.Equal("1/2", string.Join(' ', questions[0].Options[1].TextTokens.Select(t => t.Text)));
    }

    [Fact]
    public void Upper_case_question_followed_by_options_is_kept()
    {
        var questions = QuestionSegmenter.Segment([L("1. AZƏRBAYCANIN PAYTAXTI"), L("A) Bakı"), L("B) Gəncə")]);

        Assert.Equal(2, Assert.Single(questions).Options.Count);
    }

    [Fact]
    public void Option_letters_must_be_sequential()
    {
        var questions = QuestionSegmenter.Segment([L("1. Sual"), L("B) hələ variant deyil"), L("A) x"), L("B) y")]);

        var question = Assert.Single(questions);
        Assert.Equal("Sual B) hələ variant deyil", string.Join(' ', question.TextTokens.Select(t => t.Text)));
        Assert.Equal(2, question.Options.Count);
    }

    [Fact]
    public void Hyphenated_words_are_rejoined()
    {
        var questions = QuestionSegmenter.Segment([L("1. Verilən-"), L("lər bazası nədir?"), L("A) x"), L("B) y")]);

        Assert.Equal("Verilənlər bazası nədir?", string.Join(' ', Assert.Single(questions).TextTokens.Select(t => t.Text)));
    }

    private static TextLine At(double y, string text) =>
        new(1, text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(TextToken.Plain).ToList(), y);

    private static string Text(IEnumerable<TextToken> tokens) => string.Join(' ', tokens.Select(t => t.Text));

    [Fact]
    public void Bullet_options_get_sequential_labels_and_check_mark_is_the_answer()
    {
        var question = Assert.Single(QuestionSegmenter.Segment([L("1. Sual?"), L("• bir"), L("√ iki"), L("•üç")]));

        Assert.Equal(["bir", "iki", "üç"], question.Options.Select(o => Text(o.TextTokens)));
        Assert.Equal(['A', 'B', 'C'], question.Options.Select(o => o.Letter));
        Assert.Equal(1, new MarkerAnswerDetector().Detect(question).CorrectIndex);
    }

    [Fact]
    public void Text_above_a_vertically_centred_marker_belongs_to_that_option()
    {
        // Table cell: two text lines (y 424 / 414) with the marker centred between them (y 420).
        var question = Assert.Single(QuestionSegmenter.Segment([
            At(480, "4. Sual?"),
            At(452, "• birinci"),
            At(424, "ikinci variantın"),
            At(420, "√"),
            At(414, "davamı"),
            At(400, "• üçüncü"),
        ]));

        Assert.Equal(["birinci", "ikinci variantın davamı", "üçüncü"], question.Options.Select(o => Text(o.TextTokens)));
    }

    [Fact]
    public void Text_above_a_vertically_centred_question_number_belongs_to_that_question()
    {
        var questions = QuestionSegmenter.Segment([
            At(760, "1. Birinci?"), At(746, "• a"), At(732, "• b"),
            At(704, "Uzun sualın"), At(699, "2."), At(694, "davamı?"),
            At(667, "• c"), At(653, "• d"),
        ]);

        Assert.Equal(["Birinci?", "Uzun sualın davamı?"], questions.Select(q => Text(q.TextTokens)));
        Assert.Equal(["a", "b"], questions[0].Options.Select(o => Text(o.TextTokens)));
    }

    [Fact]
    public void Wrapped_line_equidistant_from_both_anchors_stays_with_the_one_above()
    {
        var question = Assert.Single(QuestionSegmenter.Segment([
            At(500, "1. Sual?"), At(486, "• uzun variant"), At(476, "davamı"), At(466, "• növbəti"),
        ]));

        Assert.Equal(["uzun variant davamı", "növbəti"], question.Options.Select(o => Text(o.TextTokens)));
    }

    [Fact]
    public void Title_above_the_first_question_is_not_attached_to_it()
    {
        var question = Assert.Single(QuestionSegmenter.Segment([At(800, "Fənn: Hüquq"), At(772, "1. Sual?"), At(744, "• a"), At(730, "• b")]));

        Assert.Equal("Sual?", Text(question.TextTokens));
    }

    [Fact]
    public void Content_hash_ignores_case_with_azerbaijani_rules_and_option_order()
    {
        var a = ContentHasher.Compute("İSTİQAMƏT nədir?", ["Bir", "İki"]);
        var b = ContentHasher.Compute("istiqamət nədir", ["iki", "bir"]);

        Assert.Equal(a, b);
    }
}

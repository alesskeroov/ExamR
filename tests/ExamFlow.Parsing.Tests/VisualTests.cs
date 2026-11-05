namespace ExamFlow.Parsing.Tests;

public class VisualMapperTests
{
    private static ParsedQuestion Question(int number, SourceAnchor anchor, params SourceAnchor[] options) =>
        new(number, $"Q{number}", options.Select((_, i) => new ParsedOption(((char)('A' + i)).ToString(), $"o{i}")).ToList(), null, null, [])
        {
            Anchor = anchor,
            OptionAnchors = options,
        };

    private static VisualMapping MapOne(IReadOnlyList<ParsedQuestion> questions, int page, VisualBounds bounds, params SourceAnchor[] skipped) =>
        Assert.Single(VisualMapper.Map(questions, skipped, [(page, bounds)]));

    // Question 1 at 800, options at 640/624/608/592; question 2 at 560 with options at 544/528.
    private static readonly ParsedQuestion[] TwoQuestions =
    [
        Question(1, new(1, 800), new(1, 640), new(1, 624), new(1, 608), new(1, 592)),
        Question(2, new(1, 560), new(1, 544), new(1, 528)),
    ];

    [Fact]
    public void Image_between_question_text_and_options_belongs_to_the_question_body()
    {
        var mapping = MapOne(TwoQuestions, 1, new VisualBounds(50, 660, 250, 790));

        Assert.Equal(new VisualMapping(0, null, VisualMappingStatus.AutoMapped), mapping);
    }

    [Fact]
    public void Option_label_just_below_an_image_does_not_take_it()
    {
        // Image ends 6pt above option A's baseline (closer than the line allowance).
        var mapping = MapOne(TwoQuestions, 1, new VisualBounds(50, 646, 250, 790));

        Assert.Equal(new VisualMapping(0, null, VisualMappingStatus.AutoMapped), mapping);
    }

    [Fact]
    public void Image_beside_an_option_label_belongs_to_that_option()
    {
        var mapping = MapOne(TwoQuestions, 1, new VisualBounds(300, 616, 340, 632)); // centred on B (624)

        Assert.Equal(new VisualMapping(0, 1, VisualMappingStatus.AutoMapped), mapping);
    }

    [Fact]
    public void Image_below_an_option_label_belongs_to_that_option()
    {
        var questions = new[] { Question(1, new(1, 800), new(1, 700), new(1, 600)) };

        var mapping = MapOne(questions, 1, new VisualBounds(50, 610, 150, 696)); // under A, above B

        Assert.Equal(new VisualMapping(0, 0, VisualMappingStatus.AutoMapped), mapping);
    }

    [Fact]
    public void Image_before_the_first_question_is_unmapped()
    {
        var mapping = MapOne(TwoQuestions, 1, new VisualBounds(50, 815, 100, 830));

        Assert.Equal(VisualMappingStatus.Unmapped, mapping.Status);
        Assert.Null(mapping.QuestionIndex);
    }

    [Fact]
    public void Image_spanning_two_questions_needs_review()
    {
        var mapping = MapOne(TwoQuestions, 1, new VisualBounds(300, 540, 400, 600));

        Assert.Equal(VisualMappingStatus.NeedsReview, mapping.Status);
    }

    [Fact]
    public void Image_beside_options_on_one_line_needs_review()
    {
        var questions = new[] { Question(1, new(1, 800), new(1, 700), new(1, 700), new(1, 700)) };

        var mapping = MapOne(questions, 1, new VisualBounds(300, 690, 340, 710));

        Assert.Equal(new VisualMapping(0, null, VisualMappingStatus.NeedsReview, "Eyni sətirdə bir neçə variant var"), mapping);
    }

    [Fact]
    public void Question_continuing_on_the_next_page_keeps_its_image()
    {
        var questions = new[]
        {
            Question(1, new SourceAnchor(1, 80), new SourceAnchor(2, 700), new SourceAnchor(2, 684)),
            Question(2, new SourceAnchor(2, 600), new SourceAnchor(2, 584)),
        };

        var mapping = MapOne(questions, 2, new VisualBounds(50, 720, 200, 810));

        Assert.Equal(new VisualMapping(0, null, VisualMappingStatus.AutoMapped), mapping);
    }

    [Fact]
    public void Image_in_a_skipped_duplicate_question_is_not_given_to_the_previous_one()
    {
        var mapping = MapOne(TwoQuestions, 1, new VisualBounds(50, 400, 150, 480), skipped: new SourceAnchor(1, 500));

        Assert.Equal(VisualMappingStatus.NeedsReview, mapping.Status);
        Assert.Null(mapping.QuestionIndex);
    }

    [Fact]
    public void Without_positions_everything_is_unmapped()
    {
        var questions = new[] { new ParsedQuestion(1, "Q", [], null, null, []) };

        var mapping = MapOne(questions, 1, new VisualBounds(0, 0, 100, 100));

        Assert.Equal(VisualMappingStatus.Unmapped, mapping.Status);
    }
}

public class PdfVisualExtractorTests
{
    [Fact]
    public void Parser_records_question_and_option_positions_without_changing_the_result()
    {
        var plain = new TestPdf()
            .Line("1. First question").Line("A) one").Line("B) two")
            .Line("2. Second question").Line("A) three").Line("B) four")
            .Parse();
        var withImage = new TestPdf()
            .Line("1. First question").Image(new TestImage(120, 80)).Line("A) one").Line("B) two")
            .Line("2. Second question").Line("A) three").Line("B) four")
            .Parse();

        Assert.Equal(plain.Questions.Select(q => q.Text), withImage.Questions.Select(q => q.Text));
        Assert.Equal(plain.Questions[1].ContentHash, withImage.Questions[1].ContentHash); // no image: hash unchanged
        Assert.NotEqual(plain.Questions[0].ContentHash, withImage.Questions[0].ContentHash);

        var first = plain.Questions[0];
        Assert.Equal(new SourceAnchor(1, 800), first.Anchor);
        Assert.Equal([new SourceAnchor(1, 784), new SourceAnchor(1, 768)], first.OptionAnchors);
        Assert.Equal(new SourceAnchor(1, 752), plain.Questions[1].Anchor);
    }

    [Fact]
    public void Extracts_png_with_page_position_and_maps_it_to_the_question_body()
    {
        var bytes = new TestPdf()
            .Line("1. First question").Line("A) one").Line("B) two")
            .Line("2. Which shape is shown?").Image(new TestImage(120, 80, Color: (200, 30, 30))).Line("A) circle").Line("B) square")
            .Build();

        var parsed = new PdfQuestionParser().Parse(new MemoryStream(bytes));
        var extracted = PdfVisualExtractor.Extract(new MemoryStream(bytes));

        Assert.False(extracted.Failed);
        var visual = Assert.Single(extracted.Visuals);
        Assert.Equal(1, visual.PageNumber);
        Assert.Equal("image/png", visual.ContentType);
        Assert.NotNull(visual.Content);
        Assert.Equal((4, 3), (visual.PixelWidth, visual.PixelHeight));
        Assert.Equal(120, visual.Bounds.Width, 1);
        Assert.Equal(new VisualMapping(1, null, VisualMappingStatus.AutoMapped), Assert.Single(VisualMapper.Map(parsed, extracted.Visuals)));
    }

    [Fact]
    public void Image_beside_an_option_is_mapped_to_that_option()
    {
        var bytes = new TestPdf()
            .Line("1. Which picture is red?")
            .Line("A) first").Image(new TestImage(40, 30, Beside: true, Color: (0, 0, 255)))
            .Line("B) second").Image(new TestImage(40, 30, Beside: true, Color: (255, 0, 0)))
            .Build();

        var parsed = new PdfQuestionParser().Parse(new MemoryStream(bytes));
        var mappings = VisualMapper.Map(parsed, PdfVisualExtractor.Extract(new MemoryStream(bytes)).Visuals);

        Assert.Equal([0, 1], mappings.Select(m => m.OptionIndex));
        Assert.All(mappings, m => Assert.Equal(VisualMappingStatus.AutoMapped, m.Status));
    }

    [Fact]
    public void Ignores_tiny_images_and_logos_repeated_on_most_pages()
    {
        var logo = new TestImage(60, 20, Color: (10, 120, 10));
        var bytes = new TestPdf()
            .Image(logo).Line("1. First").Line("A) a").Line("B) b").Image(new TestImage(8, 8))
            .NewPage().Image(logo).Line("2. Second").Line("A) c").Line("B) d")
            .NewPage().Image(logo).Line("3. Third").Image(new TestImage(100, 60, Color: (1, 2, 3))).Line("A) e").Line("B) f")
            .Build();

        var extracted = PdfVisualExtractor.Extract(new MemoryStream(bytes));

        var visual = Assert.Single(extracted.Visuals);
        Assert.Equal(3, visual.PageNumber);
        Assert.Contains(extracted.Warnings, w => w.Contains("təkrarlanan"));
    }

    [Fact]
    public void Unreadable_input_fails_without_throwing()
    {
        var result = PdfVisualExtractor.Extract(new MemoryStream([1, 2, 3]));

        Assert.True(result.Failed);
        Assert.Empty(result.Visuals);
    }

    [Fact]
    public void Same_text_with_different_images_is_not_a_duplicate()
    {
        var result = new TestPdf()
            .Line("1. Which formula is true?").Image(new TestImage(150, 40, Color: (255, 0, 0))).Line("A) .").Line("B) .")
            .Line("2. Which formula is true?").Image(new TestImage(150, 40, Color: (0, 255, 0))).Line("A) .").Line("B) .")
            .Line("3. Which formula is true?").Image(new TestImage(150, 40, Color: (255, 0, 0))).Line("A) .").Line("B) .")
            .Parse();

        Assert.Equal([1, 2], result.Questions.Select(q => q.Number));
        Assert.Equal(1, result.DuplicatesSkipped); // 3 repeats 1: same text and the same image
        Assert.NotEqual(result.Questions[0].ContentHash, result.Questions[1].ContentHash);
    }

    [Fact]
    public void Question_whose_text_is_only_an_image_is_kept_with_a_placeholder()
    {
        var result = new TestPdf()
            .Line("1. .").Image(new TestImage(150, 40, Color: (9, 9, 9))).Line("A) one").Line("B) two")
            .Line("2. .").Image(new TestImage(150, 40, Color: (99, 9, 9))).Line("A) one").Line("B) two")
            .Line("3. Text question").Line("A) one").Line("B) two")
            .Parse();

        Assert.Equal([PdfQuestionParser.ImageOnlyQuestionText, PdfQuestionParser.ImageOnlyQuestionText, "Text question"],
            result.Questions.Select(q => q.Text));
        Assert.Contains("Sual mətni yalnız şəkildir", result.Questions[0].Warning);
    }

    [Fact]
    public void Text_only_hash_is_unchanged_by_the_image_parameter()
    {
        var expected = ContentHasher.Compute("Q", ["a", "b"]);

        Assert.Equal(expected, ContentHasher.Compute("Q", ["a", "b"], null));
        Assert.Equal(expected, ContentHasher.Compute("Q", ["a", "b"], []));
        Assert.NotEqual(expected, ContentHasher.Compute("Q", ["a", "b"], ["AB12"]));
    }
}

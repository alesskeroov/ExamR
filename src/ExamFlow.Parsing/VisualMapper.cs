namespace ExamFlow.Parsing;

public enum VisualMappingStatus
{
    /// <summary>Placed with confidence.</summary>
    AutoMapped,

    /// <summary>A best guess (or no guess) that a person should confirm.</summary>
    NeedsReview,

    /// <summary>Outside every question (title page, before the first question) or positions unknown.</summary>
    Unmapped,
}

/// <summary>
/// Where a visual belongs. <see cref="QuestionIndex"/> indexes <see cref="ParseResult.Questions"/>;
/// <see cref="OptionIndex"/> indexes that question's options (null = question body).
/// </summary>
public sealed record VisualMapping(int? QuestionIndex, int? OptionIndex, VisualMappingStatus Status, string? Reason = null);

/// <summary>
/// Assigns visuals to questions/options by vertical position in reading order (single-column layout, like
/// <see cref="PdfTextExtractor"/>). Pure function, no PDF access. Two rules, applied to questions and then options:
/// <list type="number">
/// <item>A start line (question number / option label) whose baseline lies within the image's height is the
/// owner: the image sits beside it. More than one such line is ambiguous.</item>
/// <item>Otherwise the owner is the last start line at or above the image's top edge.</item>
/// </list>
/// </summary>
public static class VisualMapper
{
    /// <summary>
    /// How far above a start baseline still counts as that start's line: its text height plus a centred table
    /// cell line (see <c>QuestionSegmenter.MaxCellLineDistance</c>).
    /// </summary>
    public const double LineAllowance = 14;

    /// <summary>Anchors closer than this are on the same line.</summary>
    private const double SameLineTolerance = 2;

    private sealed record Start(SourceAnchor Anchor, int? QuestionIndex);

    public static IReadOnlyList<VisualMapping> Map(ParseResult result, IReadOnlyList<ExtractedVisual> visuals) =>
        Map(result.Questions, result.SkippedAnchors, visuals.Select(v => (v.PageNumber, v.Bounds)).ToList());

    public static IReadOnlyList<VisualMapping> Map(
        IReadOnlyList<ParsedQuestion> questions,
        IReadOnlyList<SourceAnchor> skippedAnchors,
        IReadOnlyList<(int PageNumber, VisualBounds Bounds)> visuals)
    {
        if (questions.Count == 0 || questions.Any(q => q.Anchor is null))
        {
            return visuals.Select(_ => new VisualMapping(null, null, VisualMappingStatus.Unmapped, "Sualların mövqeyi məlum deyil")).ToList();
        }

        var starts = questions.Select((q, i) => new Start(q.Anchor!, i))
            .Concat(skippedAnchors.Select(a => new Start(a, null)))
            .OrderBy(s => s.Anchor.PageNumber)
            .ThenByDescending(s => s.Anchor.Y)
            .ToList();
        var anchors = starts.Select(s => s.Anchor).ToList();

        return visuals.Select(v => MapOne(questions, starts, anchors, v.PageNumber, v.Bounds)).ToList();
    }

    private static VisualMapping MapOne(
        IReadOnlyList<ParsedQuestion> questions, List<Start> starts, List<SourceAnchor> anchors, int page, VisualBounds bounds)
    {
        var (owner, doubt) = FindOwner(anchors, page, bounds);
        if (owner < 0)
        {
            return new VisualMapping(null, null, VisualMappingStatus.Unmapped, "İlk sualdan əvvəldir");
        }

        if (starts[owner].QuestionIndex is not { } questionIndex)
        {
            return new VisualMapping(null, null, VisualMappingStatus.NeedsReview, "Atlanmış (təkrar və ya boş) sualın sahəsindədir");
        }

        if (doubt is not null)
        {
            return new VisualMapping(questionIndex, null, VisualMappingStatus.NeedsReview, doubt);
        }

        var question = questions[questionIndex];
        var lastAnchorPage = question.OptionAnchors.Where(a => a is not null).Select(a => a!.PageNumber)
            .Append(question.Anchor!.PageNumber).Max();
        if (page > lastAnchorPage + 1)
        {
            return new VisualMapping(questionIndex, null, VisualMappingStatus.NeedsReview, "Sualdan uzaq səhifədədir");
        }

        return MapToOption(question, questionIndex, page, bounds);
    }

    /// <summary>
    /// Index of the question start (anchors in reading order) that owns the visual, or -1 before the first start;
    /// <c>doubt</c> is set when the choice is a guess.
    /// </summary>
    public static (int Owner, string? Doubt) FindOwner(IReadOnlyList<SourceAnchor> anchors, int page, VisualBounds bounds)
    {
        var beside = Beside(anchors, page, bounds);
        var above = LastAtOrAbove(anchors, page, bounds.Top);

        if (beside.Count == 0 || (beside.Count == 1 && beside[0] == above))
        {
            return (above, null);
        }

        // Beside a question start but reaching higher than its line: probably its own, but not certain.
        return beside.Count == 1
            ? (beside[0], "Bir neçə sualın sərhədindədir")
            : (above >= 0 ? above : beside[0], "Bir neçə sualın yanındadır");
    }

    private static VisualMapping MapToOption(ParsedQuestion question, int questionIndex, int page, VisualBounds bounds)
    {
        if (question.OptionAnchors.Count == 0 || question.OptionAnchors.Any(a => a is null))
        {
            return new VisualMapping(questionIndex, null, VisualMappingStatus.AutoMapped);
        }

        var options = question.OptionAnchors.Select(a => a!).ToList();
        var beside = Beside(options, page, bounds);
        if (beside.Count > 1)
        {
            var sameLine = beside.All(i => Math.Abs(options[i].Y - options[beside[0]].Y) < SameLineTolerance);
            return new VisualMapping(questionIndex, null, VisualMappingStatus.NeedsReview,
                sameLine ? "Eyni sətirdə bir neçə variant var" : "Bir neçə variantın yanındadır");
        }

        var owner = beside.Count == 1 ? beside[0] : LastAtOrAbove(options, page, bounds.Top);
        if (owner < 0)
        {
            return new VisualMapping(questionIndex, null, VisualMappingStatus.AutoMapped); // Above the options: question body.
        }

        var label = options[owner];
        if (options.Where((a, i) => i != owner && a.PageNumber == label.PageNumber && Math.Abs(a.Y - label.Y) < SameLineTolerance).Any())
        {
            return new VisualMapping(questionIndex, null, VisualMappingStatus.NeedsReview, "Eyni sətirdə bir neçə variant var");
        }

        return new VisualMapping(questionIndex, owner, VisualMappingStatus.AutoMapped);
    }

    /// <summary>Indexes of anchors on the page whose baseline lies within the image's height.</summary>
    private static List<int> Beside(IReadOnlyList<SourceAnchor> anchors, int page, VisualBounds bounds) =>
        anchors.Select((a, i) => (a, i))
            .Where(x => x.a.PageNumber == page && x.a.Y >= bounds.Bottom && x.a.Y <= bounds.Top)
            .Select(x => x.i)
            .ToList();

    /// <summary>Index of the last anchor (reading order) whose line is at or above the point, or -1.</summary>
    private static int LastAtOrAbove(IReadOnlyList<SourceAnchor> anchors, int page, double y)
    {
        var index = -1;
        for (var i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            if (a.PageNumber < page || (a.PageNumber == page && y <= a.Y + LineAllowance))
            {
                index = i;
            }
        }

        return index;
    }
}

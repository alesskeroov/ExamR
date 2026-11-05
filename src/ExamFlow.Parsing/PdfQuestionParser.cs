using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace ExamFlow.Parsing;

/// <summary>Entry point: PDF stream → structured questions. Stateless and thread-safe.</summary>
public sealed class PdfQuestionParser(IEnumerable<IAnswerDetector>? detectors = null)
{
    public const int MaxQuestionLength = 4000;
    public const int MaxOptionLength = 1000;
    private const int MinCharactersPerPage = 20;
    private const double MaxUnknownGlyphRatio = 0.05;

    /// <summary>Text of a question whose stem is only an image (its images keep it apart from other such questions).</summary>
    public const string ImageOnlyQuestionText = "[Şəkil]";

    // DI passes an empty sequence when nothing is registered; never run without a detector.
    private readonly IReadOnlyList<IAnswerDetector> _detectors =
        detectors?.ToList() is { Count: > 0 } configured ? configured : DefaultDetectors;

    /// <summary>Explicit markers are more reliable than styling, so they are tried first.</summary>
    public static IReadOnlyList<IAnswerDetector> DefaultDetectors { get; } = [new MarkerAnswerDetector(), new StyleAnswerDetector()];

    public static PdfInspection Inspect(Stream pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = true });
            return new PdfInspection(true, false, document.NumberOfPages);
        }
        catch (PdfDocumentEncryptedException)
        {
            return new PdfInspection(false, true, 0);
        }
        catch (Exception)
        {
            return new PdfInspection(false, false, 0);
        }
    }

    public ParseResult Parse(Stream pdf, CancellationToken ct = default)
    {
        PdfDocument document;
        try
        {
            document = PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = true });
        }
        catch (PdfDocumentEncryptedException)
        {
            return ParseResult.Failed(ParseFailure.Encrypted);
        }
        catch (Exception)
        {
            return ParseResult.Failed(ParseFailure.Unreadable);
        }

        using (document)
        {
            PdfTextContent content;
            try
            {
                content = PdfTextExtractor.Extract(document, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ParseResult.Failed(ParseFailure.Unreadable, document.NumberOfPages);
            }

            if (content.CharacterCount < MinCharactersPerPage * Math.Max(1, content.PageCount))
            {
                return ParseResult.Failed(ParseFailure.Scanned, content.PageCount);
            }

            return ParseLines(content.Lines, content.PageCount, LocateVisuals(document, ct));
        }
    }

    /// <summary>Images only refine duplicate detection; if they cannot be read, parsing continues text-only.</summary>
    private static IReadOnlyList<VisualLocation> LocateVisuals(PdfDocument document, CancellationToken ct)
    {
        try
        {
            return PdfVisualExtractor.Locate(document, ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    /// <summary>Parses already extracted lines; used directly by tests.</summary>
    public ParseResult ParseLines(IReadOnlyList<TextLine> lines, int pageCount, IReadOnlyList<VisualLocation>? visuals = null)
    {
        var warnings = new List<string>();
        var allText = string.Concat(lines.Select(l => l.Text));
        if (allText.Length > 0 && (double)allText.Count(c => c == '�') / allText.Length > MaxUnknownGlyphRatio)
        {
            warnings.Add("Şrift kodlaşdırması problemi: bəzi simvollar düzgün oxunmadı");
        }

        var questions = new List<ParsedQuestion>();
        var seenHashes = new HashSet<string>();
        var duplicates = 0;
        var skippedAnchors = new List<SourceAnchor>();

        var raws = QuestionSegmenter.Segment(LayoutCleaner.Clean(lines));
        var visualKeys = VisualKeysByQuestion(raws, visuals ?? []);

        foreach (var raw in raws)
        {
            var questionVisuals = visualKeys.GetValueOrDefault(raw);
            var questionWarnings = new List<string>(raw.Warnings);
            var questionText = Join(raw.TextTokens);
            string? answerText = raw.ReadingAnswer ? Join(raw.AnswerTokens) : null;
            if (raw.Options.Count == 0 && answerText is null)
            {
                var separator = Regex.Match(questionText, @"\s+(?:cavab|[iİıI]zah|answer)\s*[:：]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (separator.Success)
                {
                    answerText = questionText[(separator.Index + separator.Length)..];
                    questionText = questionText[..separator.Index];
                }
            }
            var text = Truncate(questionText, MaxQuestionLength, "Sual mətni", questionWarnings);
            // Formula images often leave only stray punctuation ("." or "..") in the text layer.
            if (questionVisuals is not null && !text.Any(char.IsLetterOrDigit))
            {
                text = ImageOnlyQuestionText;
                questionWarnings.Add("Sual mətni yalnız şəkildir");
            }

            if (text.Length == 0)
            {
                warnings.Add($"Sual {raw.Number}: mətn boşdur, atlandı");
                if (raw.Anchor is not null) skippedAnchors.Add(raw.Anchor);
                continue;
            }

            var options = raw.Options
                .Select(o => new ParsedOption(o.Letter.ToString(), Truncate(Join(o.TextTokens), MaxOptionLength, $"Variant {o.Letter}", questionWarnings)))
                .ToList();

            var explanation = !string.IsNullOrWhiteSpace(answerText) ? Truncate(answerText, 16000, "Cavab mətni", questionWarnings) : null;
            if (options.Count < 2 && explanation is null)
            {
                questionWarnings.Add($"Yalnız {options.Count} variant tapıldı");
            }

            var detection = explanation is null ? Detect(raw) : new AnswerDetection(null, null);
            if (detection.Warning is not null)
            {
                questionWarnings.Add(detection.Warning);
            }

            var hash = ContentHasher.Compute(text, options.Select(o => o.Text).Concat(explanation is null ? [] : new[] { explanation }),
                questionVisuals);
            if (!seenHashes.Add(Convert.ToHexString(hash)))
            {
                duplicates++;
                if (raw.Anchor is not null) skippedAnchors.Add(raw.Anchor);
                continue;
            }

            var warning = questionWarnings.Count > 0 ? string.Join("; ", questionWarnings) : null;
            if (warning is not null)
            {
                warnings.Add($"Sual {raw.Number}: {warning}");
            }

            questions.Add(new ParsedQuestion(raw.Number, text, options, detection.CorrectIndex, warning, hash)
            {
                Explanation = explanation,
                Anchor = raw.Anchor,
                OptionAnchors = raw.Options.Select(o => o.Anchor).ToList(),
            });
        }

        return new ParseResult
        {
            Questions = questions,
            Warnings = warnings,
            PageCount = pageCount,
            DuplicatesSkipped = duplicates,
            SkippedAnchors = skippedAnchors,
            Failure = questions.Count == 0 ? ParseFailure.NoQuestions : ParseFailure.None,
        };
    }

    /// <summary>
    /// Image hashes per question, by the same ownership rule as <see cref="VisualMapper"/>. Questions whose text is
    /// only placeholders (e.g. formulas drawn as images) are then not dropped as duplicates of each other.
    /// </summary>
    private static Dictionary<RawQuestion, List<string>> VisualKeysByQuestion(IReadOnlyList<RawQuestion> raws, IReadOnlyList<VisualLocation> visuals)
    {
        var result = new Dictionary<RawQuestion, List<string>>(ReferenceEqualityComparer.Instance);
        if (visuals.Count == 0 || raws.Count == 0 || raws.Any(r => r.Anchor is null))
        {
            return result;
        }

        var ordered = raws.OrderBy(r => r.Anchor!.PageNumber).ThenByDescending(r => r.Anchor!.Y).ToList();
        var anchors = ordered.Select(r => r.Anchor!).ToList();
        foreach (var visual in visuals)
        {
            var (owner, _) = VisualMapper.FindOwner(anchors, visual.PageNumber, visual.Bounds);
            if (owner < 0)
            {
                continue;
            }

            if (!result.TryGetValue(ordered[owner], out var keys))
            {
                result[ordered[owner]] = keys = [];
            }

            keys.Add(Convert.ToHexString(visual.RawHash));
        }

        return result;
    }

    private AnswerDetection Detect(RawQuestion question)
    {
        string? warning = null;
        foreach (var detector in _detectors)
        {
            var result = detector.Detect(question);
            if (result.CorrectIndex is not null)
            {
                return result;
            }

            warning ??= result.Warning;
        }

        return new AnswerDetection(null, warning);
    }

    private static string Join(IEnumerable<TextToken> tokens) => string.Join(' ', tokens.Select(t => t.Text)).Trim();

    private static string Truncate(string text, int maxLength, string field, List<string> warnings)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        warnings.Add($"{field} {maxLength} simvoldan uzundur, qısaldıldı");
        return text[..maxLength];
    }
}

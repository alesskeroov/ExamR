namespace ExamFlow.Parsing;

/// <summary>A word with the style information needed for answer detection.</summary>
public sealed record TextToken(string Text, bool IsBold, string ColorKey)
{
    public const string DefaultColor = "default";

    public static TextToken Plain(string text) => new(text, false, DefaultColor);
}

public sealed class TextLine(int pageNumber, IReadOnlyList<TextToken> tokens, double y = double.NaN)
{
    public int PageNumber { get; } = pageNumber;
    public IReadOnlyList<TextToken> Tokens { get; } = tokens;

    /// <summary>Baseline in PDF points (larger is higher on the page); NaN when unknown.</summary>
    public double Y { get; } = y;

    /// <summary>Tokens joined by single spaces; regex indices on this string map back to tokens.</summary>
    public string Text { get; } = string.Join(' ', tokens.Select(t => t.Text));
}

/// <summary>Where a question or option starts in the PDF: page and baseline Y in PDF points (larger is higher).</summary>
public sealed record SourceAnchor(int PageNumber, double Y)
{
    /// <summary>Null for lines without a known position (DOCX, test lines).</summary>
    public static SourceAnchor? Of(TextLine line) => double.IsNaN(line.Y) ? null : new(line.PageNumber, line.Y);
}

public sealed class RawQuestion(int number)
{
    public int Number { get; } = number;
    public SourceAnchor? Anchor { get; set; }
    public List<TextToken> TextTokens { get; } = [];
    public List<RawOption> Options { get; } = [];
    public List<TextToken> AnswerTokens { get; } = [];
    public bool ReadingAnswer { get; set; }
    public List<string> Warnings { get; } = [];
}

public sealed class RawOption(char letter)
{
    public char Letter { get; } = letter;
    public SourceAnchor? Anchor { get; set; }
    public List<TextToken> LabelTokens { get; } = [];
    public List<TextToken> TextTokens { get; } = [];
}

public sealed record ParsedOption(string Label, string Text);

/// <summary>
/// A shared text (reading passage) that several questions refer to: "Read the passage and answer questions 18 – 21."
/// Each line keeps its own text so paragraphs can be rebuilt.
/// </summary>
public sealed class RawPassage(int fromNumber, int toNumber, string title)
{
    public int FromNumber { get; } = fromNumber;
    public int ToNumber { get; } = toNumber;
    public string Title { get; } = title;
    public List<string> Lines { get; } = [];
}

/// <summary>A passage for questions <see cref="FromNumber"/>…<see cref="ToNumber"/> (original numbers, inclusive).</summary>
public sealed record ParsedPassage(int FromNumber, int ToNumber, string Title, string Text);

public sealed record ParsedQuestion(
    int Number,
    string Text,
    IReadOnlyList<ParsedOption> Options,
    int? CorrectIndex,
    string? Warning,
    byte[] ContentHash)
{
    public string? Explanation { get; init; }

    /// <summary>Position metadata for attaching visuals; never affects parsing or <see cref="ContentHash"/>.</summary>
    public SourceAnchor? Anchor { get; init; }

    /// <summary>One entry per option, same order as <see cref="Options"/>.</summary>
    public IReadOnlyList<SourceAnchor?> OptionAnchors { get; init; } = [];
}

public enum ParseFailure
{
    None,
    Encrypted,
    Unreadable,
    Scanned,
    NoQuestions,
}

public sealed class ParseResult
{
    public IReadOnlyList<ParsedQuestion> Questions { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int PageCount { get; init; }
    public int DuplicatesSkipped { get; init; }

    /// <summary>Reading passages found in the file; never part of a question's text or options.</summary>
    public IReadOnlyList<ParsedPassage> Passages { get; init; } = [];

    /// <summary>Starts of segmented questions that were dropped (empty or duplicate); they still bound visual regions.</summary>
    public IReadOnlyList<SourceAnchor> SkippedAnchors { get; init; } = [];
    public ParseFailure Failure { get; init; }
    public bool IsSuccess => Failure == ParseFailure.None;

    public static ParseResult Failed(ParseFailure failure, int pageCount = 0) =>
        new() { Failure = failure, PageCount = pageCount };
}

public sealed record PdfInspection(bool IsReadable, bool IsEncrypted, int PageCount);

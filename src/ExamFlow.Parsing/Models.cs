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

public sealed class RawQuestion(int number)
{
    public int Number { get; } = number;
    public List<TextToken> TextTokens { get; } = [];
    public List<RawOption> Options { get; } = [];
    public List<string> Warnings { get; } = [];
}

public sealed class RawOption(char letter)
{
    public char Letter { get; } = letter;
    public List<TextToken> LabelTokens { get; } = [];
    public List<TextToken> TextTokens { get; } = [];
}

public sealed record ParsedOption(string Label, string Text);

public sealed record ParsedQuestion(
    int Number,
    string Text,
    IReadOnlyList<ParsedOption> Options,
    int? CorrectIndex,
    string? Warning,
    byte[] ContentHash);

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
    public ParseFailure Failure { get; init; }
    public bool IsSuccess => Failure == ParseFailure.None;

    public static ParseResult Failed(ParseFailure failure, int pageCount = 0) =>
        new() { Failure = failure, PageCount = pageCount };
}

public sealed record PdfInspection(bool IsReadable, bool IsEncrypted, int PageCount);

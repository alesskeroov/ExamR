using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExamFlow.Parsing.Tests;

/// <summary>Style of one run of text inside a generated test line.</summary>
public sealed record Run(string Text, bool Bold = false, (byte R, byte G, byte B)? Color = null);

/// <summary>Builds real PDFs (with real bold fonts and colours) for parser tests.</summary>
public sealed class TestPdf
{
    private const double FontSize = 11;
    private const double LineHeight = 16;
    private readonly List<List<Run[]>> _pages = [[]];
    private bool _unicode;

    /// <summary>Uses Arial (embedded TrueType) so Azerbaijani letters can be written. Windows only.</summary>
    public static bool UnicodeFontsAvailable => File.Exists(ArialPath) && File.Exists(ArialBoldPath);

    private static string ArialPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
    private static string ArialBoldPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf");

    public TestPdf WithUnicodeFonts()
    {
        _unicode = true;
        return this;
    }

    public TestPdf Line(string text) => Line(new Run(text));

    public TestPdf Line(params Run[] runs)
    {
        _pages[^1].Add(runs);
        return this;
    }

    public TestPdf NewPage()
    {
        _pages.Add([]);
        return this;
    }

    public byte[] Build()
    {
        using var builder = new PdfDocumentBuilder();
        var regular = _unicode ? builder.AddTrueTypeFont(File.ReadAllBytes(ArialPath)) : builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = _unicode ? builder.AddTrueTypeFont(File.ReadAllBytes(ArialBoldPath)) : builder.AddStandard14Font(Standard14Font.HelveticaBold);

        foreach (var lines in _pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 800.0;
            foreach (var runs in lines)
            {
                var x = 50.0;
                foreach (var run in runs)
                {
                    var (r, g, b) = run.Color ?? (0, 0, 0);
                    page.SetTextAndFillColor(r, g, b);
                    var letters = page.AddText(run.Text, FontSize, new PdfPoint(x, y), run.Bold ? bold : regular);
                    x = (letters.Count > 0 ? letters[^1].EndBaseLine.X : x) + FontSize * 0.3;
                }

                page.ResetColor();
                y -= LineHeight;
            }
        }

        return builder.Build();
    }

    public ParseResult Parse() => new PdfQuestionParser().Parse(new MemoryStream(Build()));
}

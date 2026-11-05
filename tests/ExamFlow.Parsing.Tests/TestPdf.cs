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

    // Images per page, keyed by the number of text lines written before them on that page.
    private readonly List<List<(int AfterLines, TestImage Image)>> _images = [[]];
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
        _images.Add([]);
        return this;
    }

    /// <summary>
    /// Draws a PNG. <see cref="TestImage.Beside"/> = false: below the last line, pushing later lines down;
    /// true: right of the last line, vertically centred on its baseline.
    /// </summary>
    public TestPdf Image(TestImage image)
    {
        _images[^1].Add((_pages[^1].Count, image));
        return this;
    }

    public byte[] Build()
    {
        using var builder = new PdfDocumentBuilder();
        var regular = _unicode ? builder.AddTrueTypeFont(File.ReadAllBytes(ArialPath)) : builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = _unicode ? builder.AddTrueTypeFont(File.ReadAllBytes(ArialBoldPath)) : builder.AddStandard14Font(Standard14Font.HelveticaBold);

        for (var p = 0; p < _pages.Count; p++)
        {
            var lines = _pages[p];
            var page = builder.AddPage(PageSize.A4);
            var y = 800.0;
            for (var l = 0; l <= lines.Count; l++)
            {
                foreach (var (_, image) in _images[p].Where(i => i.AfterLines == l))
                {
                    var png = TestImage.Png(image.PixelWidth, image.PixelHeight, image.Color);
                    if (image.Beside)
                    {
                        var baseline = y + LineHeight; // The last written line.
                        page.AddPng(png, new PdfRectangle(300, baseline - image.Height / 2, 300 + image.Width, baseline + image.Height / 2));
                    }
                    else
                    {
                        var top = y + LineHeight - 4; // Just below the last line's descenders.
                        page.AddPng(png, new PdfRectangle(50, top - image.Height, 50 + image.Width, top));
                        y -= image.Height;
                    }
                }

                if (l == lines.Count)
                {
                    break;
                }

                var runs = lines[l];
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

/// <summary>A solid-colour PNG drawn into a test PDF; size in PDF points.</summary>
public sealed record TestImage(double Width, double Height, bool Beside = false, (byte R, byte G, byte B) Color = default, int PixelWidth = 4, int PixelHeight = 3)
{
    /// <summary>Minimal RGB PNG encoder, so tests need no imaging package.</summary>
    public static byte[] Png(int width, int height, (byte R, byte G, byte B) color)
    {
        var raw = new byte[height * (1 + width * 3)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 3);
            for (var x = 0; x < width; x++)
            {
                raw[row + 1 + x * 3] = color.R;
                raw[row + 2 + x * 3] = color.G;
                raw[row + 3 + x * 3] = color.B;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeAndData = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typeAndData);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length, Crc32(typeAndData));
        stream.Write(length);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}

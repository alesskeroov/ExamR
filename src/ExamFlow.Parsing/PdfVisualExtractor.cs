using System.Security.Cryptography;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace ExamFlow.Parsing;

/// <summary>Area of a visual on its page in PDF points (origin bottom-left, larger Y is higher).</summary>
public sealed record VisualBounds(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;
}

/// <summary>
/// A raster image found in the PDF. <see cref="Content"/> is null when the image could not be converted to a
/// browser format (JBIG2, CCITT, JPEG 2000, too large); the location is kept so the UI can point to the PDF page.
/// </summary>
public sealed record ExtractedVisual(
    int PageNumber,
    VisualBounds Bounds,
    byte[]? Content,
    string? ContentType,
    byte[] Sha256,
    int PixelWidth,
    int PixelHeight,
    string? UnsupportedReason);

/// <summary>Position and encoded-stream hash of a content image; see <see cref="PdfVisualExtractor.Locate"/>.</summary>
public sealed record VisualLocation(int PageNumber, VisualBounds Bounds, byte[] RawHash);

public sealed record VisualExtractionResult(IReadOnlyList<ExtractedVisual> Visuals, IReadOnlyList<string> Warnings, bool Failed)
{
    public static VisualExtractionResult Empty { get; } = new([], [], false);
}

/// <summary>Limits that keep extraction cheap on small hosts.</summary>
public sealed record VisualExtractionOptions
{
    public int MaxVisuals { get; init; } = 3000;
    public long MaxTotalBytes { get; init; } = 40L * 1024 * 1024;
    public int MaxBytesPerVisual { get; init; } = 5 * 1024 * 1024;

    /// <summary>Images with more samples are not decoded (PNG conversion holds the whole bitmap in memory).</summary>
    public long MaxPixels { get; init; } = 4_000_000;

    /// <summary>Smaller images are bullets, icons or rules, not question content.</summary>
    public double MinSidePoints { get; init; } = 12;
    public double MinAreaPoints { get; init; } = 400;

    /// <summary>Images covering more of the page are page backgrounds or scans.</summary>
    public double MaxPageCoverage { get; init; } = 0.85;
}

/// <summary>
/// Extracts embedded raster images with their page positions. Separate from <see cref="PdfQuestionParser"/> so text
/// parsing is unchanged; vector drawings are not handled here (PdfPig cannot rasterize).
/// </summary>
public static class PdfVisualExtractor
{
    private const string JpegFilter = "DCTDecode";
    private const string JpegContentType = "image/jpeg";
    private const string PngContentType = "image/png";

    public static VisualExtractionResult Extract(Stream pdf, VisualExtractionOptions? options = null, CancellationToken ct = default)
    {
        PdfDocument document;
        try
        {
            document = PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = true });
        }
        catch (Exception)
        {
            return new VisualExtractionResult([], ["Şəkillər oxunmadı: PDF açıla bilmədi"], true);
        }

        using (document)
        {
            return Extract(document, options ?? new VisualExtractionOptions(), ct);
        }
    }

    public static VisualExtractionResult Extract(PdfDocument document, VisualExtractionOptions options, CancellationToken ct)
    {
        var warnings = new List<string>();
        var visuals = new List<ExtractedVisual>();
        long totalBytes = 0;
        var unsupported = 0;

        foreach (var (pageNumber, image, bounds, rawHash) in Candidates(document, options, warnings, ct))
        {
            if (visuals.Count >= options.MaxVisuals)
            {
                warnings.Add($"Şəkil limiti ({options.MaxVisuals}) aşıldı; qalan şəkillər götürülmədi");
                break;
            }

            var visual = ToVisual(pageNumber, bounds, image, rawHash, options);
            if (visual.Content is not null)
            {
                if (totalBytes + visual.Content.Length > options.MaxTotalBytes)
                {
                    warnings.Add("Şəkillərin ümumi həcm limiti aşıldı; qalan şəkillər götürülmədi");
                    break;
                }

                totalBytes += visual.Content.Length;
            }
            else
            {
                unsupported++;
            }

            visuals.Add(visual);
        }

        if (unsupported > 0)
        {
            warnings.Add($"{unsupported} şəkil brauzer formatına çevrilmədi; yalnız PDF səhifəsində baxmaq olar");
        }

        return new VisualExtractionResult(visuals, warnings, false);
    }

    /// <summary>
    /// Same images as <see cref="Extract(PdfDocument, VisualExtractionOptions, CancellationToken)"/> (same filters), but only
    /// positions and hashes of the encoded streams: nothing is decoded. Used by the parser to tell apart questions whose
    /// text is identical but whose images differ.
    /// </summary>
    public static IReadOnlyList<VisualLocation> Locate(PdfDocument document, VisualExtractionOptions? options = null, CancellationToken ct = default) =>
        Candidates(document, options ?? new VisualExtractionOptions(), [], ct)
            .Select(c => new VisualLocation(c.PageNumber, c.Bounds, c.RawHash))
            .ToList();

    /// <summary>Content images in reading order, after size, background, logo and same-place filters.</summary>
    private static IEnumerable<(int PageNumber, IPdfImage Image, VisualBounds Bounds, byte[] RawHash)> Candidates(
        PdfDocument document, VisualExtractionOptions options, List<string> warnings, CancellationToken ct)
    {
        var repeated = FindRepeatedImages(document, warnings, ct);
        var seen = new HashSet<string>();

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            List<IPdfImage> images;
            try
            {
                images = page.GetImages().ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add($"Səhifə {page.Number}: şəkillər oxunmadı");
                continue;
            }

            var pageArea = page.Width * page.Height;
            foreach (var image in images)
            {
                ct.ThrowIfCancellationRequested();
                var bounds = Normalize(image.BoundingBox);
                if (bounds.Width < options.MinSidePoints || bounds.Height < options.MinSidePoints
                    || bounds.Width * bounds.Height < options.MinAreaPoints
                    || (pageArea > 0 && bounds.Width * bounds.Height > pageArea * options.MaxPageCoverage))
                {
                    continue;
                }

                var rawHash = SHA256.HashData(image.RawMemory.Span);
                var rawKey = Convert.ToHexString(rawHash);
                if (repeated.Contains(rawKey)
                    || !seen.Add($"{page.Number}|{rawKey}|{Math.Round(bounds.Left)}|{Math.Round(bounds.Bottom)}"))
                {
                    continue; // Logo/watermark on most pages, or the same image drawn twice in one place.
                }

                yield return (page.Number, image, bounds, rawHash);
            }
        }
    }

    private static ExtractedVisual ToVisual(int pageNumber, VisualBounds bounds, IPdfImage image, byte[] rawHash, VisualExtractionOptions options)
    {
        var width = image.WidthInSamples;
        var height = image.HeightInSamples;
        ExtractedVisual Unsupported(string reason) => new(pageNumber, bounds, null, null, rawHash, width, height, reason);

        var filters = Filters(image.ImageDictionary);
        if (filters is [JpegFilter])
        {
            // The stream is a complete JPEG file: store it as is, without decoding.
            var raw = image.RawMemory;
            return raw.Length > options.MaxBytesPerVisual
                ? Unsupported("Şəkil çox böyükdür")
                : new ExtractedVisual(pageNumber, bounds, raw.ToArray(), JpegContentType, SHA256.HashData(raw.Span), width, height, null);
        }

        if ((long)width * height > options.MaxPixels)
        {
            return Unsupported("Şəkil çox böyükdür");
        }

        byte[]? png;
        try
        {
            if (!image.TryGetPng(out png))
            {
                png = null;
            }
        }
        catch (Exception)
        {
            png = null;
        }

        if (png is null || png.Length == 0)
        {
            var filter = filters.Count > 0 ? filters[^1] : null;
            return Unsupported(filter is null ? "Şəkil formatı dəstəklənmir" : $"Şəkil formatı dəstəklənmir ({filter})");
        }

        return png.Length > options.MaxBytesPerVisual
            ? Unsupported("Şəkil çox böyükdür")
            : new ExtractedVisual(pageNumber, bounds, png, PngContentType, SHA256.HashData(png), width, height, null);
    }

    /// <summary>
    /// Images drawn on at least half of the pages (min. 3) are headers, logos or watermarks.
    /// Hashing the encoded stream is cheap; nothing is decoded in this pass.
    /// </summary>
    private static HashSet<string> FindRepeatedImages(PdfDocument document, List<string> warnings, CancellationToken ct)
    {
        var pagesByHash = new Dictionary<string, HashSet<int>>();
        if (document.NumberOfPages < 3)
        {
            return [];
        }

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                foreach (var image in page.GetImages())
                {
                    var key = Convert.ToHexString(SHA256.HashData(image.RawMemory.Span));
                    if (!pagesByHash.TryGetValue(key, out var pages))
                    {
                        pagesByHash[key] = pages = [];
                    }

                    pages.Add(page.Number);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Reported once in the main pass.
            }
        }

        var repeated = pagesByHash
            .Where(p => p.Value.Count >= 3 && p.Value.Count * 2 >= document.NumberOfPages)
            .Select(p => p.Key)
            .ToHashSet();
        if (repeated.Count > 0)
        {
            warnings.Add($"{repeated.Count} təkrarlanan şəkil (loqo/fon) nəzərə alınmadı");
        }

        return repeated;
    }

    private static IReadOnlyList<string> Filters(DictionaryToken dictionary)
    {
        if (!dictionary.TryGet(NameToken.Filter, out var token) && !dictionary.TryGet(NameToken.Create("F"), out token))
        {
            return [];
        }

        return token switch
        {
            NameToken name => [name.Data],
            ArrayToken array => array.Data.OfType<NameToken>().Select(n => n.Data).ToList(),
            _ => [],
        };
    }

    /// <summary>Bounds may be rotated; use the axis-aligned box around all corners.</summary>
    private static VisualBounds Normalize(PdfRectangle r)
    {
        double[] xs = [r.TopLeft.X, r.TopRight.X, r.BottomLeft.X, r.BottomRight.X];
        double[] ys = [r.TopLeft.Y, r.TopRight.Y, r.BottomLeft.Y, r.BottomRight.Y];
        return new VisualBounds(xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }
}

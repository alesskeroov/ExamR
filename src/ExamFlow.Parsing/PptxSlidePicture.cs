using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ExamFlow.Parsing;

/// <summary>The image of one slide, read straight from the presentation file.</summary>
public sealed record SlidePicture(byte[] Content, string ContentType);

public static partial class LearningTextExtractor
{
    /// <summary>Largest picture a slide may serve; bigger files are left out rather than loaded into memory.</summary>
    public const long MaxSlidePictureBytes = 15 * 1024 * 1024;

    private static readonly Dictionary<string, string> PictureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif",
        [".webp"] = "image/webp", [".bmp"] = "image/bmp",
    };

    /// <summary>
    /// The main picture of slide <paramref name="number"/> (1-based, presentation order — the page numbers of
    /// <see cref="FromPptx"/>): the largest picture placed on it. Null when the slide has no picture in a format browsers
    /// show (EMF/WMF drawings are not), or the file is not a readable presentation.
    /// </summary>
    public static SlidePicture? SlidePicture(Stream pptx, int number, TextExtractionLimits limits)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var archive = new ZipArchive(pptx, ZipArchiveMode.Read, leaveOpen: true);
            XDocument? Load(string path)
            {
                if (archive.GetEntry(path) is not { } entry) return null;
                using var bounded = new BoundedStream(entry.Open(), limits.MaxDocxExpandedBytes);
                return XDocument.Load(XmlReader.Create(bounded, settings));
            }

            var presentation = Load("ppt/presentation.xml");
            var targets = Relationships(Load("ppt/_rels/presentation.xml.rels"), "ppt/");
            var slides = presentation?.Root?.Element(P + "sldIdLst")?.Elements(P + "sldId")
                .Select(s => targets.GetValueOrDefault((string?)s.Attribute(R + "id") ?? "")).Where(p => p is not null).Select(p => p!).ToList() ?? [];
            if (number < 1 || number > slides.Count) return null;
            var slidePath = slides[number - 1];
            var slide = Load(slidePath);
            if (slide?.Root is null) return null;

            var slash = slidePath.LastIndexOf('/');
            var pictures = Relationships(Load($"{slidePath[..(slash + 1)]}_rels/{slidePath[(slash + 1)..]}.rels"), slidePath[..(slash + 1)]);
            // Largest by the size it is shown at on the slide; a full-slide picture wins over logos and icons.
            var best = slide.Root.Descendants(P + "pic")
                .Select(pic => new
                {
                    Id = (string?)pic.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed"),
                    Extent = pic.Element(P + "spPr")?.Element(A + "xfrm")?.Element(A + "ext"),
                })
                .Where(p => p.Id is not null && pictures.ContainsKey(p.Id))
                .Select(p => new { Path = pictures[p.Id!], Area = (double?)(long?)p.Extent?.Attribute("cx") * (double?)(long?)p.Extent?.Attribute("cy") ?? 0 })
                .Where(p => PictureTypes.ContainsKey(Path.GetExtension(p.Path)))
                .OrderByDescending(p => p.Area)
                .FirstOrDefault();
            if (best is null || archive.GetEntry(best.Path) is not { } image || image.Length > MaxSlidePictureBytes) return null;

            using var source = new BoundedStream(image.Open(), MaxSlidePictureBytes);
            using var copy = new MemoryStream((int)image.Length);
            source.CopyTo(copy);
            return new SlidePicture(copy.ToArray(), PictureTypes[Path.GetExtension(best.Path)]);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException) { return null; }
    }
}

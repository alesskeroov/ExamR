using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Questions;

/// <summary>
/// An image as the client sees it. The client builds the file URL from <see cref="Id"/> and the context it came from
/// (document, exam or battle room). Without a file (<see cref="HasFile"/> = false) it links to <see cref="PageNumber"/>.
/// </summary>
public sealed record VisualDto(Guid Id, Guid? OptionId, int PageNumber, double Width, double Height, bool HasFile, bool NeedsReview);

public static class QuestionVisuals
{
    /// <summary>Visuals attached to the given questions, in reading order. Ignored and unmapped ones are left out.</summary>
    public static async Task<ILookup<Guid, QuestionVisual>> ForQuestionsAsync(AppDbContext db, IReadOnlyCollection<Guid> questionIds, CancellationToken ct)
    {
        if (questionIds.Count == 0)
        {
            return Array.Empty<QuestionVisual>().ToLookup(v => v.QuestionId!.Value);
        }

        var visuals = await db.QuestionVisuals.AsNoTracking()
            .Where(v => v.QuestionId != null && questionIds.Contains(v.QuestionId.Value) && v.MappingStatus != QuestionVisualStatus.Ignored)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(ct);
        return visuals.ToLookup(v => v.QuestionId!.Value);
    }

    /// <summary>Option ids are matched by position (the order options were parsed in, see <see cref="AnswerOption.Position"/>).</summary>
    public static VisualDto ToDto(QuestionVisual visual, IReadOnlyList<Guid> optionIdsByPosition) => new(
        visual.Id,
        visual.OptionPosition is byte p && p < optionIdsByPosition.Count ? optionIdsByPosition[p] : null,
        visual.PageNumber, visual.Width, visual.Height, visual.StorageKey is not null,
        visual.MappingStatus == QuestionVisualStatus.NeedsReview);

    public static VisualSnapshot ToSnapshot(QuestionVisual visual, IReadOnlyList<AnswerOption> options) => new()
    {
        VisualId = visual.Id,
        OptionId = options.FirstOrDefault(o => o.Position == visual.OptionPosition)?.Id,
        StorageKey = visual.StorageKey,
        ContentType = visual.ContentType,
        PageNumber = visual.PageNumber,
        Width = visual.Width,
        Height = visual.Height,
    };

    public static VisualDto ToDto(VisualSnapshot visual) =>
        new(visual.VisualId, visual.OptionId, visual.PageNumber, visual.Width, visual.Height, visual.StorageKey is not null, false);

    /// <summary>Streams a stored image. Callers check permissions first; a missing file is a 404, never a 500.</summary>
    public static IResult File(IFileStorage storage, HttpResponse response, string? storageKey, string? contentType)
    {
        if (storageKey is null || contentType is null)
        {
            return TypedResults.Problem("Şəkil tapılmadı.", statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            var stream = storage.OpenRead(storageKey);
            // Only this user's browser may cache it, and only briefly: after a document is deleted or a share is
            // revoked, cached copies should not outlive the access for long.
            response.Headers.CacheControl = "private, max-age=3600";
            return TypedResults.Stream(stream, contentType);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return TypedResults.Problem("Şəkil tapılmadı.", statusCode: StatusCodes.Status404NotFound);
        }
    }
}

/// <summary>
/// An image found in a PDF and where it belongs. The file lives in <c>IFileStorage</c> under <see cref="StorageKey"/>
/// (never in the database); access always goes through the document's permissions.
/// </summary>
public class QuestionVisual
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public UploadedDocument Document { get; set; } = null!;

    /// <summary>Null when unmapped or the question could not be determined.</summary>
    public Guid? QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary><see cref="AnswerOption.Position"/> of the option the image belongs to; null = question body.</summary>
    public byte? OptionPosition { get; set; }

    /// <summary>Reading order within the document.</summary>
    public int SortOrder { get; set; }

    public int PageNumber { get; set; }

    /// <summary>Area on the page in PDF points, origin bottom-left.</summary>
    public double Left { get; set; }
    public double Bottom { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public QuestionVisualStatus MappingStatus { get; set; }

    /// <summary>Why the mapping needs review, or why there is no file (unsupported format, too large, not saved).</summary>
    public string? Note { get; set; }

    /// <summary>Null when the image could not be converted to a browser format; the UI then links to the PDF page.</summary>
    public string? StorageKey { get; set; }
    public string? ContentType { get; set; }
    public int ByteSize { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public byte[] Sha256 { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public enum QuestionVisualStatus
{
    AutoMapped,
    NeedsReview,
    Unmapped,
    ManuallyMapped,
    Ignored,
}

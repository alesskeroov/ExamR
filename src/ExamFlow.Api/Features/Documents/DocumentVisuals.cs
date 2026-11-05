using System.Collections.Concurrent;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;

namespace ExamFlow.Api.Features.Documents;

public sealed record VisualImport(IReadOnlyList<QuestionVisual> Visuals, IReadOnlyList<string> Warnings)
{
    public static VisualImport None { get; } = new([], []);
}

/// <summary>
/// Extracts a PDF's images, maps them to the freshly parsed questions and stores the files. Never throws for
/// image problems: questions are saved even when no image could be read.
/// </summary>
public static class DocumentVisuals
{
    private const int UploadParallelism = 4;
    private const int MaxNoteLength = 200;

    /// <summary>Content-addressed per document, so reprocessing rewrites the same keys instead of piling up files.</summary>
    public static string StorageKey(Guid documentId, byte[] sha256, string contentType) =>
        $"qv_{documentId:N}_{Convert.ToHexString(sha256).ToLowerInvariant()}{(contentType == "image/jpeg" ? ".jpg" : ".png")}";

    public static async Task<VisualImport> ImportAsync(
        Guid documentId,
        Stream pdf,
        ParseResult result,
        IReadOnlyList<Question> questions,
        IReadOnlySet<string> storedKeys,
        IFileStorage storage,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken timeout,
        CancellationToken ct)
    {
        VisualExtractionResult extracted;
        try
        {
            pdf.Position = 0;
            extracted = await Task.Run(() => PdfVisualExtractor.Extract(pdf, ct: timeout), timeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new VisualImport([], ["Şəkillər vaxtında çıxarıla bilmədi; suallar şəkilsiz saxlanıldı"]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Visual extraction failed for document {DocumentId}", documentId);
            return new VisualImport([], ["Şəkillər oxuna bilmədi; suallar şəkilsiz saxlanıldı"]);
        }

        if (extracted.Visuals.Count == 0)
        {
            return new VisualImport([], extracted.Warnings);
        }

        var mappings = VisualMapper.Map(result, extracted.Visuals);
        var files = extracted.Visuals
            .Where(v => v.Content is not null)
            .GroupBy(v => StorageKey(documentId, v.Sha256, v.ContentType!))
            .Where(g => !storedKeys.Contains(g.Key))
            .Select(g => (Key: g.Key, Content: g.First().Content!))
            .ToList();

        var failed = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = UploadParallelism, CancellationToken = ct },
            async (file, token) =>
            {
                try
                {
                    using var content = new MemoryStream(file.Content, writable: false);
                    await storage.SaveAsync(file.Key, content, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not store visual {Key}", file.Key);
                    failed.Add(file.Key);
                }
            });

        var failedKeys = failed.ToHashSet();
        var visuals = extracted.Visuals.Select((v, i) =>
        {
            var mapping = mappings[i];
            var key = v.Content is null ? null : StorageKey(documentId, v.Sha256, v.ContentType!);
            var note = v.UnsupportedReason ?? mapping.Reason;
            if (key is not null && failedKeys.Contains(key))
            {
                key = null;
                note = "Fayl saxlanmadı";
            }

            return new QuestionVisual
            {
                DocumentId = documentId,
                Question = mapping.QuestionIndex is int q ? questions[q] : null,
                OptionPosition = mapping.OptionIndex is int o ? (byte)o : null,
                SortOrder = i,
                PageNumber = v.PageNumber,
                Left = v.Bounds.Left,
                Bottom = v.Bounds.Bottom,
                Width = v.Bounds.Width,
                Height = v.Bounds.Height,
                MappingStatus = mapping.Status switch
                {
                    VisualMappingStatus.AutoMapped => QuestionVisualStatus.AutoMapped,
                    VisualMappingStatus.NeedsReview => QuestionVisualStatus.NeedsReview,
                    _ => QuestionVisualStatus.Unmapped,
                },
                Note = note is { Length: > MaxNoteLength } ? note[..MaxNoteLength] : note,
                StorageKey = key,
                ContentType = key is null ? null : v.ContentType,
                ByteSize = key is null ? 0 : v.Content!.Length,
                PixelWidth = v.PixelWidth,
                PixelHeight = v.PixelHeight,
                Sha256 = v.Sha256,
                CreatedAt = now,
            };
        }).ToList();

        var warnings = extracted.Warnings.ToList();
        if (failedKeys.Count > 0)
        {
            warnings.Add($"{failedKeys.Count} şəkil faylı saxlanmadı");
        }

        var review = visuals.Count(v => v.MappingStatus == QuestionVisualStatus.NeedsReview);
        if (review > 0)
        {
            warnings.Add($"{review} şəklin hansı suala aid olduğu yoxlanmalıdır");
        }

        return new VisualImport(visuals, warnings);
    }
}

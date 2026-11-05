namespace ExamFlow.Api.Features.Documents;

public sealed class DocumentOptions
{
    public const string Section = "Documents";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxPages { get; set; } = 300;
    public int ParseTimeoutSeconds { get; set; } = 60;
    public int MaxConcurrentJobs { get; set; } = 2;

    /// <summary>Extract and store images of PDF questions; switch off on hosts short of memory/CPU.</summary>
    public bool ExtractVisuals { get; set; } = true;

    /// <summary>
    /// Own budget for image extraction, separate from <see cref="ParseTimeoutSeconds"/>: on a slow host (e.g. a
    /// fractional vCPU) parsing a large PDF can use up the parse budget and leave no time for its images.
    /// </summary>
    public int VisualTimeoutSeconds { get; set; } = 300;
}

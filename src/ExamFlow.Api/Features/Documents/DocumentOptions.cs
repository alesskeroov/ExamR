namespace ExamFlow.Api.Features.Documents;

public sealed class DocumentOptions
{
    public const string Section = "Documents";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxPages { get; set; } = 300;
    public int ParseTimeoutSeconds { get; set; } = 60;
    public int MaxConcurrentJobs { get; set; } = 2;
}

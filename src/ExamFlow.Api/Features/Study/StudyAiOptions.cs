using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Study;

public sealed class StudyAiOptions
{
    public const string Section = "StudyAi";
    [Required, StringLength(100)] public string Model { get; set; } = "gpt-4.1-mini";
    [Range(1, 1000)] public int DailyRequestLimit { get; set; } = 50;
    [Range(256, 4000)] public int MaxOutputTokens { get; set; } = 1500;
    [Range(1000, 16000)] public int MaxContextCharacters { get; set; } = 10000;
    [Range(1, 8)] public int MaxChunks { get; set; } = 5;
    [Range(10, 90)] public int TimeoutSeconds { get; set; } = 45;
    [Range(typeof(decimal), "0", "1000")] public decimal InputUsdPerMillion { get; set; } = 0.40m;
    [Range(typeof(decimal), "0", "1000")] public decimal CachedInputUsdPerMillion { get; set; } = 0.10m;
    [Range(typeof(decimal), "0", "1000")] public decimal OutputUsdPerMillion { get; set; } = 1.60m;
}

public sealed class StudyAiException(string detail, int statusCode = 503) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
}

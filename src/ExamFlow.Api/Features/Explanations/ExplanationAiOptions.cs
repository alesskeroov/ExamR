using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Explanations;

/// <summary>AI explanation generation. Off and on the mock provider by default, so nothing can spend money until both are changed.</summary>
public sealed class ExplanationAiOptions
{
    public const string Section = "ExplanationAi";
    public const string MockProvider = "Mock";
    public const string OpenAiProvider = "OpenAI";

    public bool Enabled { get; set; }

    /// <summary>"Mock" (no network, simulated tokens) or "OpenAI" (real, paid calls).</summary>
    [Required, RegularExpression("^(Mock|OpenAI)$")] public string Provider { get; set; } = MockProvider;

    [Required, StringLength(64)] public string TheoryModel { get; set; } = "gpt-4o-mini";
    [Required, StringLength(64)] public string MathModel { get; set; } = "gpt-4.1-mini";

    // USD per 1M tokens (standard tier). Cached input is charged at the full rate here: estimates stay on the safe side.
    [Range(typeof(decimal), "0", "1000")] public decimal TheoryInputUsdPerMillion { get; set; } = 0.15m;
    [Range(typeof(decimal), "0", "1000")] public decimal TheoryOutputUsdPerMillion { get; set; } = 0.60m;
    [Range(typeof(decimal), "0", "1000")] public decimal MathInputUsdPerMillion { get; set; } = 0.40m;
    [Range(typeof(decimal), "0", "1000")] public decimal MathOutputUsdPerMillion { get; set; } = 1.60m;

    /// <summary>In-app cap per calendar month (UTC) across all runs, failed calls included.</summary>
    [Range(typeof(decimal), "0", "1000")] public decimal MonthlyBudgetUsd { get; set; } = 0.50m;

    /// <summary>Questions one run may generate; small while testing.</summary>
    [Range(1, 2000)] public int MaxQuestionsPerJob { get; set; } = 10;

    [Range(1, 20)] public int TheoryBatchSize { get; set; } = 8;
    [Range(1, 10)] public int MathBatchSize { get; set; } = 3;
    [Range(64, 1000)] public int TheoryOutputTokensPerQuestion { get; set; } = 250;
    [Range(128, 2000)] public int MathOutputTokensPerQuestion { get; set; } = 600;
    [Range(200, 8000)] public int MaxQuestionCharacters { get; set; } = 2000;

    /// <summary>
    /// Estimated input tokens per question image (sent at high detail). Image questions use the math model, whose
    /// image tokens stay below this; 4o-class mini models count images far higher.
    /// </summary>
    [Range(0, 50000)] public int ImageInputTokens { get; set; } = 2500;

    /// <summary>Larger image files are not sent; the question is still explained from its text.</summary>
    [Range(1, 20)] public int MaxImageMegabytes { get; set; } = 4;

    /// <summary>Transient failures in a row (rate limit, timeout, outage) before a run stops; no endless retry.</summary>
    [Range(1, 10)] public int MaxConsecutiveFailures { get; set; } = 3;

    public string ModelFor(ExplanationQuestionType type) => type == ExplanationQuestionType.Math ? MathModel : TheoryModel;
    public int BatchSizeFor(ExplanationQuestionType type) => type == ExplanationQuestionType.Math ? MathBatchSize : TheoryBatchSize;
    public int OutputTokensFor(ExplanationQuestionType type) => type == ExplanationQuestionType.Math ? MathOutputTokensPerQuestion : TheoryOutputTokensPerQuestion;

    public decimal CostOf(ExplanationQuestionType type, int inputTokens, int outputTokens) => type == ExplanationQuestionType.Math
        ? (inputTokens * MathInputUsdPerMillion + outputTokens * MathOutputUsdPerMillion) / 1_000_000m
        : (inputTokens * TheoryInputUsdPerMillion + outputTokens * TheoryOutputUsdPerMillion) / 1_000_000m;
}

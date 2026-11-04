using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Explanations;

/// <summary>Explanation of one exam question; both fields are null when no approved explanation exists yet.</summary>
public sealed record QuestionExplanationDto(ExplanationQuestionType? QuestionType, string? Explanation);

/// <summary>Approved explanation for the exam question at <paramref name="Position"/> (0-based, as in the result).</summary>
public sealed record ExamExplanationDto(int Position, ExplanationQuestionType QuestionType, string Explanation);

/// <summary>Read side of the bank: plain database reads, never a model call.</summary>
public static class ExplanationLookup
{
    /// <summary>Approved explanations keyed by hex fingerprint; unknown or unapproved fingerprints are simply absent.</summary>
    public static async Task<Dictionary<string, QuestionExplanationDto>> ApprovedAsync(AppDbContext db, IEnumerable<byte[]> fingerprints, CancellationToken ct)
    {
        var keys = fingerprints.DistinctBy(Convert.ToHexString).ToList();
        if (keys.Count == 0) return [];
        var rows = await db.QuestionExplanations.AsNoTracking()
            .Where(e => e.Status == ExplanationStatus.Approved && keys.Contains(e.Fingerprint))
            .Select(e => new { e.Fingerprint, e.QuestionType, e.Explanation })
            .ToListAsync(ct);
        return rows.ToDictionary(r => Convert.ToHexString(r.Fingerprint), r => new QuestionExplanationDto(r.QuestionType, r.Explanation));
    }
}

using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Battles;

public sealed class BattleRating
{
    [Key]
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int CurrentRating { get; set; }
    public int HighestRating { get; set; }
    public int TotalRankedBattles { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public DateTimeOffset LastRatingUpdate { get; set; }
    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public sealed class BattleRankOptions
{
    [Range(1, 10000)] public int InitialRating { get; set; } = 1000;
    [Range(1, 100)] public int KFactor { get; set; } = 32;
    [Range(1, 10000)] public int RatingScale { get; set; } = 400;
    [Range(0, 0.5)] public double PerformanceWeight { get; set; } = 0.2;
    [Range(1, 50)] public int FullWeightQuestions { get; set; } = 10;
    public int[] Thresholds { get; set; } = [0, 1200, 1400, 1600, 1800, 2000];
}

public sealed record RankedPerformance(Guid UserId, int Rating, int Score, int CorrectCount, long CorrectTimeMs);
public sealed record RatingChange(Guid UserId, int Before, int After, int Outcome);
public sealed record BattleRankDto(string Name, int Rating, int HighestRating, int TotalRankedBattles,
    int Wins, int Losses, int Draws, string? NextRank, int Remaining, double Progress, DateTimeOffset? LastRatingUpdate);

/// <summary>Pairwise multiplayer Elo with normalized score contribution. No I/O or historical replay.</summary>
public static class BattleRankEngine
{
    public static readonly string[] Names = ["Bronze", "Silver", "Gold", "Platinum", "Diamond", "Master"];

    public static IReadOnlyList<RatingChange> Calculate(IReadOnlyList<RankedPerformance> players, int questionCount, BattleRankOptions options)
    {
        if (players.Count < 2 || questionCount < 1) return [];
        var reliability = Math.Min(1, Math.Sqrt((double)questionCount / options.FullWeightQuestions));
        double Normalized(RankedPerformance p) => Math.Clamp((double)p.Score / (questionCount * BattleScoring.MaxPoints), 0, 1);
        int Compare(RankedPerformance a, RankedPerformance b) => a.Score != b.Score ? a.Score.CompareTo(b.Score)
            : a.CorrectCount != b.CorrectCount ? a.CorrectCount.CompareTo(b.CorrectCount) : b.CorrectTimeMs.CompareTo(a.CorrectTimeMs);
        return players.Select(player =>
        {
            var sum = 0d;
            var best = true;
            var tied = false;
            foreach (var opponent in players.Where(p => p.UserId != player.UserId))
            {
                var comparison = Compare(player, opponent);
                best &= comparison >= 0;
                tied |= comparison == 0;
                var actual = comparison > 0 ? 1d : comparison < 0 ? 0d : 0.5;
                var expected = 1 / (1 + Math.Pow(10, (double)(opponent.Rating - player.Rating) / options.RatingScale));
                sum += (1 - options.PerformanceWeight) * (actual - expected)
                    + options.PerformanceWeight * (Normalized(player) - Normalized(opponent));
            }
            var delta = (int)Math.Round(options.KFactor * reliability * sum / (players.Count - 1), MidpointRounding.AwayFromZero);
            return new RatingChange(player.UserId, player.Rating, Math.Max(0, player.Rating + delta), best ? tied ? 0 : 1 : -1);
        }).ToList();
    }

    public static BattleRankDto Describe(BattleRating? rating, BattleRankOptions options)
    {
        var value = rating?.CurrentRating ?? options.InitialRating;
        var index = Array.FindLastIndex(options.Thresholds, threshold => value >= threshold);
        index = Math.Max(0, index);
        var next = index + 1 < Names.Length ? options.Thresholds[index + 1] : (int?)null;
        var floor = index == 0 ? options.InitialRating : options.Thresholds[index];
        var progress = next is null ? 100 : Math.Clamp(100d * (value - floor) / Math.Max(1, next.Value - floor), 0, 100);
        return new(Names[index], value, rating?.HighestRating ?? options.InitialRating, rating?.TotalRankedBattles ?? 0,
            rating?.Wins ?? 0, rating?.Losses ?? 0, rating?.Draws ?? 0, next is null ? null : Names[index + 1],
            next is null ? 0 : next.Value - value, progress, rating?.LastRatingUpdate);
    }

    // Called inside the same transaction as final results; the caller holds the database settlement lock.
    internal static async Task ApplyAsync(Infrastructure.Data.AppDbContext db, IReadOnlyList<BattleResult> results,
        BattleRankOptions options, CancellationToken ct)
    {
        if (results.Count < 2) return;
        var ids = results.Select(r => r.UserId).ToList();
        var profiles = await db.BattleRatings.Where(r => ids.Contains(r.UserId)).ToDictionaryAsync(r => r.UserId, ct);
        foreach (var id in ids.Where(id => !profiles.ContainsKey(id)))
        {
            var profile = new BattleRating { UserId = id, CurrentRating = options.InitialRating, HighestRating = options.InitialRating };
            profiles.Add(id, profile); db.BattleRatings.Add(profile);
        }
        var performances = results.Select(r => new RankedPerformance(r.UserId, profiles[r.UserId].CurrentRating, r.Score, r.CorrectCount, r.CorrectTimeMs)).ToList();
        foreach (var change in Calculate(performances, results[0].QuestionCount, options))
        {
            var result = results.Single(r => r.UserId == change.UserId);
            result.RatingBefore = change.Before; result.RatingAfter = change.After; result.RatingOutcome = change.Outcome;
            var profile = profiles[change.UserId];
            profile.CurrentRating = change.After; profile.HighestRating = Math.Max(profile.HighestRating, change.After);
            profile.TotalRankedBattles++; profile.Wins += change.Outcome == 1 ? 1 : 0;
            profile.Losses += change.Outcome == -1 ? 1 : 0; profile.Draws += change.Outcome == 0 ? 1 : 0;
            profile.LastRatingUpdate = result.PlayedAt;
        }
    }
}

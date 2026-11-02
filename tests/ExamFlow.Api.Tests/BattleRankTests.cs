using ExamFlow.Api.Features.Battles;

namespace ExamFlow.Api.Tests;

public class BattleRankTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly BattleRankOptions Options = new();
    private static RankedPerformance Player(Guid id, int rating = 1000, int score = 8000, int correct = 10, long time = 1000) => new(id, rating, score, correct, time);

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(0.88, 978)]
    [InlineData(8.36, 791)]
    [InlineData(20, 500)]
    public void ExistingScoreRemainsUnchanged(double seconds, int expected) =>
        Assert.Equal(expected, BattleScoring.Points(true, TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(20)));

    [Fact]
    public void WrongAnswerNeverSubtractsScore() => Assert.Equal(0, BattleScoring.Points(false, TimeSpan.Zero, TimeSpan.FromSeconds(20)));

    [Fact]
    public void EqualOpponentsGainAndLoseForWin()
    {
        var changes = BattleRankEngine.Calculate([Player(A), Player(B, score: 5000)], 10, Options);
        Assert.True(changes[0].After > changes[0].Before);
        Assert.True(changes[1].After < changes[1].Before);
        Assert.Equal(1, changes[0].Outcome); Assert.Equal(-1, changes[1].Outcome);
    }

    [Fact]
    public void BeatingStrongerOpponentGainsMore()
    {
        var easy = BattleRankEngine.Calculate([Player(A), Player(B, 600, 5000)], 10, Options)[0];
        var hard = BattleRankEngine.Calculate([Player(A), Player(B, 1400, 5000)], 10, Options)[0];
        Assert.True(hard.After > easy.After);
    }

    [Fact]
    public void ExactTiesDoNotUseNamesOrArrayOrder()
    {
        var changes = BattleRankEngine.Calculate([Player(A), Player(B), Player(C)], 10, Options);
        Assert.All(changes, c => { Assert.Equal(c.Before, c.After); Assert.Equal(0, c.Outcome); });
    }

    [Fact]
    public void FasterCorrectAnswersBreakPerformanceTie()
    {
        var changes = BattleRankEngine.Calculate([Player(A, time: 100), Player(B, time: 200)], 10, Options);
        Assert.Equal(1, changes[0].Outcome); Assert.Equal(-1, changes[1].Outcome);
    }

    [Fact]
    public void DifferentQuestionCountsAreNormalized()
    {
        var ten = BattleRankEngine.Calculate([Player(A, score: 8000), Player(B, score: 5000)], 10, Options);
        var fifty = BattleRankEngine.Calculate([Player(A, score: 40000), Player(B, score: 25000)], 50, Options);
        Assert.Equal(ten.Select(c => c.After), fifty.Select(c => c.After));
    }

    [Fact]
    public void ShortBattleHasLessWeight()
    {
        var shortGame = BattleRankEngine.Calculate([Player(A, score: 1000), Player(B, score: 0)], 1, Options)[0];
        var normalGame = BattleRankEngine.Calculate([Player(A, score: 10000), Player(B, score: 0)], 10, Options)[0];
        Assert.True(shortGame.After < normalGame.After);
    }

    [Fact]
    public void MultiplayerChangesAreBoundedAndOrderIndependent()
    {
        RankedPerformance[] players = [Player(A, 1000, 9000), Player(B, 1200, 6000), Player(C, 800, 3000)];
        var forward = BattleRankEngine.Calculate(players, 10, Options).OrderBy(c => c.UserId).ToList();
        var reverse = BattleRankEngine.Calculate(players.Reverse().ToList(), 10, Options).OrderBy(c => c.UserId).ToList();
        Assert.Equal(forward, reverse);
        Assert.All(forward, c => Assert.InRange(Math.Abs(c.After - c.Before), 0, Options.KFactor));
    }

    [Fact]
    public void RatingCannotBecomeNegative()
    {
        var changes = BattleRankEngine.Calculate([Player(A, 0, 0), Player(B, 0, 10000)], 10, Options);
        Assert.Equal(0, changes[0].After);
    }

    [Fact]
    public void SoloBattleIsUnranked() => Assert.Empty(BattleRankEngine.Calculate([Player(A)], 10, Options));

    [Theory]
    [InlineData(1000, "Bronze", 200)]
    [InlineData(1200, "Silver", 200)]
    [InlineData(1400, "Gold", 200)]
    [InlineData(1600, "Platinum", 200)]
    [InlineData(1800, "Diamond", 200)]
    [InlineData(2000, "Master", 0)]
    public void RankBoundariesAndProgress(int value, string name, int remaining)
    {
        var rank = BattleRankEngine.Describe(new BattleRating { CurrentRating = value }, Options);
        Assert.Equal(name, rank.Name); Assert.Equal(remaining, rank.Remaining);
        Assert.InRange(rank.Progress, 0, 100);
    }

    [Fact]
    public void NewUserHasNoInventedRankedHistory()
    {
        var rank = BattleRankEngine.Describe(null, Options);
        Assert.Equal(1000, rank.Rating); Assert.Equal(0, rank.TotalRankedBattles); Assert.Equal(0, rank.Progress);
    }
}

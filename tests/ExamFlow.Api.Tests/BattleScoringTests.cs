using ExamFlow.Api.Features.Battles;

namespace ExamFlow.Api.Tests;

public class BattleScoringTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(10, 750)]
    [InlineData(20, 500)]
    [InlineData(25, 500)] // late answers are clamped to the deadline
    public void Correct_answers_earn_more_when_faster(double seconds, int expected)
    {
        Assert.Equal(expected, BattleScoring.Points(true, TimeSpan.FromSeconds(seconds), Limit));
    }

    [Fact]
    public void Wrong_or_missing_answers_earn_nothing()
    {
        Assert.Equal(0, BattleScoring.Points(false, TimeSpan.FromSeconds(1), Limit));
    }

    [Fact]
    public void Ranking_uses_score_then_correct_count_then_speed()
    {
        var slow = new BattleParticipant(Guid.NewGuid(), "Slow", DateTimeOffset.UnixEpoch) { Score = 1500, CorrectCount = 2, CorrectTimeMs = 9000 };
        var fast = new BattleParticipant(Guid.NewGuid(), "Fast", DateTimeOffset.UnixEpoch) { Score = 1500, CorrectCount = 2, CorrectTimeMs = 3000 };
        var leader = new BattleParticipant(Guid.NewGuid(), "Leader", DateTimeOffset.UnixEpoch) { Score = 1800, CorrectCount = 2 };
        var last = new BattleParticipant(Guid.NewGuid(), "Last", DateTimeOffset.UnixEpoch) { Score = 0 };

        var ranked = BattleScoring.Rank([last, slow, fast, leader]);

        Assert.Equal(["Leader", "Fast", "Slow", "Last"], ranked.Select(p => p.DisplayName));
    }

    [Fact]
    public void Chat_messages_lose_control_characters_and_whitespace()
    {
        Assert.Equal("salam <b>dünya</b>", BattleScoring.CleanMessage("  salam\u0000 <b>dünya</b>\u0007  "));
    }
}

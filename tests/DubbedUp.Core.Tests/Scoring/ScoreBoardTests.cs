using DubbedUp.Core.Game;
using DubbedUp.Core.Scoring;
using DubbedUp.Core.Voting;
using Xunit;

namespace DubbedUp.Core.Tests.Scoring;

public sealed class ScoreBoardTests
{
    [Fact]
    public void Score_board_requires_unique_players_and_starts_at_zero()
    {
        Assert.Throws<GameRuleException>(() => ScoreBoard.Create(["p1"]));
        Assert.Throws<GameRuleException>(() => ScoreBoard.Create(["p1", "p1"]));

        var scores = ScoreBoard.Create(["p2", "p1"]);

        Assert.Equal(
            [new PlayerScore("p1", 0), new PlayerScore("p2", 0)],
            scores.Standings);
    }

    [Fact]
    public void Winner_points_accumulate_across_distinct_voting_rounds()
    {
        var scores = ScoreBoard.Create(["p1", "p2", "p3"]);

        var firstUpdate = scores.Apply(Winner("vote-1", "p1", "p2", "p3"));
        var secondUpdate = scores.Apply(Winner("vote-2", "p1", "p2", "p3"), winnerPoints: 2);

        Assert.Equal([new ScoreAward("p1", 1)], firstUpdate.Awards);
        Assert.Equal([new ScoreAward("p1", 2)], secondUpdate.Awards);
        Assert.Equal(
            [new PlayerScore("p1", 3), new PlayerScore("p2", 0), new PlayerScore("p3", 0)],
            scores.Standings);
    }

    [Fact]
    public void Tied_winning_players_each_receive_one_award_in_stable_order()
    {
        var scores = ScoreBoard.Create(["p2", "p1"]);
        var voting = VotingRound.Create(
            "vote-1",
            ["p1", "p2"],
            [Candidate("performance-1", "p1"), Candidate("performance-2", "p2")]);
        voting.CastVote("p1", "performance-2");
        voting.CastVote("p2", "performance-1");

        var update = scores.Apply(voting.Complete());

        Assert.Equal([new ScoreAward("p1", 1), new ScoreAward("p2", 1)], update.Awards);
        Assert.Equal([new PlayerScore("p1", 1), new PlayerScore("p2", 1)], update.Standings);
    }

    [Fact]
    public void Voting_round_cannot_be_scored_twice()
    {
        var scores = ScoreBoard.Create(["p1", "p2", "p3"]);
        var result = Winner("vote-1", "p1", "p2", "p3");
        scores.Apply(result);

        Assert.Throws<GameRuleException>(() => scores.Apply(result));
    }

    [Fact]
    public void Winner_points_must_be_positive()
    {
        var scores = ScoreBoard.Create(["p1", "p2", "p3"]);

        Assert.Throws<GameRuleException>(() => scores.Apply(Winner("vote-1", "p1", "p2", "p3"), 0));
    }

    private static VotingResult Winner(
        string votingRoundId,
        string winningPlayerId,
        string secondPlayerId,
        string thirdPlayerId)
    {
        var voting = VotingRound.Create(
            votingRoundId,
            [winningPlayerId, secondPlayerId, thirdPlayerId],
            [
                Candidate("winning-performance", winningPlayerId),
                Candidate("second-performance", secondPlayerId),
                Candidate("third-performance", thirdPlayerId),
            ]);
        voting.CastVote(winningPlayerId, "second-performance");
        voting.CastVote(secondPlayerId, "winning-performance");
        voting.CastVote(thirdPlayerId, "winning-performance");
        return voting.Complete();
    }

    private static PerformanceCandidate Candidate(string performanceId, string playerId) =>
        new(performanceId, playerId);
}

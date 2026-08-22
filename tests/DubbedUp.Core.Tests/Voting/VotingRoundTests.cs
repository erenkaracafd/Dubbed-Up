using DubbedUp.Core.Game;
using DubbedUp.Core.Voting;
using Xunit;

namespace DubbedUp.Core.Tests.Voting;

public sealed class VotingRoundTests
{
    [Fact]
    public void Create_requires_unique_eligible_players_and_candidates()
    {
        Assert.Throws<GameRuleException>(() => VotingRound.Create(
            "vote-1",
            ["p1", "p1"],
            Candidates()));
        Assert.Throws<GameRuleException>(() => VotingRound.Create(
            "vote-1",
            Players(),
            [Candidate("performance-1", "p1"), Candidate("performance-1", "p2")]));
    }

    [Fact]
    public void Candidate_owner_must_be_eligible_and_each_voter_needs_an_alternative()
    {
        Assert.Throws<GameRuleException>(() => VotingRound.Create(
            "vote-1",
            Players(),
            [Candidate("performance-1", "p1"), Candidate("performance-2", "p3")]));
        Assert.Throws<GameRuleException>(() => VotingRound.Create(
            "vote-1",
            Players(),
            [Candidate("performance-1", "p1"), Candidate("performance-2", "p1")]));
    }

    [Fact]
    public void Eligible_player_can_cast_exactly_one_non_self_vote()
    {
        var voting = VotingRound.Create("vote-1", Players(), Candidates());
        voting.CastVote("p1", "performance-2");

        Assert.Equal(new Vote("p1", "performance-2"), Assert.Single(voting.Votes));
        Assert.Throws<GameRuleException>(() => voting.CastVote("p1", "performance-2"));
        Assert.Throws<GameRuleException>(() => voting.CastVote("p2", "performance-2"));
        Assert.Throws<GameRuleException>(() => voting.CastVote("unknown", "performance-1"));
        Assert.Throws<GameRuleException>(() => voting.CastVote("p2", "unknown-performance"));
    }

    [Fact]
    public void Complete_requires_every_eligible_player_to_vote()
    {
        var voting = VotingRound.Create("vote-1", Players(), Candidates());
        voting.CastVote("p1", "performance-2");

        var exception = Assert.Throws<GameRuleException>(() => voting.Complete());

        Assert.Contains("p2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tied_results_are_complete_and_deterministically_ordered()
    {
        var voting = VotingRound.Create("vote-1", Players(), Candidates());
        voting.CastVote("p1", "performance-2");
        voting.CastVote("p2", "performance-1");

        var result = voting.Complete();

        Assert.True(result.IsTie);
        Assert.Equal(2, result.TotalVotes);
        Assert.Equal(["performance-1", "performance-2"], result.WinningPerformanceIds);
        Assert.Equal(["p1", "p2"], result.WinningPlayerIds);
        Assert.Equal(["performance-1", "performance-2"], result.Tallies.Select(tally => tally.PerformanceId));
        Assert.All(result.Tallies, tally => Assert.Equal(1, tally.VoteCount));
        Assert.Same(result, voting.Complete());
        Assert.Throws<GameRuleException>(() => voting.CastVote("p1", "performance-2"));
    }

    [Fact]
    public void Winner_and_zero_vote_candidates_are_reported_for_the_ui()
    {
        var voting = VotingRound.Create(
            "vote-1",
            ["p1", "p2", "p3"],
            [
                Candidate("performance-1", "p1"),
                Candidate("performance-2", "p2"),
                Candidate("performance-3", "p3"),
            ]);
        voting.CastVote("p1", "performance-2");
        voting.CastVote("p2", "performance-1");
        voting.CastVote("p3", "performance-1");

        var result = voting.Complete();

        Assert.False(result.IsTie);
        Assert.Equal(["performance-1"], result.WinningPerformanceIds);
        Assert.Equal(["p1"], result.WinningPlayerIds);
        Assert.Collection(
            result.Tallies,
            tally => Assert.Equal(new PerformanceVoteResult("performance-1", "p1", 2), tally),
            tally => Assert.Equal(new PerformanceVoteResult("performance-2", "p2", 1), tally),
            tally => Assert.Equal(new PerformanceVoteResult("performance-3", "p3", 0), tally));
    }

    private static string[] Players() => ["p1", "p2"];

    private static PerformanceCandidate[] Candidates() =>
    [
        Candidate("performance-1", "p1"),
        Candidate("performance-2", "p2"),
    ];

    private static PerformanceCandidate Candidate(string performanceId, string playerId) =>
        new(performanceId, playerId);
}

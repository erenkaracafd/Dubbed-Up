namespace DubbedUp.Core.Voting;

public sealed class VotingResult
{
    internal VotingResult(
        string votingRoundId,
        IReadOnlyList<PerformanceVoteResult> tallies,
        IReadOnlyList<string> winningPerformanceIds,
        IReadOnlyList<string> winningPlayerIds,
        int totalVotes)
    {
        VotingRoundId = votingRoundId;
        Tallies = tallies;
        WinningPerformanceIds = winningPerformanceIds;
        WinningPlayerIds = winningPlayerIds;
        TotalVotes = totalVotes;
    }

    public string VotingRoundId { get; }

    public IReadOnlyList<PerformanceVoteResult> Tallies { get; }

    public IReadOnlyList<string> WinningPerformanceIds { get; }

    public IReadOnlyList<string> WinningPlayerIds { get; }

    public int TotalVotes { get; }

    public bool IsTie => WinningPerformanceIds.Count > 1;
}

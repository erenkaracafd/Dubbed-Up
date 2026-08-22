using DubbedUp.Core.Game;

namespace DubbedUp.Core.Voting;

public sealed class VotingRound
{
    private readonly Dictionary<string, PerformanceCandidate> _candidates;
    private readonly HashSet<string> _eligiblePlayerIds;
    private readonly Dictionary<string, Vote> _votes = new(StringComparer.Ordinal);
    private VotingResult? _result;

    private VotingRound(
        string votingRoundId,
        HashSet<string> eligiblePlayerIds,
        Dictionary<string, PerformanceCandidate> candidates)
    {
        VotingRoundId = votingRoundId;
        _eligiblePlayerIds = eligiblePlayerIds;
        _candidates = candidates;
    }

    public string VotingRoundId { get; }

    public bool IsComplete => _result is not null;

    public IReadOnlyList<string> EligiblePlayerIds => _eligiblePlayerIds
        .OrderBy(playerId => playerId, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<PerformanceCandidate> Candidates => _candidates.Values
        .OrderBy(candidate => candidate.PerformanceId, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<Vote> Votes => _votes.Values
        .OrderBy(vote => vote.VoterPlayerId, StringComparer.Ordinal)
        .ToArray();

    public static VotingRound Create(
        string votingRoundId,
        IEnumerable<string> eligiblePlayerIds,
        IEnumerable<PerformanceCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(votingRoundId))
        {
            throw new GameRuleException("votingRoundId is required.");
        }

        ArgumentNullException.ThrowIfNull(eligiblePlayerIds);
        ArgumentNullException.ThrowIfNull(candidates);

        var players = eligiblePlayerIds.ToArray();
        if (players.Length < 2 || players.Any(string.IsNullOrWhiteSpace))
        {
            throw new GameRuleException("Voting requires at least two eligible players with valid IDs.");
        }

        var eligiblePlayers = players.ToHashSet(StringComparer.Ordinal);
        if (eligiblePlayers.Count != players.Length)
        {
            throw new GameRuleException("Eligible player IDs must be unique.");
        }

        var candidateList = candidates.ToArray();
        if (candidateList.Length < 2)
        {
            throw new GameRuleException("Voting requires at least two performance candidates.");
        }

        if (candidateList.Any(candidate => candidate is null))
        {
            throw new GameRuleException("Performance candidates cannot contain null entries.");
        }

        if (candidateList.Select(candidate => candidate.PerformanceId).Distinct(StringComparer.Ordinal).Count() !=
            candidateList.Length)
        {
            throw new GameRuleException("Performance candidate IDs must be unique.");
        }

        var candidateMap = candidateList.ToDictionary(
            candidate => candidate.PerformanceId,
            candidate => candidate,
            StringComparer.Ordinal);

        var unknownOwner = candidateList.FirstOrDefault(candidate => !eligiblePlayers.Contains(candidate.PlayerId));
        if (unknownOwner is not null)
        {
            throw new GameRuleException(
                $"PerformanceId '{unknownOwner.PerformanceId}' belongs to ineligible playerId '{unknownOwner.PlayerId}'.");
        }

        foreach (var playerId in eligiblePlayers)
        {
            if (candidateList.All(candidate => string.Equals(candidate.PlayerId, playerId, StringComparison.Ordinal)))
            {
                throw new GameRuleException($"PlayerId '{playerId}' has no eligible performance to vote for.");
            }
        }

        return new VotingRound(votingRoundId, eligiblePlayers, candidateMap);
    }

    public void CastVote(string voterPlayerId, string performanceId)
    {
        if (IsComplete)
        {
            throw new GameRuleException("Voting is already complete.");
        }

        if (!_eligiblePlayerIds.Contains(voterPlayerId))
        {
            throw new GameRuleException($"PlayerId '{voterPlayerId}' is not eligible to vote.");
        }

        if (!_candidates.TryGetValue(performanceId, out var candidate))
        {
            throw new GameRuleException($"Unknown performanceId '{performanceId}'.");
        }

        if (string.Equals(candidate.PlayerId, voterPlayerId, StringComparison.Ordinal))
        {
            throw new GameRuleException("Players cannot vote for their own performance.");
        }

        if (!_votes.TryAdd(voterPlayerId, new Vote(voterPlayerId, performanceId)))
        {
            throw new GameRuleException($"PlayerId '{voterPlayerId}' has already voted.");
        }
    }

    public VotingResult Complete()
    {
        if (_result is not null)
        {
            return _result;
        }

        var missingVoters = _eligiblePlayerIds
            .Where(playerId => !_votes.ContainsKey(playerId))
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        if (missingVoters.Length > 0)
        {
            throw new GameRuleException($"Missing votes from: {string.Join(", ", missingVoters)}.");
        }

        var voteCounts = _votes.Values
            .GroupBy(vote => vote.PerformanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var tallies = _candidates.Values
            .Select(candidate => new PerformanceVoteResult(
                candidate.PerformanceId,
                candidate.PlayerId,
                voteCounts.GetValueOrDefault(candidate.PerformanceId)))
            .OrderByDescending(tally => tally.VoteCount)
            .ThenBy(tally => tally.PerformanceId, StringComparer.Ordinal)
            .ToArray();
        var winningVoteCount = tallies[0].VoteCount;
        var winningTallies = tallies
            .Where(tally => tally.VoteCount == winningVoteCount)
            .OrderBy(tally => tally.PerformanceId, StringComparer.Ordinal)
            .ToArray();

        _result = new VotingResult(
            VotingRoundId,
            tallies,
            winningTallies.Select(tally => tally.PerformanceId).ToArray(),
            winningTallies
                .Select(tally => tally.PlayerId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray(),
            _votes.Count);
        return _result;
    }
}

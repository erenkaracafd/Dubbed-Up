using DubbedUp.Core.Game;

namespace DubbedUp.Core.Voting;

public sealed record PerformanceCandidate
{
    public PerformanceCandidate(string performanceId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(performanceId))
        {
            throw new GameRuleException("performanceId is required.");
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new GameRuleException("playerId is required for a performance candidate.");
        }

        PerformanceId = performanceId;
        PlayerId = playerId;
    }

    public string PerformanceId { get; }

    public string PlayerId { get; }
}

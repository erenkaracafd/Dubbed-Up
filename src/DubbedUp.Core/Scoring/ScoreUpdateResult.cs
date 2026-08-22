namespace DubbedUp.Core.Scoring;

public sealed record ScoreUpdateResult(
    string VotingRoundId,
    IReadOnlyList<ScoreAward> Awards,
    IReadOnlyList<PlayerScore> Standings);

using DubbedUp.Core.Game;
using DubbedUp.Core.Voting;

namespace DubbedUp.Core.Scoring;

public sealed class ScoreBoard
{
    private readonly HashSet<string> _appliedVotingRoundIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _scores;

    private ScoreBoard(Dictionary<string, int> scores)
    {
        _scores = scores;
    }

    public IReadOnlyList<PlayerScore> Standings => CreateStandings();

    public static ScoreBoard Create(IEnumerable<string> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        var players = playerIds.ToArray();
        if (players.Length < 2 || players.Any(string.IsNullOrWhiteSpace))
        {
            throw new GameRuleException("A score board requires at least two players with valid IDs.");
        }

        if (players.Distinct(StringComparer.Ordinal).Count() != players.Length)
        {
            throw new GameRuleException("Score board player IDs must be unique.");
        }

        var scores = players.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal);

        return new ScoreBoard(scores);
    }

    public ScoreUpdateResult Apply(VotingResult result, int winnerPoints = 1)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (winnerPoints <= 0)
        {
            throw new GameRuleException("winnerPoints must be greater than zero.");
        }

        if (_appliedVotingRoundIds.Contains(result.VotingRoundId))
        {
            throw new GameRuleException($"VotingRoundId '{result.VotingRoundId}' has already been scored.");
        }

        var unknownWinner = result.WinningPlayerIds.FirstOrDefault(playerId => !_scores.ContainsKey(playerId));
        if (unknownWinner is not null)
        {
            throw new GameRuleException($"Winning playerId '{unknownWinner}' is not on the score board.");
        }

        var awards = result.WinningPlayerIds
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .Select(playerId => new ScoreAward(playerId, winnerPoints))
            .ToArray();
        foreach (var award in awards)
        {
            _scores[award.PlayerId] += award.Points;
        }

        _appliedVotingRoundIds.Add(result.VotingRoundId);
        return new ScoreUpdateResult(result.VotingRoundId, awards, CreateStandings());
    }

    private IReadOnlyList<PlayerScore> CreateStandings() => _scores
        .Select(pair => new PlayerScore(pair.Key, pair.Value))
        .OrderByDescending(score => score.Points)
        .ThenBy(score => score.PlayerId, StringComparer.Ordinal)
        .ToArray();
}

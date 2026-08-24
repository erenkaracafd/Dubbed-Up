using DubbedUp.Core.Game;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Rounds;
using DubbedUp.Core.Scenes;

namespace DubbedUp.Core.Sessions;

public sealed class LocalSession
{
    private readonly List<Round> _rounds = [];

    private LocalSession(string sessionId, IReadOnlyList<Player> players)
    {
        SessionId = sessionId;
        Players = players.ToList().AsReadOnly();
    }

    public string SessionId { get; }

    public IReadOnlyList<Player> Players { get; }

    public IReadOnlyList<Round> Rounds => _rounds.AsReadOnly();

    public Round? ActiveRound => _rounds.LastOrDefault(round => round.Phase != RoundPhase.Complete);

    public static LocalSession Create(string sessionId, IEnumerable<Player> players)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new GameRuleException("sessionId is required.");
        }

        ArgumentNullException.ThrowIfNull(players);
        var playerList = players.ToArray();
        if (playerList.Length < 1)
        {
            throw new GameRuleException("A local session requires at least one player.");
        }

        var duplicatePlayerId = playerList
            .GroupBy(player => player.PlayerId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePlayerId is not null)
        {
            throw new GameRuleException($"Duplicate playerId '{duplicatePlayerId}'.");
        }

        return new LocalSession(sessionId, playerList);
    }

    public Round StartRound(string roundId, OfficialSceneDocument scene)
    {
        if (string.IsNullOrWhiteSpace(roundId))
        {
            throw new GameRuleException("roundId is required.");
        }

        ArgumentNullException.ThrowIfNull(scene);
        ProjectValidator.Validate(scene);

        if (ActiveRound is not null)
        {
            throw new GameRuleException("The active round must be completed before starting another round.");
        }

        if (_rounds.Any(round => string.Equals(round.RoundId, roundId, StringComparison.Ordinal)))
        {
            throw new GameRuleException($"Duplicate roundId '{roundId}'.");
        }

        var round = new Round(
            roundId,
            scene.SceneId,
            Players.Select(player => player.PlayerId).ToArray(),
            scene.VoiceSlots);
        _rounds.Add(round);
        return round;
    }
}

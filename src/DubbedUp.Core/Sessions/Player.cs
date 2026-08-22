using DubbedUp.Core.Game;

namespace DubbedUp.Core.Sessions;

public sealed record Player
{
    public Player(string playerId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new GameRuleException("playerId is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new GameRuleException("Player display name is required.");
        }

        PlayerId = playerId;
        DisplayName = displayName.Trim();
    }

    public string PlayerId { get; }

    public string DisplayName { get; }
}

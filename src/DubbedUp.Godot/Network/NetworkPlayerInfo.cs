namespace DubbedUp.Godot.Network;

/// <summary>
/// Represents a connected multiplayer player in the lobby.
/// </summary>
public sealed record NetworkPlayerInfo
{
    public long PeerId { get; init; }

    public string PlayerName { get; init; } = "Player";

    public string? AssignedCharacterId { get; init; }

    public bool IsHost { get; init; }

    public bool IsReady { get; init; }
}

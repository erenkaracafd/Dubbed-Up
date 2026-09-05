using System.Linq;

namespace DubbedUp.Godot.Network;

/// <summary>
/// Represents a connected multiplayer player in the lobby.
/// </summary>
public sealed record NetworkPlayerInfo
{
    public long PeerId { get; init; }

    public string PlayerName { get; init; } = "Player";

    public string[] AssignedCharacterIds { get; init; } = [];

    public string? AssignedCharacterId => AssignedCharacterIds.FirstOrDefault();

    public bool IsHost { get; init; }

    public bool IsReady { get; init; }
}


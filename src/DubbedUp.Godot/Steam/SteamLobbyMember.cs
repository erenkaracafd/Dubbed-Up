namespace DubbedUp.Godot.Steam;

public sealed record SteamLobbyMember(
    ulong SteamId,
    string DisplayName,
    bool IsOwner);

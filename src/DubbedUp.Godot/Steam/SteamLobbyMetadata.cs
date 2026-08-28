namespace DubbedUp.Godot.Steam;

public static class SteamLobbyMetadata
{
    public const string ProtocolVersionKey = "protocol_version";
    public const string SceneIdKey = "scene_id";
    public const string WorkshopItemIdKey = "workshop_item_id";
    public const string LobbyPhaseKey = "lobby_phase";

    public const int MaxValueLength = 255;

    private static readonly HashSet<string> AllowedKeys =
    [
        ProtocolVersionKey,
        SceneIdKey,
        WorkshopItemIdKey,
        LobbyPhaseKey,
    ];

    public static bool IsAllowed(string key, string value)
    {
        return !string.IsNullOrWhiteSpace(key)
            && AllowedKeys.Contains(key)
            && value.Length <= MaxValueLength;
    }
}

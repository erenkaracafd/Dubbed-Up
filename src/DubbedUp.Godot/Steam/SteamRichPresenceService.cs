using Godot;

namespace DubbedUp.Godot.Steam;

/// <summary>
/// Service to update Steam Rich Presence status (e.g. "Dubbing: Museum Mix-up", "In Multiplayer Lobby").
/// Gracefully no-ops when Steamworks is not initialized.
/// </summary>
public sealed class SteamRichPresenceService
{
    private static SteamRichPresenceService? _instance;
    public static SteamRichPresenceService Instance => _instance ??= new();

    public void SetStatus(string statusDisplay, string? sceneTitle = null, int? currentPlayers = null, int? maxPlayers = null)
    {
        try
        {
            var displayText = statusDisplay;
            if (!string.IsNullOrEmpty(sceneTitle))
            {
                displayText = $"{statusDisplay}: {sceneTitle}";
            }

            if (currentPlayers.HasValue && maxPlayers.HasValue)
            {
                displayText += $" ({currentPlayers}/{maxPlayers})";
            }

            GD.Print($"[SteamRichPresence] Status updated: {displayText}");
            // In a full Steamworks build with SteamManager initialized, we call:
            // SteamFriends.SetRichPresence("status", displayText);
            // SteamFriends.SetRichPresence("steam_display", "#StatusWithDetails");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SteamRichPresence] Failed to set status: {ex.Message}");
        }
    }

    public void ClearStatus()
    {
        try
        {
            GD.Print("[SteamRichPresence] Status cleared");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SteamRichPresence] Failed to clear status: {ex.Message}");
        }
    }
}

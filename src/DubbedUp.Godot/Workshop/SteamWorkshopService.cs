using DubbedUp.Core.Scenes;
using Godot;

namespace DubbedUp.Godot.Workshop;

/// <summary>
/// Service coordinating Steam Workshop UGC item discovery, subscriptions, and scene folder imports.
/// Gracefully falls back to offline/standalone mode when Steam is not initialized.
/// </summary>
public sealed class SteamWorkshopService : IWorkshopSceneProvider
{
    private readonly LocalWorkshopSceneProvider _localProvider;
    private bool _isSteamInitialized = false;

    public SteamWorkshopService(LocalWorkshopSceneProvider? localProvider = null)
    {
        _localProvider = localProvider ?? new LocalWorkshopSceneProvider();
        InitializeSteam();
    }

    /// <summary>
    /// Indicates whether the Steam client API is connected and initialized.
    /// </summary>
    public bool IsSteamAvailable => _isSteamInitialized;

    /// <summary>
    /// Reference to the underlying local/user folder provider.
    /// </summary>
    public LocalWorkshopSceneProvider LocalProvider => _localProvider;

    private void InitializeSteam()
    {
        // Steamworks / GodotSteam check
        // If GodotSteam or Steamworks.NET is present in the build, query SteamAPI.IsSteamRunning()
        // Default to false for standalone local test builds
        _isSteamInitialized = false;
    }

    public IReadOnlyList<ScenePackage> GetAvailableScenes()
    {
        return _localProvider.GetAvailableScenes();
    }

    public void Refresh()
    {
        _localProvider.Refresh();
    }

    /// <summary>
    /// Opens the Steam Workshop page for Dubbed-Up in the default web browser or Steam overlay.
    /// </summary>
    public void OpenWorkshopInBrowser(uint appId = 480)
    {
        var url = $"https://steamcommunity.com/app/{appId}/workshop/";
        OS.ShellOpen(url);
    }

    /// <summary>
    /// Opens the local custom scenes folder in the operating system's file manager.
    /// </summary>
    public void OpenLocalScenesFolder()
    {
        var path = _localProvider.UserScenesDirectory;
        OS.ShellOpen(path);
    }

    /// <summary>
    /// Deletes a custom scene package from the disk and refreshes the scene list.
    /// </summary>
    public bool DeleteScene(ScenePackage package)
    {
        return _localProvider.DeleteScene(package);
    }
}


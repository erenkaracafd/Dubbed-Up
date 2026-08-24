using DubbedUp.Core.Scenes;

namespace DubbedUp.Godot.Workshop;

/// <summary>
/// Provider interface for discovering installed, local, or Steam Workshop scene packages.
/// </summary>
public interface IWorkshopSceneProvider
{
    /// <summary>
    /// Gets all available scene packages discovered by this provider.
    /// </summary>
    IReadOnlyList<ScenePackage> GetAvailableScenes();

    /// <summary>
    /// Refreshes and re-scans the scene directories.
    /// </summary>
    void Refresh();
}

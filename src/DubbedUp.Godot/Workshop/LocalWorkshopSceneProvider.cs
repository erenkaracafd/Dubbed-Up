using DubbedUp.Core.Scenes;
using Godot;

namespace DubbedUp.Godot.Workshop;

/// <summary>
/// Discovers scene packages from local game directories and the user's custom workshop scenes directory.
/// </summary>
public sealed class LocalWorkshopSceneProvider : IWorkshopSceneProvider
{
    private readonly List<string> _searchDirectories = [];
    private readonly List<ScenePackage> _cachedScenes = [];

    public LocalWorkshopSceneProvider(IEnumerable<string>? additionalDirectories = null)
    {
        // 1. Standard project scenes folder
        _searchDirectories.Add(ProjectSettings.GlobalizePath("res://scenes"));
        _searchDirectories.Add("scenes");

        // 2. Official scenes folder
        _searchDirectories.Add(ProjectSettings.GlobalizePath("res://Content/OfficialScenes"));
        _searchDirectories.Add("Content/OfficialScenes");

        // 3. User data workshop folder (user://workshop_scenes)
        var userScenesPath = ProjectSettings.GlobalizePath("user://workshop_scenes");
        _searchDirectories.Add(userScenesPath);

        // Ensure user scenes directory exists so players can drop folders into it
        try
        {
            if (!System.IO.Directory.Exists(userScenesPath))
            {
                System.IO.Directory.CreateDirectory(userScenesPath);
            }
        }
        catch
        {
            // Ignore directory creation failure in sandboxed environments
        }

        if (additionalDirectories is not null)
        {
            _searchDirectories.AddRange(additionalDirectories);
        }

        Refresh();
    }

    /// <summary>
    /// Gets the absolute path to the user's custom workshop scenes directory.
    /// </summary>
    public string UserScenesDirectory => ProjectSettings.GlobalizePath("user://workshop_scenes");

    public IReadOnlyList<ScenePackage> GetAvailableScenes() => _cachedScenes.AsReadOnly();

    public void Refresh()
    {
        _cachedScenes.Clear();
        var seenSceneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in _searchDirectories)
        {
            try
            {
                var discovered = ScenePackageLoader.DiscoverPackages(dir);
                foreach (var package in discovered)
                {
                    if (seenSceneIds.Add(package.SceneId))
                    {
                        _cachedScenes.Add(package);
                    }
                }
            }
            catch
            {
                // Continue scanning other directories if one fails
            }
        }
    }
}

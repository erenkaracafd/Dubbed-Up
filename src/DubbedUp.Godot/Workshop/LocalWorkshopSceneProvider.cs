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
        var resPath = ProjectSettings.GlobalizePath("res://");

        // 1. Official scenes inside Godot project
        _searchDirectories.Add(ProjectSettings.GlobalizePath("res://Content/OfficialScenes"));
        _searchDirectories.Add(System.IO.Path.Combine(resPath, "Content", "OfficialScenes"));
        _searchDirectories.Add(ProjectSettings.GlobalizePath("res://scenes"));
        _searchDirectories.Add(System.IO.Path.Combine(resPath, "scenes"));

        // 2. Repository root scenes folder (when running in dev mode from repo root or build folder)
        _searchDirectories.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(resPath, "..", "..", "scenes")));
        _searchDirectories.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(resPath, "..", "scenes")));
        _searchDirectories.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "scenes")));
        _searchDirectories.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "..", "scenes")));
        _searchDirectories.Add("scenes");
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
            if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir))
            {
                continue;
            }

            try
            {
                var discovered = ScenePackageLoader.DiscoverPackages(dir);
                foreach (var package in discovered)
                {
                    if (seenSceneIds.Add(package.SceneId))
                    {
                        _cachedScenes.Add(package);
                        GD.Print($"[SceneProvider] Discovered scene: '{package.Title}' ({package.SceneId}) from '{package.PackageDirectory}'");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SceneProvider] Failed scanning '{dir}': {ex.Message}");
            }
        }

        GD.Print($"[SceneProvider] Total available scenes: {_cachedScenes.Count}");
    }

    public bool DeleteScene(ScenePackage package)
    {
        if (string.IsNullOrWhiteSpace(package.PackageDirectory) || !System.IO.Directory.Exists(package.PackageDirectory))
        {
            return false;
        }

        try
        {
            System.IO.Directory.Delete(package.PackageDirectory, true);
            GD.Print($"[SceneProvider] Successfully deleted scene package: '{package.Title}' at '{package.PackageDirectory}'");
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SceneProvider] Failed to delete scene package '{package.Title}': {ex.Message}");
            return false;
        }
    }
}

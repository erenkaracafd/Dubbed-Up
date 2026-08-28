using DubbedUp.Core.Game;
using DubbedUp.Core.ProjectFormat;

namespace DubbedUp.Core.Scenes;

/// <summary>
/// Discovers and loads scene packages (containing scene.json metadata, video, and preview assets)
/// from local directories, content folders, or Steam Workshop folders.
/// </summary>
public static class ScenePackageLoader
{
    private static readonly string[] SceneFileNames = ["scene.json", "scene.dubbedup.json"];
    private static readonly string[] VideoExtensions = [".mp4", ".ogv", ".webm", ".mkv"];
    private static readonly string[] ThumbnailExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    /// <summary>
    /// Loads and validates a single scene package from the specified directory.
    /// </summary>
    /// <param name="directoryPath">The directory containing scene.json and related media assets.</param>
    /// <returns>A validated <see cref="ScenePackage"/>.</returns>
    public static ScenePackage LoadPackageFromDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new GameRuleException("Directory path cannot be null or empty.");
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Scene directory does not exist: {directoryPath}");
        }

        var sceneJsonPath = SceneFileNames
            .Select(name => Path.Combine(directoryPath, name))
            .FirstOrDefault(File.Exists);

        if (sceneJsonPath is null)
        {
            throw new FileNotFoundException($"No scene.json or scene.dubbedup.json found in directory: {directoryPath}");
        }

        var json = File.ReadAllText(sceneJsonPath);
        var document = ProjectJsonSerializer.DeserializeScene(json);

        // Resolve video path: check document source media first, then fallback to standard files
        string? videoPath = null;
        var videoMedia = document.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        if (videoMedia is not null && !string.IsNullOrWhiteSpace(videoMedia.RelativePath))
        {
            var candidate = Path.Combine(directoryPath, videoMedia.RelativePath);
            if (File.Exists(candidate))
            {
                videoPath = Path.GetFullPath(candidate);
            }
        }

        if (videoPath is null)
        {
            // Search standard names like video.mp4, scene.mp4, etc.
            foreach (var ext in VideoExtensions)
            {
                var candidate = Path.Combine(directoryPath, $"video{ext}");
                if (File.Exists(candidate))
                {
                    videoPath = Path.GetFullPath(candidate);
                    break;
                }

                candidate = Path.Combine(directoryPath, $"scene{ext}");
                if (File.Exists(candidate))
                {
                    videoPath = Path.GetFullPath(candidate);
                    break;
                }
            }
        }

        // Resolve thumbnail preview path
        string? thumbnailPath = null;
        foreach (var ext in ThumbnailExtensions)
        {
            var candidate = Path.Combine(directoryPath, $"preview{ext}");
            if (File.Exists(candidate))
            {
                thumbnailPath = Path.GetFullPath(candidate);
                break;
            }

            candidate = Path.Combine(directoryPath, $"thumbnail{ext}");
            if (File.Exists(candidate))
            {
                thumbnailPath = Path.GetFullPath(candidate);
                break;
            }
        }

        return new ScenePackage(document, Path.GetFullPath(directoryPath), videoPath, thumbnailPath);
    }

    /// <summary>
    /// Scans a root directory (and its immediate subdirectories) for valid scene packages.
    /// </summary>
    /// <param name="rootDirectory">The root folder containing scene package folders.</param>
    /// <returns>A list of successfully loaded <see cref="ScenePackage"/> instances.</returns>
    public static IReadOnlyList<ScenePackage> DiscoverPackages(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return [];
        }

        var packages = new List<ScenePackage>();

        // Check if rootDirectory itself is a package
        if (SceneFileNames.Any(name => File.Exists(Path.Combine(rootDirectory, name))))
        {
            try
            {
                packages.Add(LoadPackageFromDirectory(rootDirectory));
                return packages;
            }
            catch
            {
                // Fall through to scan subdirectories
            }
        }

        // Scan immediate subdirectories
        var subDirs = Directory.GetDirectories(rootDirectory);
        foreach (var dir in subDirs)
        {
            try
            {
                if (SceneFileNames.Any(name => File.Exists(Path.Combine(dir, name))))
                {
                    packages.Add(LoadPackageFromDirectory(dir));
                }
            }
            catch
            {
                // Skip corrupted or unreadable scene folders gracefully in discovery
            }
        }

        return packages
            .OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}


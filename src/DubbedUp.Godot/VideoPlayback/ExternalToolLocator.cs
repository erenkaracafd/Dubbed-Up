using System.Text.Json;
using Godot;
using Environment = System.Environment;

namespace DubbedUp.Godot.VideoPlayback;

/// <summary>
/// Resolves optional media executables without coupling the engine-independent Core project
/// to FFmpeg, Python, or workstation-specific paths.
/// </summary>
public static class ExternalToolLocator
{
    private const string ConfigFileName = "media-tools.json";

    public static string ResolveFfmpeg() =>
        ResolveEnvironmentPath("DUBBEDUP_FFMPEG_PATH")
        ?? ReadConfiguredPath("ffmpegPath")
        ?? FindProjectTool(System.IO.Path.Combine("ffmpeg", "bin", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
        ?? FindWinGetFfmpeg()
        ?? "ffmpeg";

    public static string ResolveWhisperPython() =>
        ResolveEnvironmentPath("DUBBEDUP_WHISPER_PYTHON")
        ?? ReadConfiguredPath("whisperPythonPath")
        ?? FindProjectTool(System.IO.Path.Combine("whisper", OperatingSystem.IsWindows() ? "Scripts" : "bin", OperatingSystem.IsWindows() ? "python.exe" : "python"))
        ?? "python";

    public static string ResolveWhisperModel() =>
        Environment.GetEnvironmentVariable("DUBBEDUP_WHISPER_MODEL")
        ?? ReadConfiguredValue("whisperModel")
        ?? "tiny";

    public static void AddFfmpegToPath(System.Diagnostics.ProcessStartInfo startInfo)
    {
        var ffmpeg = ResolveFfmpeg();
        if (!System.IO.Path.IsPathRooted(ffmpeg)) return;

        var directory = System.IO.Path.GetDirectoryName(ffmpeg);
        if (string.IsNullOrWhiteSpace(directory)) return;

        var currentPath = startInfo.Environment.TryGetValue("PATH", out var path) ? path : Environment.GetEnvironmentVariable("PATH");
        startInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(currentPath)
            ? directory
            : $"{directory}{System.IO.Path.PathSeparator}{currentPath}";
    }

    private static string? ResolveEnvironmentPath(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return !string.IsNullOrWhiteSpace(value) && System.IO.File.Exists(value) ? System.IO.Path.GetFullPath(value) : null;
    }

    private static string? ReadConfiguredPath(string propertyName)
    {
        var value = ReadConfiguredValue(propertyName);
        return !string.IsNullOrWhiteSpace(value) && System.IO.File.Exists(value) ? System.IO.Path.GetFullPath(value) : null;
    }

    private static string? ReadConfiguredValue(string propertyName)
    {
        foreach (var root in EnumerateSearchRoots())
        {
            var configPath = System.IO.Path.Combine(root, ".tools", ConfigFileName);
            if (!System.IO.File.Exists(configPath)) continue;

            try
            {
                using var document = JsonDocument.Parse(System.IO.File.ReadAllText(configPath));
                if (document.RootElement.TryGetProperty(propertyName, out var property))
                {
                    return property.GetString();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ExternalToolLocator] Invalid media tool config '{configPath}': {ex.Message}");
            }
        }

        return null;
    }

    private static string? FindProjectTool(string relativePath)
    {
        foreach (var root in EnumerateSearchRoots())
        {
            var candidate = System.IO.Path.Combine(root, ".tools", relativePath);
            if (System.IO.File.Exists(candidate)) return System.IO.Path.GetFullPath(candidate);
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        var starts = new List<string?>
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory
        };

        try
        {
            starts.Add(ProjectSettings.GlobalizePath("res://"));
        }
        catch
        {
            // Godot may not be initialized in build-time tooling.
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start)) continue;

            var directory = new System.IO.DirectoryInfo(System.IO.Path.GetFullPath(start));
            for (var depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
            {
                if (seen.Add(directory.FullName)) yield return directory.FullName;
            }
        }
    }

    private static string? FindWinGetFfmpeg()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var packageRoot = System.IO.Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (!System.IO.Directory.Exists(packageRoot)) return null;

            return System.IO.Directory.EnumerateFiles(packageRoot, "ffmpeg.exe", System.IO.SearchOption.AllDirectories)
                .Where(path => path.Contains("Gyan.FFmpeg", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

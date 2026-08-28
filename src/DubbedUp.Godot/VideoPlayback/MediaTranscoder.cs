using System.Diagnostics;
using Godot;

namespace DubbedUp.Godot.VideoPlayback;

public static class MediaTranscoder
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".flv", ".wmv", ".m4v"
    };

    public static bool RunProcess(string fileName, string arguments, int timeoutMs = 180000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            using var process = Process.Start(psi);
            if (process is null) return false;

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                GD.PrintErr($"[MediaTranscoder] Process '{fileName}' timed out after {timeoutMs}ms");
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MediaTranscoder] Process execution error for '{fileName}': {ex.Message}");
            return false;
        }
    }

    public static VideoStream? LoadVideoStream(string? packageDirectory, string? relativePath)
    {
        // 1. Direct res:// resource check
        if (!string.IsNullOrEmpty(relativePath) && relativePath.StartsWith("res://") && ResourceLoader.Exists(relativePath))
        {
            var res = GD.Load<VideoStream>(relativePath);
            if (res is not null) return res;
        }

        // 2. Look for existing .ogv or auto-transcode package directory
        string? ogvPath = null;
        if (!string.IsNullOrEmpty(packageDirectory) && System.IO.Directory.Exists(packageDirectory))
        {
            var candidates = new List<string>
            {
                System.IO.Path.Combine(packageDirectory, "media", "video.ogv"),
                System.IO.Path.Combine(packageDirectory, "video.ogv")
            };

            if (!string.IsNullOrEmpty(relativePath) && relativePath.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Insert(0, System.IO.Path.Combine(packageDirectory, relativePath));
            }

            foreach (var cand in candidates)
            {
                if (System.IO.File.Exists(cand) && new System.IO.FileInfo(cand).Length > 1000)
                {
                    ogvPath = cand;
                    break;
                }
            }

            // If .ogv is not yet generated, execute automatic transcoding
            if (ogvPath is null)
            {
                ogvPath = EnsureTranscoded(packageDirectory);
            }
        }

        // 3. Fallback to official scenes / globalized path check
        if (ogvPath is null && !string.IsNullOrEmpty(relativePath))
        {
            var glob = ProjectSettings.GlobalizePath(relativePath);
            if (System.IO.File.Exists(glob) && glob.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase))
            {
                ogvPath = glob;
            }
        }

        if (ogvPath is not null && System.IO.File.Exists(ogvPath))
        {
            var localized = ProjectSettings.LocalizePath(ogvPath);
            if (!string.IsNullOrEmpty(localized) && ResourceLoader.Exists(localized))
            {
                var loaded = GD.Load<VideoStream>(localized);
                if (loaded is not null) return loaded;
            }

            var theora = new VideoStreamTheora
            {
                File = !string.IsNullOrEmpty(localized) ? localized : ogvPath.Replace("\\", "/")
            };
            GD.Print($"[MediaTranscoder] Successfully created VideoStreamTheora for: {theora.File}");
            return theora;
        }

        GD.PrintErr($"[MediaTranscoder] Failed to load any valid .ogv VideoStream for package: '{packageDirectory}', relative: '{relativePath}'");
        return null;
    }

    public static string? EnsureAudioExtracted(string packageDir)
    {
        if (string.IsNullOrWhiteSpace(packageDir) || !System.IO.Directory.Exists(packageDir))
        {
            return null;
        }

        var mediaDir = System.IO.Path.Combine(packageDir, "media");
        if (!System.IO.Directory.Exists(mediaDir))
        {
            mediaDir = packageDir;
        }

        var audioWav = System.IO.Path.Combine(mediaDir, "audio.wav");
        var vocalsWav = System.IO.Path.Combine(mediaDir, "vocals.wav");
        var bgWav = System.IO.Path.Combine(mediaDir, "background.wav");

        if (System.IO.File.Exists(audioWav) && new System.IO.FileInfo(audioWav).Length > 1000)
        {
            if (!System.IO.File.Exists(vocalsWav)) System.IO.File.Copy(audioWav, vocalsWav, true);
            if (!System.IO.File.Exists(bgWav)) System.IO.File.Copy(audioWav, bgWav, true);
            return audioWav;
        }

        if (System.IO.File.Exists(vocalsWav) && new System.IO.FileInfo(vocalsWav).Length > 1000)
        {
            if (!System.IO.File.Exists(audioWav)) System.IO.File.Copy(vocalsWav, audioWav, true);
            if (!System.IO.File.Exists(bgWav)) System.IO.File.Copy(vocalsWav, bgWav, true);
            return vocalsWav;
        }

        // Search for any source video file to extract audio from
        string? sourceFile = null;
        var candidates = new List<string>
        {
            System.IO.Path.Combine(mediaDir, "video.ogv"),
            System.IO.Path.Combine(mediaDir, "source_input.mp4"),
            System.IO.Path.Combine(mediaDir, "source_input.webm"),
            System.IO.Path.Combine(mediaDir, "source_input.mkv"),
            System.IO.Path.Combine(mediaDir, "source_input.mov")
        };

        foreach (var c in candidates)
        {
            if (System.IO.File.Exists(c) && new System.IO.FileInfo(c).Length > 1000)
            {
                sourceFile = c;
                break;
            }
        }

        if (sourceFile is null)
        {
            try
            {
                var files = System.IO.Directory.GetFiles(packageDir, "*.*", System.IO.SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    var ext = System.IO.Path.GetExtension(f);
                    if (VideoExtensions.Contains(ext) && System.IO.File.Exists(f))
                    {
                        sourceFile = f;
                        break;
                    }
                }
            }
            catch { }
        }

        if (sourceFile is not null && System.IO.File.Exists(sourceFile))
        {
            var args = $"-y -loglevel error -i \"{sourceFile}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{audioWav}\"";
            RunProcess("ffmpeg", args, 30000);

            if (System.IO.File.Exists(audioWav) && new System.IO.FileInfo(audioWav).Length > 1000)
            {
                if (!System.IO.File.Exists(vocalsWav)) System.IO.File.Copy(audioWav, vocalsWav, true);
                if (!System.IO.File.Exists(bgWav)) System.IO.File.Copy(audioWav, bgWav, true);
                GD.Print($"[MediaTranscoder] Successfully extracted audio.wav from '{sourceFile}'");
                return audioWav;
            }
        }

        return null;
    }

    public static string? EnsureTranscoded(string packageDir)
    {
        if (string.IsNullOrWhiteSpace(packageDir) || !System.IO.Directory.Exists(packageDir))
        {
            return null;
        }

        var mediaDir = System.IO.Path.Combine(packageDir, "media");
        if (!System.IO.Directory.Exists(mediaDir))
        {
            mediaDir = packageDir;
        }

        // Always guarantee audio WAVs are extracted
        EnsureAudioExtracted(packageDir);

        var ogvTarget = System.IO.Path.Combine(mediaDir, "video.ogv");
        if (System.IO.File.Exists(ogvTarget) && new System.IO.FileInfo(ogvTarget).Length > 1000)
        {
            return ogvTarget;
        }

        // Search for any source video file
        string? sourceVideoFile = null;
        try
        {
            var files = System.IO.Directory.GetFiles(packageDir, "*.*", System.IO.SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var ext = System.IO.Path.GetExtension(file);
                if (VideoExtensions.Contains(ext) && !file.EndsWith("video.ogv", StringComparison.OrdinalIgnoreCase))
                {
                    sourceVideoFile = file;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MediaTranscoder] Error searching package directory: {ex.Message}");
        }

        if (sourceVideoFile is null || !System.IO.File.Exists(sourceVideoFile))
        {
            return null;
        }

        GD.Print($"[MediaTranscoder] Found source video: '{sourceVideoFile}'. Starting fast multithreaded OGV conversion...");

        // 1. Copy source file to a safe ASCII filename in media folder to avoid emoji/space CLI encoding issues
        var safeInputPath = System.IO.Path.Combine(mediaDir, "source_input" + System.IO.Path.GetExtension(sourceVideoFile));
        try
        {
            if (!string.Equals(sourceVideoFile, safeInputPath, StringComparison.OrdinalIgnoreCase))
            {
                System.IO.File.Copy(sourceVideoFile, safeInputPath, true);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MediaTranscoder] Failed to copy to safe input path: {ex.Message}");
            safeInputPath = sourceVideoFile;
        }

        // 2. Extract audio.wav first (ultra-fast, takes 50ms)
        EnsureAudioExtracted(packageDir);

        // 3. Fast Multithreaded Transcode to video.ogv with FFmpeg
        var args = $"-y -loglevel error -i \"{safeInputPath}\" -threads 0 -vf \"scale='min(1280,iw)':-2\" -c:v libtheora -q:v 6 -c:a libvorbis -q:a 4 -pix_fmt yuv420p \"{ogvTarget}\"";
        RunProcess("ffmpeg", args, 180000);

        return System.IO.File.Exists(ogvTarget) ? ogvTarget : null;
    }
}

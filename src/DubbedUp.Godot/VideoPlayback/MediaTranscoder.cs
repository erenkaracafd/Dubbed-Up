using System.Diagnostics;
using Godot;

namespace DubbedUp.Godot.VideoPlayback;

public static class MediaTranscoder
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".flv", ".wmv", ".m4v"
    };

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

        GD.Print($"[MediaTranscoder] Found source video: '{sourceVideoFile}'. Starting automatic OGV conversion...");

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

        // 2. Transcode to video.ogv with FFmpeg
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{safeInputPath}\" -c:v libtheora -q:v 7 -c:a libvorbis -q:a 5 -pix_fmt yuv420p \"{ogvTarget}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is not null)
            {
                process.WaitForExit(180000); // 3 minutes timeout
                GD.Print($"[MediaTranscoder] FFmpeg OGV conversion finished with exit code {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MediaTranscoder] FFmpeg OGV conversion error: {ex.Message}");
        }

        // 3. Extract audio.wav if missing
        var audioWav = System.IO.Path.Combine(mediaDir, "audio.wav");
        if (!System.IO.File.Exists(audioWav))
        {
            try
            {
                var psiWav = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{safeInputPath}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{audioWav}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var pWav = Process.Start(psiWav);
                pWav?.WaitForExit(60000);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MediaTranscoder] FFmpeg WAV extraction error: {ex.Message}");
            }
        }

        // 4. Ensure vocals.wav and background.wav exist
        var vocalsWav = System.IO.Path.Combine(mediaDir, "vocals.wav");
        var bgWav = System.IO.Path.Combine(mediaDir, "background.wav");

        if (System.IO.File.Exists(audioWav))
        {
            if (!System.IO.File.Exists(vocalsWav)) System.IO.File.Copy(audioWav, vocalsWav, true);
            if (!System.IO.File.Exists(bgWav)) System.IO.File.Copy(audioWav, bgWav, true);
        }

        return System.IO.File.Exists(ogvTarget) ? ogvTarget : null;
    }
}


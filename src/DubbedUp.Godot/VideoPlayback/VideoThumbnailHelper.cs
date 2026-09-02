using System.Diagnostics;
using Godot;

namespace DubbedUp.Godot.VideoPlayback;

/// <summary>
/// Extracts and caches 16:9 video frame thumbnails at high speed using FFmpeg.
/// </summary>
public static class VideoThumbnailHelper
{
    private static readonly Dictionary<string, Texture2D> _memoryCache = [];

    public static string GetThumbnailCacheDirectory()
    {
        var dir = ProjectSettings.GlobalizePath("user://thumbnails");
        if (!System.IO.Directory.Exists(dir))
        {
            try { System.IO.Directory.CreateDirectory(dir); } catch { /* Ignore */ }
        }
        return dir;
    }

    /// <summary>
    /// Returns a Texture2D for a given video file or cover image.
    /// If no thumbnail exists, extracts a frame from the video at 1.0s.
    /// </summary>
    public static Texture2D? GetOrExtractThumbnail(string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath)) return null;

        if (_memoryCache.TryGetValue(mediaPath, out var cached) && cached is not null)
        {
            return cached;
        }

        // If mediaPath is already an image file
        var ext = System.IO.Path.GetExtension(mediaPath).ToLowerInvariant();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".webp")
        {
            if (System.IO.File.Exists(mediaPath))
            {
                var tex = LoadTextureFromFile(mediaPath);
                if (tex is not null)
                {
                    _memoryCache[mediaPath] = tex;
                    return tex;
                }
            }
        }

        // If it is a directory (scene package folder)
        string? packageThumbFile = null;
        if (System.IO.Directory.Exists(mediaPath))
        {
            var packageDir = mediaPath;
            packageThumbFile = System.IO.Path.Combine(packageDir, "thumbnail.png");

            var possibleImages = new[]
            {
                packageThumbFile,
                System.IO.Path.Combine(packageDir, "cover.png"),
                System.IO.Path.Combine(packageDir, "cover.jpg"),
                System.IO.Path.Combine(packageDir, "media", "thumbnail.png"),
            };

            foreach (var img in possibleImages)
            {
                if (System.IO.File.Exists(img) && new System.IO.FileInfo(img).Length > 100)
                {
                    var tex = LoadTextureFromFile(img);
                    if (tex is not null)
                    {
                        _memoryCache[mediaPath] = tex;
                        return tex;
                    }
                }
            }

            // Look for video inside directory
            var possibleVideos = new[]
            {
                System.IO.Path.Combine(packageDir, "media", "source_input.mp4"),
                System.IO.Path.Combine(packageDir, "media", "video.ogv"),
                System.IO.Path.Combine(packageDir, "media", "scene.ogv"),
            };

            foreach (var vid in possibleVideos)
            {
                if (System.IO.File.Exists(vid))
                {
                    mediaPath = vid;
                    break;
                }
            }
        }

        if (!System.IO.File.Exists(mediaPath)) return null;

        // Extract thumbnail using FFmpeg (saving directly to package folder if available)
        var thumbPath = ExtractVideoThumbnailToFile(mediaPath, packageThumbFile);
        if (thumbPath is not null && System.IO.File.Exists(thumbPath))
        {
            var tex = LoadTextureFromFile(thumbPath);
            if (tex is not null)
            {
                _memoryCache[mediaPath] = tex;
                return tex;
            }
        }

        return null;
    }

    /// <summary>
    /// Invokes FFmpeg to capture a crisp 16:9 thumbnail frame at 2.0s into the video.
    /// Uses SHA256 path hash to prevent thumbnail collision between videos with identical names.
    /// </summary>
    public static string? ExtractVideoThumbnailToFile(string videoFilePath, string? preferredOutPath = null)
    {
        try
        {
            if (!System.IO.File.Exists(videoFilePath)) return null;

            string thumbOutPath;
            if (!string.IsNullOrWhiteSpace(preferredOutPath))
            {
                thumbOutPath = preferredOutPath;
            }
            else
            {
                var cacheDir = GetThumbnailCacheDirectory();
                var fullNorm = System.IO.Path.GetFullPath(videoFilePath).ToLowerInvariant();
                var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fullNorm));
                var hashStr = Convert.ToHexString(hashBytes)[..12].ToLowerInvariant();
                var fileName = System.IO.Path.GetFileNameWithoutExtension(videoFilePath);
                var safeName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^a-zA-Z0-9_-]", "_");
                thumbOutPath = System.IO.Path.Combine(cacheDir, $"{safeName}_{hashStr}_thumb.png");
            }

            if (System.IO.File.Exists(thumbOutPath))
            {
                var fileInfo = new System.IO.FileInfo(thumbOutPath);
                if (fileInfo.Length > 100) return thumbOutPath;
            }

            var videoDir = System.IO.Path.GetDirectoryName(videoFilePath) ?? "";
            var videoFileName = System.IO.Path.GetFileName(videoFilePath);

            // Capture at 2.0 seconds into video to avoid black opening frames
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -loglevel error -ss 00:00:02 -i \"{videoFileName}\" -vframes 1 -vf \"scale=320:180:force_original_aspect_ratio=increase,crop=320:180\" \"{thumbOutPath}\"",
                WorkingDirectory = videoDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            proc.WaitForExit(6000);

            if (System.IO.File.Exists(thumbOutPath) && new System.IO.FileInfo(thumbOutPath).Length > 100)
            {
                return thumbOutPath;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VideoThumbnailHelper] Failed to extract thumbnail for '{videoFilePath}': {ex.Message}");
        }

        return null;
    }

    private static ImageTexture? LoadTextureFromFile(string imagePath)
    {
        try
        {
            if (!System.IO.File.Exists(imagePath)) return null;

            var image = Image.LoadFromFile(imagePath);
            if (image is null || image.IsEmpty()) return null;

            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VideoThumbnailHelper] Error loading image texture from '{imagePath}': {ex.Message}");
            return null;
        }
    }
}


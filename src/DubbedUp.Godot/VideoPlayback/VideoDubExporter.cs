using System.Globalization;
using System.Text;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.VoiceTakes;
using Godot;

namespace DubbedUp.Godot.VideoPlayback;

/// <summary>
/// Handles mixing source scene video, background music/effects, and recorded player voice takes
/// into a high-quality, standalone MP4 video file using FFmpeg.
/// </summary>
public static class VideoDubExporter
{
    public static async Task<string?> ExportDubbedVideoAsync(
        OfficialSceneDocument scene,
        string? packageDirectory,
        VoiceTakeStore takeStore,
        Action<string>? onStatusUpdate = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                onStatusUpdate?.Invoke("🔍 Locating scene media and voice takes...");

                // 1. Determine Source Video
                string? sourceVideo = ResolveSourceVideo(scene, packageDirectory);
                if (string.IsNullOrEmpty(sourceVideo) || !System.IO.File.Exists(sourceVideo))
                {
                    GD.PrintErr($"[VideoDubExporter] Source video not found for scene '{scene.SceneId}'");
                    return null;
                }

                // 2. Determine Background / Music Audio
                string? bgAudio = ResolveBackgroundAudio(scene, packageDirectory);

                // 3. Collect Voice Takes mapped to timeline positions
                var activeTakes = new List<(string TakePath, long StartMs, long EndMs)>();
                foreach (var slot in scene.VoiceSlots)
                {
                    var take = takeStore.GetLatestTakeForSlot(slot.VoiceSlotId);
                    if (take is null) continue;

                    var timelineEntry = scene.Timeline.FirstOrDefault(t => t.VoiceSlotId == slot.VoiceSlotId);
                    long startMs = timelineEntry?.StartMilliseconds ?? 0;
                    long endMs = timelineEntry?.EndMilliseconds ?? (startMs + take.DurationMilliseconds);

                    var takeFullPath = ProjectSettings.GlobalizePath(take.AudioRelativePath);
                    if (System.IO.File.Exists(takeFullPath))
                    {
                        activeTakes.Add((takeFullPath, startMs, endMs));
                    }
                }

                // 4. Create Export Directory
                var exportDir = ProjectSettings.GlobalizePath("user://exports");
                if (!System.IO.Directory.Exists(exportDir))
                {
                    System.IO.Directory.CreateDirectory(exportDir);
                }

                var safeTitle = string.Concat(scene.Title.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(' ', '_');
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var exportMp4 = System.IO.Path.Combine(exportDir, $"DubbedUp_{safeTitle}_{timestamp}.mp4");

                onStatusUpdate?.Invoke("🎬 Rendering & mixing dubbed MP4 video with FFmpeg...");

                // 5. Construct FFmpeg command
                var cmdBuilder = new StringBuilder();
                cmdBuilder.Append($"-y -loglevel error -i \"{sourceVideo}\" ");

                int inputIndex = 1;
                int bgInputIndex = -1;

                if (!string.IsNullOrEmpty(bgAudio) && System.IO.File.Exists(bgAudio))
                {
                    cmdBuilder.Append($"-i \"{bgAudio}\" ");
                    bgInputIndex = inputIndex++;
                }

                var takeIndices = new List<(int InputIdx, long StartMs)>();
                foreach (var take in activeTakes)
                {
                    cmdBuilder.Append($"-i \"{take.TakePath}\" ");
                    takeIndices.Add((inputIndex++, take.StartMs));
                }

                // Filter Complex building
                var filterBuilder = new StringBuilder();
                var mixInputs = new List<string>();

                // Process background audio (duck dialogue sections during player lines)
                if (bgInputIndex >= 0)
                {
                    if (activeTakes.Count > 0)
                    {
                        var volClauses = new List<string>();
                        foreach (var t in activeTakes)
                        {
                            var s = (t.StartMs / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);
                            var e = (t.EndMs / 1000.0).ToString("0.00", CultureInfo.InvariantCulture);
                            volClauses.Add($"between(t\\,{s}\\,{e})");
                        }
                        var duckExpr = string.Join("+", volClauses);
                        filterBuilder.Append($"[{bgInputIndex}:a]volume=enable='{duckExpr}':volume=0.0[bg_ducked];");
                        mixInputs.Add("[bg_ducked]");
                    }
                    else
                    {
                        mixInputs.Add($"[{bgInputIndex}:a]");
                    }
                }

                // Process voice takes with exact millisecond delay
                for (int i = 0; i < takeIndices.Count; i++)
                {
                    var (idx, startMs) = takeIndices[i];
                    filterBuilder.Append($"[{idx}:a]adelay={startMs}|{startMs}[take_{i}];");
                    mixInputs.Add($"[take_{i}]");
                }

                // Mix all audio streams together
                if (mixInputs.Count > 0)
                {
                    filterBuilder.Append(string.Join("", mixInputs));
                    filterBuilder.Append($"amix=inputs={mixInputs.Count}:normalize=0:duration=first[aout]");
                }

                string filterStr = filterBuilder.ToString();
                if (!string.IsNullOrEmpty(filterStr))
                {
                    cmdBuilder.Append($"-filter_complex \"{filterStr}\" -map 0:v:0 -map \"[aout]\" ");
                }
                else
                {
                    cmdBuilder.Append($"-map 0:v:0 ");
                }

                // Video & Audio Codec settings (Full HD 1080p H.264 + 256k AAC high fidelity)
                cmdBuilder.Append($"-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a aac -b:a 256k -movflags +faststart \"{exportMp4}\"");

                var args = cmdBuilder.ToString();
                GD.Print($"[VideoDubExporter] Executing FFmpeg export command...");
                MediaTranscoder.RunProcess("ffmpeg", args, 180000);

                if (System.IO.File.Exists(exportMp4) && new System.IO.FileInfo(exportMp4).Length > 1000)
                {
                    GD.Print($"[VideoDubExporter] Successfully exported dubbed video: '{exportMp4}'");
                    onStatusUpdate?.Invoke($"✅ Export complete! Video saved to: {System.IO.Path.GetFileName(exportMp4)}");
                    return exportMp4;
                }

                GD.PrintErr($"[VideoDubExporter] Export failed or produced empty file: '{exportMp4}'");
                return null;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[VideoDubExporter] Export exception: {ex.Message}");
                return null;
            }
        });
    }

    private static string? ResolveSourceVideo(OfficialSceneDocument scene, string? packageDirectory)
    {
        // Check for MP4 source input first for best export quality
        if (!string.IsNullOrEmpty(packageDirectory))
        {
            var mp4 = System.IO.Path.Combine(packageDirectory, "media", "source_input.mp4");
            if (System.IO.File.Exists(mp4)) return mp4;

            var ogv = System.IO.Path.Combine(packageDirectory, "media", "video.ogv");
            if (System.IO.File.Exists(ogv)) return ogv;

            ogv = System.IO.Path.Combine(packageDirectory, "media", "scene.ogv");
            if (System.IO.File.Exists(ogv)) return ogv;
        }

        var videoAsset = scene.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        if (videoAsset is not null && !string.IsNullOrEmpty(videoAsset.RelativePath))
        {
            if (!string.IsNullOrEmpty(packageDirectory))
            {
                var combined = System.IO.Path.Combine(packageDirectory, videoAsset.RelativePath);
                if (System.IO.File.Exists(combined)) return combined;
            }

            var glob = ProjectSettings.GlobalizePath(videoAsset.RelativePath);
            if (System.IO.File.Exists(glob)) return glob;

            var resGlob = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{scene.SceneId}/{videoAsset.RelativePath}");
            if (System.IO.File.Exists(resGlob)) return resGlob;
        }

        return null;
    }

    private static string? ResolveBackgroundAudio(OfficialSceneDocument scene, string? packageDirectory)
    {
        if (!string.IsNullOrEmpty(packageDirectory))
        {
            var bg = System.IO.Path.Combine(packageDirectory, "media", "background.wav");
            if (System.IO.File.Exists(bg)) return bg;

            var audio = System.IO.Path.Combine(packageDirectory, "media", "audio.wav");
            if (System.IO.File.Exists(audio)) return audio;
        }

        var resBg = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{scene.SceneId}/media/background.wav");
        if (System.IO.File.Exists(resBg)) return resBg;

        var resAudio = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{scene.SceneId}/media/audio.wav");
        if (System.IO.File.Exists(resAudio)) return resAudio;

        return null;
    }
}


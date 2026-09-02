using DubbedUp.Core.Ai;
using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.Ai;

/// <summary>
/// Handles offline / local AI speech segment extraction, acoustic activity detection,
/// and subtitle/transcript parsing for automatic scene creation.
/// </summary>
public static class LocalAiSceneExtractor
{
    /// <summary>
    /// Parses an SRT (SubRip) subtitle format into detected speech segments with precise millisecond timestamps.
    /// </summary>
    public static IReadOnlyList<DetectedSpeechSegment> ParseSrtText(string srtContent)
    {
        var segments = new List<DetectedSpeechSegment>();
        var lines = srtContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int i = 0;
        int speakerIdx = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            // Sequence number
            if (int.TryParse(line, out _))
            {
                i++;
                if (i >= lines.Length) break;

                // Time code: 00:00:01,000 --> 00:00:04,500
                var timeLine = lines[i].Trim();
                var timeParts = timeLine.Split(new[] { "-->" }, StringSplitOptions.TrimEntries);
                if (timeParts.Length == 2 &&
                    TryParseSrtTimestamp(timeParts[0], out var startMs) &&
                    TryParseSrtTimestamp(timeParts[1], out var endMs))
                {
                    i++;
                    var textLines = new List<string>();
                    while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                    {
                        textLines.Add(lines[i].Trim());
                        i++;
                    }

                    var fullPrompt = string.Join(" ", textLines);
                    if (!string.IsNullOrWhiteSpace(fullPrompt) && endMs > startMs)
                    {
                        // Check if text has speaker prefix like "Speed: ..." or "Acey: ..."
                        string charId;
                        string displayName;
                        var colonIdx = fullPrompt.IndexOf(':');

                        if (colonIdx > 0 && colonIdx < 25)
                        {
                            displayName = fullPrompt[..colonIdx].Trim();
                            charId = displayName.ToLowerInvariant().Replace(' ', '-');
                            fullPrompt = fullPrompt[(colonIdx + 1)..].Trim();
                        }
                        else
                        {
                            charId = $"speaker-{(speakerIdx % 2) + 1}";
                            displayName = $"Speaker {(speakerIdx % 2) + 1}";
                            speakerIdx++;
                        }

                        segments.Add(new DetectedSpeechSegment(charId, displayName, fullPrompt, startMs, endMs));
                    }
                }
            }
            i++;
        }

        return segments;
    }

    /// <summary>
    /// Reads a WAV file and uses Whisper Speech-To-Text to automatically extract subtitles and exact sentence boundaries.
    /// Where no words/subtitles are found (silence or pure music), NO boxes are generated.
    /// Falls back to offline C# acoustic Voice Activity Detection (VAD) if Whisper is not available.
    /// </summary>
    public static IReadOnlyList<DetectedSpeechSegment> DetectSpeechFromWavFile(string wavFilePath, int maxSlots = 30, double silenceThresholdSec = 0.5)
    {
        if (string.IsNullOrWhiteSpace(wavFilePath) || !System.IO.File.Exists(wavFilePath))
        {
            return Array.Empty<DetectedSpeechSegment>();
        }

        try
        {
            // 1. Prioritize Whisper for actual spoken subtitles with silence rejection
            var whisperSegments = DetectSpeechWithWhisper(wavFilePath, maxSlots);
            if (whisperSegments is not null && whisperSegments.Count > 0)
            {
                return whisperSegments;
            }

            // 2. Fallback to pure C# acoustic energy detection
            var bytes = System.IO.File.ReadAllBytes(wavFilePath);
            return DetectSpeechFromWavBytes(bytes, silenceThresholdSec, maxSlots);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LocalAiSceneExtractor] Error detecting speech from '{wavFilePath}': {ex.Message}");
            return Array.Empty<DetectedSpeechSegment>();
        }
    }

    /// <summary>
    /// Invokes offline Whisper model to automatically extract spoken subtitles and exact sentence boundaries.
    /// Returns only segments where speech/subtitles actually exist (no ghost boxes during silence or pure music).
    /// </summary>
    public static IReadOnlyList<DetectedSpeechSegment>? DetectSpeechWithWhisper(string wavFilePath, int maxSlots = 30)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(wavFilePath) || !System.IO.File.Exists(wavFilePath)) return null;

            // Find whisper_transcribe.py
            var possiblePaths = new[]
            {
                ProjectSettings.GlobalizePath("res://Scripts/whisper_transcribe.py"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "whisper_transcribe.py"),
                System.IO.Path.GetFullPath("src/DubbedUp.Godot/Scripts/whisper_transcribe.py"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "src", "DubbedUp.Godot", "Scripts", "whisper_transcribe.py")
            };

            string? scriptPath = null;
            foreach (var p in possiblePaths)
            {
                if (System.IO.File.Exists(p))
                {
                    scriptPath = p;
                    break;
                }
            }

            if (scriptPath is null) return null;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ExternalToolLocator.ResolveWhisperPython(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };

            // Force Python to run in full UTF-8 mode on Windows for Turkish characters (ç, ğ, ı, ö, ş, ü, vb.)
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["DUBBEDUP_WHISPER_MODEL"] = ExternalToolLocator.ResolveWhisperModel();
            ExternalToolLocator.AddFfmpegToPath(psi);
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(wavFilePath);

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return null;

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(120000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                GD.PrintErr("[LocalAiSceneExtractor] Whisper timed out after 120 seconds.");
                return null;
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            if (proc.ExitCode != 0)
            {
                GD.PrintErr($"[LocalAiSceneExtractor] Whisper exited with code {proc.ExitCode}: {stderr.Trim()}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(stdout)) return null;

            int startIdx = stdout.IndexOf("---WHISPER_JSON_START---", StringComparison.Ordinal);
            int endIdx = stdout.IndexOf("---WHISPER_JSON_END---", StringComparison.Ordinal);

            if (startIdx >= 0 && endIdx > startIdx)
            {
                var json = stdout.Substring(startIdx + 24, endIdx - (startIdx + 24)).Trim();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                var segments = new List<DetectedSpeechSegment>();
                int speakerIdx = 0;

                foreach (var el in jsonDoc.RootElement.EnumerateArray())
                {
                    if (segments.Count >= maxSlots) break;

                    long startMs = el.GetProperty("startMs").GetInt64();
                    long endMs = el.GetProperty("endMs").GetInt64();
                    string text = el.GetProperty("text").GetString()?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(text) && endMs > startMs)
                    {
                        var speakerNum = (speakerIdx % 2) + 1;
                        segments.Add(new DetectedSpeechSegment(
                            $"char-{speakerNum}",
                            $"Character {speakerNum}",
                            text,
                            startMs,
                            endMs));
                        speakerIdx++;
                    }
                }

                if (segments.Count > 0)
                {
                    GD.Print($"[LocalAiSceneExtractor] Whisper extracted {segments.Count} spoken subtitle lines!");
                    return segments;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LocalAiSceneExtractor] Whisper extraction exception: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Analyzes an uncompressed WAV byte stream using vocal-frequency bandpass Voice Activity Detection (VAD)
    /// to detect speech bursts, silence boundaries, and automatic dialogue timings up to maxSlots.
    /// Splits continuously into new boxes whenever speech pauses for >= 350ms or exceeds 4.5s.
    /// </summary>
    public static IReadOnlyList<DetectedSpeechSegment> DetectSpeechFromWavBytes(byte[] wavBytes, double silenceThresholdSec = 0.35, int maxSlots = 20)
    {
        var segments = new List<DetectedSpeechSegment>();

        if (wavBytes.Length < 44)
        {
            return segments;
        }

        // Parse WAV sample rate and format
        int sampleRate = BitConverter.ToInt32(wavBytes, 24);
        short channels = BitConverter.ToInt16(wavBytes, 22);
        short bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

        if (sampleRate <= 0 || channels <= 0 || bitsPerSample != 16)
        {
            return segments;
        }

        int bytesPerSample = bitsPerSample / 8;
        int frameSizeSamples = sampleRate / 25; // 40ms windows for crisp pause detection
        int frameSizeBytes = frameSizeSamples * channels * bytesPerSample;

        int offset = 44; // Start of PCM data

        // 1. First pass: compute average vocal-band RMS energy across frames
        double totalRms = 0.0;
        int frameCount = 0;
        double maxRms = 0.0;

        // Bandpass filter state (simple RC filter: HPF 200Hz, LPF 3500Hz for human vocal formants)
        double dt = 1.0 / sampleRate;
        double rcHigh = 1.0 / (2.0 * Math.PI * 200.0);
        double alphaHigh = rcHigh / (rcHigh + dt);
        double rcLow = 1.0 / (2.0 * Math.PI * 3500.0);
        double alphaLow = dt / (rcLow + dt);

        while (offset + frameSizeBytes <= wavBytes.Length)
        {
            double sumSquare = 0;
            int samplesInFrame = 0;
            double prevIn = 0;
            double prevHp = 0;
            double prevLp = 0;

            for (int i = 0; i < frameSizeSamples * channels; i += channels)
            {
                short sample = BitConverter.ToInt16(wavBytes, offset + (i * 2));
                double input = sample;

                // Highpass 200Hz (cuts deep bass/drums)
                double hp = alphaHigh * (prevHp + input - prevIn);
                prevIn = input;
                prevHp = hp;

                // Lowpass 3500Hz (cuts cymbals/high hats)
                double lp = prevLp + alphaLow * (hp - prevLp);
                prevLp = lp;

                sumSquare += lp * lp;
                samplesInFrame++;
            }

            double frameRms = Math.Sqrt(sumSquare / Math.Max(1, samplesInFrame));
            totalRms += frameRms;
            if (frameRms > maxRms) maxRms = frameRms;
            frameCount++;
            offset += frameSizeBytes;
        }

        double avgRms = frameCount > 0 ? totalRms / frameCount : 500.0;
        // Music rejection: threshold set above background noise floor and bass/drum bleed
        double dynamicThreshold = Math.Max(400.0, Math.Min(avgRms * 1.30, maxRms * 0.28));

        // 2. Second pass: detect speech bursts and split on pauses >= 350ms
        offset = 44;
        long currentMs = 0;
        long segmentStartMs = -1;
        long lastSpeechMs = -1;
        int segmentIndex = 1;

        while (offset + frameSizeBytes <= wavBytes.Length && segments.Count < maxSlots)
        {
            double sumSquare = 0;
            int samplesInFrame = 0;
            double prevIn = 0;
            double prevHp = 0;
            double prevLp = 0;

            for (int i = 0; i < frameSizeSamples * channels; i += channels)
            {
                short sample = BitConverter.ToInt16(wavBytes, offset + (i * 2));
                double input = sample;

                double hp = alphaHigh * (prevHp + input - prevIn);
                prevIn = input;
                prevHp = hp;

                double lp = prevLp + alphaLow * (hp - prevLp);
                prevLp = lp;

                sumSquare += lp * lp;
                samplesInFrame++;
            }

            double frameRms = Math.Sqrt(sumSquare / Math.Max(1, samplesInFrame));
            bool isSpeech = frameRms > dynamicThreshold;

            if (isSpeech)
            {
                if (segmentStartMs < 0)
                {
                    segmentStartMs = Math.Max(0, currentMs - 80); // 80ms lead-in
                }
                lastSpeechMs = currentMs + 40;
            }
            else
            {
                // If speech stops for >= silenceThresholdSec (default 350ms pause), close box & start new on next phrase
                if (segmentStartMs >= 0 && (currentMs - lastSpeechMs) >= (silenceThresholdSec * 1000.0))
                {
                    var segmentEndMs = lastSpeechMs + 150;
                    if (segmentEndMs - segmentStartMs >= 700) // Minimum 700ms for a speech line
                    {
                        AddSegment(segments, segmentStartMs, segmentEndMs, ref segmentIndex, maxSlots);
                    }
                    segmentStartMs = -1;
                }
            }

            offset += frameSizeBytes;
            currentMs += 40;
        }

        // Close trailing segment if active
        if (segmentStartMs >= 0 && lastSpeechMs > segmentStartMs + 700 && segments.Count < maxSlots)
        {
            AddSegment(segments, segmentStartMs, lastSpeechMs + 150, ref segmentIndex, maxSlots);
        }

        return segments;
    }

    private static void AddSegment(List<DetectedSpeechSegment> segments, long startMs, long endMs, ref int segmentIndex, int maxSlots)
    {
        if (segments.Count >= maxSlots) return;

        var speakerNum = ((segmentIndex - 1) % 2) + 1;
        segments.Add(new DetectedSpeechSegment(
            $"char-{speakerNum}",
            $"Character {speakerNum}",
            $"Line {segmentIndex} (Auto-detected dialogue)",
            startMs,
            endMs));
        segmentIndex++;
    }

    private static bool TryParseSrtTimestamp(string timecode, out long milliseconds)
    {
        milliseconds = 0;
        try
        {
            // Format: 00:00:01,500 or 00:00:01.500
            var normalized = timecode.Replace(',', '.');
            if (TimeSpan.TryParse(normalized, out var ts))
            {
                milliseconds = (long)ts.TotalMilliseconds;
                return true;
            }
        }
        catch
        {
            // fallback
        }
        return false;
    }
}


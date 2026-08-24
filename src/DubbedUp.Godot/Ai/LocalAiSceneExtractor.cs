using DubbedUp.Core.Ai;
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
    /// Analyzes an uncompressed WAV byte stream using energy-based Voice Activity Detection (VAD)
    /// to detect speech bursts, silence boundaries, and automatic dialogue timings.
    /// </summary>
    public static IReadOnlyList<DetectedSpeechSegment> DetectSpeechFromWavBytes(byte[] wavBytes, double silenceThresholdSec = 0.6)
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
        int frameSizeSamples = sampleRate / 20; // 50ms windows
        int frameSizeBytes = frameSizeSamples * channels * bytesPerSample;

        int offset = 44; // Start of PCM
        long currentMs = 0;
        long segmentStartMs = -1;
        long lastSpeechMs = -1;
        int segmentIndex = 1;

        // Energy threshold for speech vs background
        const double energyThreshold = 800.0;

        while (offset + frameSizeBytes <= wavBytes.Length)
        {
            double sumSquare = 0;
            int samplesInFrame = 0;

            for (int i = 0; i < frameSizeSamples * channels; i++)
            {
                short sample = BitConverter.ToInt16(wavBytes, offset + (i * 2));
                sumSquare += sample * sample;
                samplesInFrame++;
            }

            double rms = Math.Sqrt(sumSquare / Math.Max(1, samplesInFrame));
            bool isSpeech = rms > energyThreshold;

            if (isSpeech)
            {
                if (segmentStartMs < 0)
                {
                    segmentStartMs = Math.Max(0, currentMs - 100); // 100ms lead-in
                }
                lastSpeechMs = currentMs + 50;
            }
            else
            {
                if (segmentStartMs >= 0 && (currentMs - lastSpeechMs) > (silenceThresholdSec * 1000.0))
                {
                    var segmentEndMs = lastSpeechMs + 200;
                    if (segmentEndMs - segmentStartMs >= 1000) // At least 1 second
                    {
                        var speakerNum = ((segmentIndex - 1) % 2) + 1;
                        segments.Add(new DetectedSpeechSegment(
                            $"speaker-{speakerNum}",
                            $"Character {speakerNum}",
                            $"Replik {segmentIndex} (Seslendirin)",
                            segmentStartMs,
                            segmentEndMs));
                        segmentIndex++;
                    }
                    segmentStartMs = -1;
                }
            }

            offset += frameSizeBytes;
            currentMs += 50;
        }

        // Close last segment if active
        if (segmentStartMs >= 0 && lastSpeechMs > segmentStartMs + 1000)
        {
            var speakerNum = ((segmentIndex - 1) % 2) + 1;
            segments.Add(new DetectedSpeechSegment(
                $"speaker-{speakerNum}",
                $"Character {speakerNum}",
                $"Replik {segmentIndex} (Seslendirin)",
                segmentStartMs,
                lastSpeechMs + 200));
        }

        return segments;
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


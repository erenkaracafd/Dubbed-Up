using Godot;

namespace DubbedUp.Godot.AudioPlayback;

/// <summary>
/// Extracts real PCM audio waveform energy envelopes from WAV files for accurate Choicer-Voicer reference visualization.
/// </summary>
public static class AudioWaveformLoader
{
    public const int Resolution = 150;

    public static float[]? ExtractWaveformSegment(string wavFilePath, double startSeconds, double endSeconds)
    {
        if (string.IsNullOrWhiteSpace(wavFilePath)) return null;

        try
        {
            byte[]? bytes = null;
            if (global::Godot.FileAccess.FileExists(wavFilePath))
            {
                bytes = global::Godot.FileAccess.GetFileAsBytes(wavFilePath);
            }
            else
            {
                var globalPath = ProjectSettings.GlobalizePath(wavFilePath);
                if (System.IO.File.Exists(globalPath))
                {
                    bytes = System.IO.File.ReadAllBytes(globalPath);
                }
                else if (System.IO.File.Exists(wavFilePath))
                {
                    bytes = System.IO.File.ReadAllBytes(wavFilePath);
                }
            }

            if (bytes is null || bytes.Length < 44) return null;

            // Parse WAV header
            int sampleRate = BitConverter.ToInt32(bytes, 24);
            short channels = BitConverter.ToInt16(bytes, 22);
            short bitsPerSample = BitConverter.ToInt16(bytes, 34);

            if (sampleRate <= 0 || channels <= 0 || bitsPerSample != 16) return null;

            int bytesPerSample = (bitsPerSample / 8) * channels;
            int totalPcmBytes = bytes.Length - 44;
            int totalSamples = totalPcmBytes / bytesPerSample;
            double totalDuration = (double)totalSamples / sampleRate;

            var startSample = (int)(Math.Clamp(startSeconds, 0.0, totalDuration) * sampleRate);
            var endSample = (int)(Math.Clamp(endSeconds, startSeconds + 0.1, totalDuration) * sampleRate);
            var sampleCount = Math.Max(1, endSample - startSample);

            var samplesPerBin = (double)sampleCount / Resolution;
            var result = new float[Resolution];
            float maxRms = 0.001f;

            for (int bin = 0; bin < Resolution; bin++)
            {
                int binStart = startSample + (int)(bin * samplesPerBin);
                int binEnd = startSample + (int)((bin + 1) * samplesPerBin);
                binEnd = Math.Min(binEnd, totalSamples);

                double sumSquares = 0;
                int count = 0;

                for (int s = binStart; s < binEnd; s++)
                {
                    int byteOffset = 44 + (s * bytesPerSample);
                    if (byteOffset + 1 < bytes.Length)
                    {
                        short sample = BitConverter.ToInt16(bytes, byteOffset);
                        sumSquares += sample * sample;
                        count++;
                    }
                }

                float rms = count > 0 ? (float)Math.Sqrt(sumSquares / count) : 0f;
                result[bin] = rms;
                if (rms > maxRms) maxRms = rms;
            }

            // Normalize between 0.0 and 1.0 with noise gating
            for (int bin = 0; bin < Resolution; bin++)
            {
                var normalized = result[bin] / maxRms;
                // Subtle curve shaping
                result[bin] = (float)Math.Clamp(Math.Pow(normalized, 0.85), 0.0, 1.0);
            }

            return result;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[WaveformLoader] Failed extracting waveform: {ex.Message}");
            return null;
        }
    }
}

using Godot;

namespace DubbedUp.Godot.AudioPlayback;

/// <summary>
/// Extracts real PCM audio waveform energy envelopes from WAV files for accurate Choicer-Voicer reference visualization.
/// Robust against arbitrary RIFF/WAVE chunks (e.g. FFmpeg INFO, LIST, JUNK chunks).
/// </summary>
public static class AudioWaveformLoader
{
    public const int Resolution = 150;

    private readonly struct WavInfo
    {
        public readonly int SampleRate;
        public readonly int Channels;
        public readonly int BitsPerSample;
        public readonly int AudioFormat; // 1 = PCM, 3 = IEEE Float
        public readonly int DataOffset;
        public readonly int DataSize;
        public readonly bool IsValid;

        public WavInfo(int sampleRate, int channels, int bitsPerSample, int audioFormat, int dataOffset, int dataSize)
        {
            SampleRate = sampleRate;
            Channels = channels;
            BitsPerSample = bitsPerSample;
            AudioFormat = audioFormat;
            DataOffset = dataOffset;
            DataSize = dataSize;
            IsValid = sampleRate > 0 && channels > 0 && bitsPerSample > 0 && dataOffset > 0 && dataSize > 0;
        }
    }

    private static byte[]? LoadFileBytes(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        try
        {
            if (global::Godot.FileAccess.FileExists(filePath))
            {
                return global::Godot.FileAccess.GetFileAsBytes(filePath);
            }

            var globalPath = ProjectSettings.GlobalizePath(filePath);
            if (System.IO.File.Exists(globalPath))
            {
                return System.IO.File.ReadAllBytes(globalPath);
            }

            if (System.IO.File.Exists(filePath))
            {
                return System.IO.File.ReadAllBytes(filePath);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[AudioWaveformLoader] Failed to read bytes from '{filePath}': {ex.Message}");
        }

        return null;
    }

    private static WavInfo ParseWavHeader(byte[] bytes)
    {
        if (bytes.Length < 44) return default;

        // Verify RIFF & WAVE magic markers
        if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F' ||
            bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
        {
            return default;
        }

        int pos = 12;
        int sampleRate = 44100;
        int channels = 2;
        int bitsPerSample = 16;
        int audioFormat = 1;
        int dataOffset = -1;
        int dataSize = -1;

        while (pos + 8 <= bytes.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
            var chunkSize = BitConverter.ToInt32(bytes, pos + 4);
            pos += 8;

            if (chunkSize < 0 || pos + chunkSize > bytes.Length)
            {
                // In streaming/broken chunks, read whatever remains
                if (chunkId == "data")
                {
                    dataOffset = pos;
                    dataSize = bytes.Length - pos;
                }
                break;
            }

            if (chunkId == "fmt ")
            {
                if (chunkSize >= 16)
                {
                    audioFormat = BitConverter.ToInt16(bytes, pos);
                    channels = BitConverter.ToInt16(bytes, pos + 2);
                    sampleRate = BitConverter.ToInt32(bytes, pos + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, pos + 14);
                }
            }
            else if (chunkId == "data")
            {
                dataOffset = pos;
                dataSize = chunkSize;
                break; // Found primary data chunk
            }

            pos += chunkSize;
            if ((chunkSize & 1) != 0) pos++; // Word alignment
        }

        return new WavInfo(sampleRate, channels, bitsPerSample, audioFormat, dataOffset, dataSize);
    }

    public static double GetAudioDurationSeconds(string wavFilePath)
    {
        var bytes = LoadFileBytes(wavFilePath);
        if (bytes is null) return 0.0;

        var info = ParseWavHeader(bytes);
        if (!info.IsValid) return 0.0;

        int bytesPerSample = (info.BitsPerSample / 8) * info.Channels;
        if (bytesPerSample <= 0) return 0.0;

        int totalSamples = info.DataSize / bytesPerSample;
        return (double)totalSamples / info.SampleRate;
    }

    public static float[]? ExtractWaveformSegment(string wavFilePath, double startSeconds, double endSeconds, int resolution = 350)
    {
        var bytes = LoadFileBytes(wavFilePath);
        if (bytes is null) return null;

        var info = ParseWavHeader(bytes);
        if (!info.IsValid) return null;

        int bytesPerFrame = (info.BitsPerSample / 8) * info.Channels;
        if (bytesPerFrame <= 0) return null;

        int totalFrames = info.DataSize / bytesPerFrame;
        double totalDuration = (double)totalFrames / info.SampleRate;

        int startFrame = (int)(Math.Clamp(startSeconds, 0.0, totalDuration) * info.SampleRate);
        int endFrame = (int)(Math.Clamp(endSeconds, startSeconds + 0.05, totalDuration) * info.SampleRate);
        int frameCount = Math.Max(1, endFrame - startFrame);

        double framesPerBin = (double)frameCount / resolution;
        var result = new float[resolution];
        float maxEnergy = 0.0001f;

        for (int bin = 0; bin < resolution; bin++)
        {
            int binStart = startFrame + (int)(bin * framesPerBin);
            int binEnd = startFrame + (int)((bin + 1) * framesPerBin);
            binEnd = Math.Min(binEnd, totalFrames);

            double sumSquares = 0;
            int count = 0;

            for (int f = binStart; f < binEnd; f++)
            {
                int frameOffset = info.DataOffset + (f * bytesPerFrame);
                if (frameOffset + bytesPerFrame > bytes.Length) break;

                float sampleVal = 0.0f;

                if (info.BitsPerSample == 16)
                {
                    short left = BitConverter.ToInt16(bytes, frameOffset);
                    short right = info.Channels > 1 ? BitConverter.ToInt16(bytes, frameOffset + 2) : left;
                    sampleVal = ((left + right) / 2.0f) / 32768.0f;
                }
                else if (info.BitsPerSample == 32)
                {
                    if (info.AudioFormat == 3) // Float
                    {
                        float left = BitConverter.ToSingle(bytes, frameOffset);
                        float right = info.Channels > 1 ? BitConverter.ToSingle(bytes, frameOffset + 4) : left;
                        sampleVal = (left + right) / 2.0f;
                    }
                    else // 32-bit int
                    {
                        int left = BitConverter.ToInt32(bytes, frameOffset);
                        int right = info.Channels > 1 ? BitConverter.ToInt32(bytes, frameOffset + 4) : left;
                        sampleVal = ((left >> 16) + (right >> 16)) / 65536.0f;
                    }
                }
                else if (info.BitsPerSample == 8)
                {
                    byte left = bytes[frameOffset];
                    byte right = info.Channels > 1 ? bytes[frameOffset + 1] : left;
                    sampleVal = (((left + right) / 2.0f) - 128.0f) / 128.0f;
                }

                sumSquares += sampleVal * sampleVal;
                count++;
            }

            float rms = count > 0 ? (float)Math.Sqrt(sumSquares / count) : 0.0f;
            result[bin] = rms;
            if (rms > maxEnergy) maxEnergy = rms;
        }

        // Normalize waveform with slight perceptual compression for clean UI visualization
        for (int bin = 0; bin < resolution; bin++)
        {
            var norm = result[bin] / maxEnergy;
            result[bin] = (float)Math.Clamp(Math.Pow(norm, 0.75), 0.0, 1.0);
        }

        return result;
    }

    public static float[]? ExtractFullWaveform(string wavFilePath, int resolution = 350)
    {
        return ExtractWaveformSegment(wavFilePath, 0.0, 99999.0, resolution);
    }
}

using System.Buffers.Binary;

namespace DubbedUp.Core.VoiceTakes;

/// <summary>
/// Shrinks recorded voice takes before they are sent to other players.
/// Microphone captures arrive as 16-bit PCM WAV (often stereo at 44.1/48 kHz); speech only needs
/// one channel at 24 kHz, which cuts the payload roughly 4x with no audible loss for dialogue.
/// </summary>
public static class VoiceTakeAudioCompactor
{
    public const int SpeechSampleRate = 24_000;

    private const int WavHeaderSize = 44;

    /// <summary>
    /// Returns a mono 16-bit WAV at <paramref name="targetSampleRate"/> (or lower, if the source already is).
    /// Input that is not 16-bit PCM WAV is returned unchanged so callers can always send the result.
    /// </summary>
    public static byte[] ToMonoSpeechWav(byte[] wav, int targetSampleRate = SpeechSampleRate)
    {
        ArgumentNullException.ThrowIfNull(wav);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetSampleRate, 8_000);

        if (!TryReadPcm16(wav, out var sampleRate, out var channels, out var pcm))
        {
            return wav;
        }

        var outputRate = Math.Min(sampleRate, targetSampleRate);
        if (channels == 1 && outputRate == sampleRate)
        {
            return wav;
        }

        var mono = Downmix(pcm, channels);
        var resampled = outputRate == sampleRate ? mono : Resample(mono, sampleRate, outputRate);
        return CreateMonoWav(resampled, outputRate);
    }

    /// <summary>Reads a mono 16-bit PCM WAV into samples; false for any other format.</summary>
    public static bool TryReadMonoSamples(byte[] wav, out int sampleRate, out short[] samples)
    {
        samples = [];
        if (!TryReadPcm16(wav, out sampleRate, out var channels, out var pcm) || channels != 1)
        {
            return false;
        }

        var span = pcm.Span;
        samples = new short[span.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(i * 2, 2));
        }
        return true;
    }

    /// <summary>Reads the format and PCM samples of a 16-bit PCM WAV, walking RIFF chunks.</summary>
    public static bool TryReadPcm16(byte[] wav, out int sampleRate, out int channels, out ReadOnlyMemory<byte> pcm)
    {
        sampleRate = 0;
        channels = 0;
        pcm = ReadOnlyMemory<byte>.Empty;

        if (wav.Length < 12
            || !wav.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            || !wav.AsSpan(8, 4).SequenceEqual("WAVE"u8))
        {
            return false;
        }

        var hasFormat = false;
        var pos = 12;
        while (pos + 8 <= wav.Length)
        {
            var id = wav.AsSpan(pos, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(pos + 4, 4));
            var body = pos + 8;
            if (size < 0 || body + size > wav.Length)
            {
                // Some writers leave a bogus size on the data chunk; trust the remaining bytes.
                if (id.SequenceEqual("data"u8) && hasFormat)
                {
                    size = wav.Length - body;
                }
                else
                {
                    return false;
                }
            }

            if (id.SequenceEqual("fmt "u8) && size >= 16)
            {
                var format = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(body, 2));
                channels = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(body + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(body + 4, 4));
                var bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(body + 14, 2));
                hasFormat = format == 1 && bitsPerSample == 16 && channels is >= 1 and <= 8 && sampleRate > 0;
                if (!hasFormat) return false;
            }
            else if (id.SequenceEqual("data"u8) && hasFormat)
            {
                var frameBytes = channels * 2;
                pcm = new ReadOnlyMemory<byte>(wav, body, size - size % frameBytes);
                return true;
            }

            pos = body + size + (size & 1); // chunks are word aligned
        }

        return false;
    }

    private static short[] Downmix(ReadOnlyMemory<byte> pcm, int channels)
    {
        var span = pcm.Span;
        var frames = span.Length / (2 * channels);
        var mono = new short[frames];
        for (var f = 0; f < frames; f++)
        {
            var sum = 0;
            for (var c = 0; c < channels; c++)
            {
                sum += BinaryPrimitives.ReadInt16LittleEndian(span.Slice((f * channels + c) * 2, 2));
            }
            mono[f] = (short)(sum / channels);
        }
        return mono;
    }

    /// <summary>
    /// Downsamples with a box filter as wide as the rate ratio, which removes most content above
    /// the new Nyquist frequency (enough to avoid audible aliasing on speech).
    /// </summary>
    private static short[] Resample(short[] input, int fromRate, int toRate)
    {
        if (input.Length == 0) return input;

        var ratio = (double)fromRate / toRate;
        var outputLength = (int)Math.Floor(input.Length / ratio);
        var output = new short[outputLength];
        var halfWidth = Math.Max(0.5, ratio / 2.0);

        for (var i = 0; i < outputLength; i++)
        {
            var center = (i + 0.5) * ratio;
            var start = Math.Max(0, (int)Math.Floor(center - halfWidth));
            var end = Math.Min(input.Length, (int)Math.Ceiling(center + halfWidth));

            long sum = 0;
            for (var j = start; j < end; j++) sum += input[j];
            output[i] = (short)(sum / Math.Max(1, end - start));
        }

        return output;
    }

    /// <summary>Writes mono 16-bit PCM samples as a canonical 44-byte-header WAV.</summary>
    public static byte[] CreateMonoWav(ReadOnlySpan<short> samples, int sampleRate)
    {
        var dataBytes = samples.Length * 2;
        var wav = new byte[WavHeaderSize + dataBytes];
        var span = wav.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);          // fmt chunk size
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);           // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1);           // mono
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * 2); // byte rate
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], 2);           // block align
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);          // bits per sample
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataBytes);

        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(WavHeaderSize + i * 2)..], samples[i]);
        }

        return wav;
    }
}

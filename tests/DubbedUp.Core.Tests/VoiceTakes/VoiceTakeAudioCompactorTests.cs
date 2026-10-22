using System.Buffers.Binary;
using DubbedUp.Core.VoiceTakes;
using Xunit;

namespace DubbedUp.Core.Tests.VoiceTakes;

public sealed class VoiceTakeAudioCompactorTests
{
    [Fact]
    public void Stereo_48k_take_becomes_mono_24k_and_four_times_smaller()
    {
        var source = CreateWav(sampleRate: 48_000, channels: 2, seconds: 1.0, frequency: 440);

        var compact = VoiceTakeAudioCompactor.ToMonoSpeechWav(source);

        Assert.True(VoiceTakeAudioCompactor.TryReadPcm16(compact, out var rate, out var channels, out var pcm));
        Assert.Equal(24_000, rate);
        Assert.Equal(1, channels);
        Assert.Equal(24_000 * 2, pcm.Length);
        Assert.InRange(compact.Length, source.Length / 4 - 64, source.Length / 4 + 64);
    }

    [Fact]
    public void Resampled_tone_keeps_its_loudness()
    {
        var source = CreateWav(sampleRate: 44_100, channels: 2, seconds: 0.5, frequency: 300, amplitude: 12_000);

        var compact = VoiceTakeAudioCompactor.ToMonoSpeechWav(source);

        Assert.True(VoiceTakeAudioCompactor.TryReadPcm16(compact, out _, out _, out var pcm));
        var peak = 0;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            peak = Math.Max(peak, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(pcm.Span[i..])));
        }

        // A 300 Hz tone is far below the new 12 kHz Nyquist limit, so it must survive the box filter.
        Assert.InRange(peak, 11_000, 12_100);
    }

    [Fact]
    public void Mono_speech_rate_take_is_returned_unchanged()
    {
        var source = CreateWav(sampleRate: 16_000, channels: 1, seconds: 0.25, frequency: 200);

        Assert.Same(source, VoiceTakeAudioCompactor.ToMonoSpeechWav(source));
    }

    [Fact]
    public void Non_wav_payload_is_returned_unchanged()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

        Assert.Same(payload, VoiceTakeAudioCompactor.ToMonoSpeechWav(payload));
    }

    [Fact]
    public void Extra_chunks_before_data_are_skipped()
    {
        var source = CreateWav(sampleRate: 48_000, channels: 2, seconds: 0.1, frequency: 440, extraChunk: true);

        Assert.True(VoiceTakeAudioCompactor.TryReadPcm16(source, out var rate, out var channels, out var pcm));
        Assert.Equal(48_000, rate);
        Assert.Equal(2, channels);
        Assert.Equal(4_800 * 4, pcm.Length);
    }

    private static byte[] CreateWav(int sampleRate, int channels, double seconds, double frequency, short amplitude = 8_000, bool extraChunk = false)
    {
        var frames = (int)(sampleRate * seconds);
        var dataBytes = frames * channels * 2;
        var extra = extraChunk ? 8 + 4 : 0; // "LIST" chunk with 4 bytes of payload
        var wav = new byte[44 + extra + dataBytes];
        var span = wav.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], wav.Length - 8);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);

        var pos = 36;
        if (extraChunk)
        {
            "LIST"u8.CopyTo(span[pos..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[(pos + 4)..], 4);
            pos += 12;
        }

        "data"u8.CopyTo(span[pos..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[(pos + 4)..], dataBytes);
        pos += 8;

        for (var f = 0; f < frames; f++)
        {
            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * frequency * f / sampleRate));
            for (var c = 0; c < channels; c++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(span[pos..], sample);
                pos += 2;
            }
        }

        return wav;
    }
}

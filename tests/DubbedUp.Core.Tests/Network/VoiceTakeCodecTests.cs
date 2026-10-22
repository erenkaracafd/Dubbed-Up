using System.Buffers.Binary;
using System.Text.Json;
using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.Network.VoiceTransport;
using Xunit;

namespace DubbedUp.Core.Tests.Network;

public sealed class VoiceTakeCodecTests
{
    [Fact]
    public void Stereo_48k_take_is_opus_encoded_and_much_smaller()
    {
        var source = CreateStereoWav(48_000, seconds: 2.0, frequency: 220);

        var (payload, codec) = VoiceTakeCodec.EncodeForTransfer(source);

        Assert.Equal(VoiceTakeCodec.Opus, codec);
        // 24 kbps for 2 s is ~6 KB plus framing; the raw capture is ~375 KB.
        Assert.True(payload.Length < source.Length / 30, $"payload {payload.Length} B vs source {source.Length} B");
    }

    [Fact]
    public void Opus_round_trip_returns_mono_48k_wav_with_same_duration_and_signal()
    {
        var source = CreateStereoWav(48_000, seconds: 1.5, frequency: 300, amplitude: 10_000);
        var (payload, codec) = VoiceTakeCodec.EncodeForTransfer(source);

        var wav = VoiceTakeCodec.DecodeToWav(payload, codec);

        Assert.True(VoiceTakeAudioCompactor.TryReadMonoSamples(wav, out var rate, out var samples));
        Assert.Equal(48_000, rate);
        Assert.Equal(72_000, samples.Length); // 1.5 s, trailing padding trimmed

        // Lossy, but a steady tone must keep roughly its level (skip codec warm-up at the start).
        var rms = Math.Sqrt(samples.Skip(9_600).Select(s => (double)s * s).Average());
        Assert.InRange(rms, 10_000 / Math.Sqrt(2) * 0.6, 10_000 / Math.Sqrt(2) * 1.4);
    }

    [Fact]
    public void Wav_codec_and_missing_codec_pass_payload_through()
    {
        var wav = CreateStereoWav(48_000, seconds: 0.1, frequency: 440);

        Assert.Same(wav, VoiceTakeCodec.DecodeToWav(wav, VoiceTakeCodec.Wav));
        Assert.Same(wav, VoiceTakeCodec.DecodeToWav(wav, null));
    }

    [Fact]
    public void Non_wav_input_is_sent_unchanged_as_wav()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

        var (payload, codec) = VoiceTakeCodec.EncodeForTransfer(bytes);

        Assert.Same(bytes, payload);
        Assert.Equal(VoiceTakeCodec.Wav, codec);
    }

    [Fact]
    public void Corrupt_or_unknown_payloads_are_rejected()
    {
        var source = CreateStereoWav(48_000, seconds: 0.5, frequency: 440);
        var (payload, _) = VoiceTakeCodec.EncodeForTransfer(source);

        Assert.Throws<InvalidDataException>(() => VoiceTakeCodec.DecodeToWav(payload[..20], VoiceTakeCodec.Opus));
        Assert.Throws<InvalidDataException>(() => VoiceTakeCodec.DecodeToWav(new byte[] { 0, 1, 2 }, VoiceTakeCodec.Opus));
        Assert.Throws<InvalidDataException>(() => VoiceTakeCodec.DecodeToWav(payload, "mp3"));

        var hugeHeader = (byte[])payload.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(hugeHeader.AsSpan(9), int.MaxValue); // absurd sample count
        Assert.Throws<InvalidDataException>(() => VoiceTakeCodec.DecodeToWav(hugeHeader, VoiceTakeCodec.Opus));
    }

    [Fact]
    public void Manifest_carries_codec_and_defaults_to_wav_for_v1_json()
    {
        var manifest = VoiceTakeTransferManifest.Create("t1", "slot-1", 2, "Ada", new byte[10], codec: VoiceTakeCodec.Opus);
        var roundTrip = JsonSerializer.Deserialize<VoiceTakeTransferManifest>(JsonSerializer.Serialize(manifest));
        Assert.Equal(VoiceTakeCodec.Opus, roundTrip!.Codec);

        const string v1Json = """{"transfer_id":"t1","voice_slot_id":"slot-1","sender_peer_id":2,"sender_name":"Ada","total_bytes":10,"chunk_size":32768,"total_chunks":1,"duration_seconds":0,"sha256":""}""";
        Assert.Equal(VoiceTakeCodec.Wav, JsonSerializer.Deserialize<VoiceTakeTransferManifest>(v1Json)!.Codec);
    }

    private static byte[] CreateStereoWav(int sampleRate, double seconds, double frequency, short amplitude = 8_000)
    {
        var frames = (int)(sampleRate * seconds);
        var mono = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            mono[i] = (short)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
        }

        // Build mono with the shared writer, then interleave into a stereo WAV of the same rate.
        var monoWav = VoiceTakeAudioCompactor.CreateMonoWav(mono, sampleRate);
        var stereo = new byte[44 + frames * 4];
        monoWav.AsSpan(0, 44).CopyTo(stereo);
        BinaryPrimitives.WriteInt32LittleEndian(stereo.AsSpan(4), stereo.Length - 8);
        BinaryPrimitives.WriteInt16LittleEndian(stereo.AsSpan(22), 2);
        BinaryPrimitives.WriteInt32LittleEndian(stereo.AsSpan(28), sampleRate * 4);
        BinaryPrimitives.WriteInt16LittleEndian(stereo.AsSpan(32), 4);
        BinaryPrimitives.WriteInt32LittleEndian(stereo.AsSpan(40), frames * 4);
        for (var i = 0; i < frames; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(stereo.AsSpan(44 + i * 4), mono[i]);
            BinaryPrimitives.WriteInt16LittleEndian(stereo.AsSpan(46 + i * 4), mono[i]);
        }
        return stereo;
    }
}

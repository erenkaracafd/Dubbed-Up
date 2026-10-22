using System.Buffers.Binary;
using Concentus;
using Concentus.Enums;
using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.Network.Protocol;

namespace DubbedUp.Godot.Network.VoiceTransport;

/// <summary>
/// Encodes voice takes for network transfer and turns received payloads back into WAV.
/// Takes are downmixed to mono speech rate, then Opus-encoded (managed Concentus, 24 kbps VoIP):
/// roughly 60x smaller than the raw stereo 48 kHz capture. Receivers always get a mono 16-bit WAV
/// (48 kHz for Opus payloads), so playback and export stay unchanged.
/// </summary>
/// <remarks>
/// Opus payload layout (little endian):
/// "DUOP" | u8 version | i32 sample rate | i32 total samples | u16 frame size | then per frame: u16 length + packet.
/// </remarks>
public static class VoiceTakeCodec
{
    public const string Wav = "wav";
    public const string Opus = "opus";

    private const byte FormatVersion = 1;
    private const int HeaderSize = 4 + 1 + 4 + 4 + 2;
    private const int Bitrate = 24_000;
    private const int MaxPacketBytes = 1275; // Opus maximum packet size
    private const int DecodeSampleRate = 48_000;
    private static readonly byte[] Magic = "DUOP"u8.ToArray();

    static VoiceTakeCodec()
    {
        // Always use the managed codec so every player encodes/decodes identically.
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
    }

    /// <summary>
    /// Returns the smallest payload we can send for a recorded take and the codec it uses.
    /// Falls back to the compacted WAV when the audio can't be Opus-encoded.
    /// </summary>
    public static (byte[] Payload, string Codec) EncodeForTransfer(byte[] wav)
    {
        ArgumentNullException.ThrowIfNull(wav);

        var compact = VoiceTakeAudioCompactor.ToMonoSpeechWav(wav);
        if (!VoiceTakeAudioCompactor.TryReadMonoSamples(compact, out var sampleRate, out var samples)
            || !IsOpusSampleRate(sampleRate)
            || samples.Length == 0)
        {
            return (compact, Wav);
        }

        try
        {
            return (EncodeOpus(samples, sampleRate), Opus);
        }
        catch (Exception)
        {
            return (compact, Wav);
        }
    }

    /// <summary>Converts a received payload into a WAV file; throws <see cref="InvalidDataException"/> if it is malformed.</summary>
    public static byte[] DecodeToWav(byte[] payload, string? codec)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrEmpty(codec) || codec == Wav)
        {
            return payload; // senders without a codec field always sent WAV
        }

        if (codec != Opus)
        {
            throw new InvalidDataException($"Unsupported voice codec '{codec}'.");
        }

        return DecodeOpus(payload);
    }

    public static byte[] EncodeOpus(short[] samples, int sampleRate)
    {
        if (!IsOpusSampleRate(sampleRate))
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Opus needs 8, 12, 16, 24 or 48 kHz.");
        }

        var encoder = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = Bitrate;

        var frameSize = sampleRate / 50; // 20 ms frames
        var frame = new short[frameSize];
        var packet = new byte[MaxPacketBytes];

        using var output = new MemoryStream(HeaderSize + samples.Length / 8);
        Span<byte> header = stackalloc byte[HeaderSize];
        Magic.CopyTo(header);
        header[4] = FormatVersion;
        BinaryPrimitives.WriteInt32LittleEndian(header[5..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[9..], samples.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[13..], (ushort)frameSize);
        output.Write(header);

        Span<byte> lengthPrefix = stackalloc byte[2];
        for (var offset = 0; offset < samples.Length; offset += frameSize)
        {
            var count = Math.Min(frameSize, samples.Length - offset);
            Array.Clear(frame);
            Array.Copy(samples, offset, frame, 0, count); // last frame is zero padded

            var length = encoder.Encode(frame, frameSize, packet, packet.Length);
            if (length <= 0)
            {
                throw new InvalidOperationException($"Opus encoder returned {length}.");
            }

            BinaryPrimitives.WriteUInt16LittleEndian(lengthPrefix, (ushort)length);
            output.Write(lengthPrefix);
            output.Write(packet, 0, length);
        }

        return output.ToArray();
    }

    private static byte[] DecodeOpus(byte[] payload)
    {
        if (payload.Length < HeaderSize || !payload.AsSpan(0, 4).SequenceEqual(Magic) || payload[4] != FormatVersion)
        {
            throw new InvalidDataException("Not a Dubbed Up Opus voice payload.");
        }

        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(5));
        var totalSamples = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(9));
        var frameSize = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(13));
        var maxSamples = (int)(sampleRate * MultiplayerProtocolConstants.MaxVoiceTakeDurationSeconds);

        if (!IsOpusSampleRate(sampleRate) || frameSize != sampleRate / 50 || totalSamples < 0 || totalSamples > maxSamples)
        {
            throw new InvalidDataException("Opus voice payload has an invalid header.");
        }

        // Opus can decode any stream at 48 kHz, and Concentus 2.2.2's managed decoder outputs
        // silence when asked for reduced rates (verified for 24 kHz), so always decode at 48 kHz.
        var scale = DecodeSampleRate / sampleRate;
        var decodeFrameSize = frameSize * scale;
        var outputSamples = totalSamples * scale;

        var decoder = OpusCodecFactory.CreateDecoder(DecodeSampleRate, 1);
        var samples = new short[outputSamples + decodeFrameSize];
        var written = 0;
        var pos = HeaderSize;

        while (pos < payload.Length && written < outputSamples)
        {
            if (pos + 2 > payload.Length)
            {
                throw new InvalidDataException("Opus voice payload is truncated.");
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(pos));
            pos += 2;
            if (length == 0 || length > MaxPacketBytes || pos + length > payload.Length)
            {
                throw new InvalidDataException("Opus voice payload has an invalid packet.");
            }

            var decoded = decoder.Decode(payload.AsSpan(pos, length), samples.AsSpan(written, decodeFrameSize), decodeFrameSize, false);
            if (decoded <= 0)
            {
                throw new InvalidDataException($"Opus decoder returned {decoded}.");
            }

            written += decoded;
            pos += length;
        }

        return VoiceTakeAudioCompactor.CreateMonoWav(samples.AsSpan(0, Math.Min(written, outputSamples)), DecodeSampleRate);
    }

    private static bool IsOpusSampleRate(int sampleRate) => sampleRate is 8_000 or 12_000 or 16_000 or 24_000 or 48_000;
}

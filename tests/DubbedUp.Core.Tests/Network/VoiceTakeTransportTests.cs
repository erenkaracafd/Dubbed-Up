using System.Security.Cryptography;
using DubbedUp.Godot.Network.Protocol;
using DubbedUp.Godot.Network.VoiceTransport;
using Xunit;

namespace DubbedUp.Core.Tests.Network;

public sealed class VoiceTakeTransportTests
{
    [Fact]
    public void Manifest_Creation_Computes_Correct_Chunks_And_Sha256()
    {
        var rawAudio = new byte[70000]; // 70 KB
        Random.Shared.NextBytes(rawAudio);

        var manifest = VoiceTakeTransferManifest.Create(
            "transfer_123",
            "voice_slot_1",
            1001,
            "ActorPlayer",
            rawAudio,
            durationSeconds: 4.5f,
            chunkSize: 32 * 1024); // 32 KB chunks

        Assert.Equal("transfer_123", manifest.TransferId);
        Assert.Equal("voice_slot_1", manifest.VoiceSlotId);
        Assert.Equal(1001, manifest.SenderPeerId);
        Assert.Equal("ActorPlayer", manifest.SenderName);
        Assert.Equal(70000, manifest.TotalBytes);
        Assert.Equal(3, manifest.TotalChunks); // 32KB + 32KB + 6KB = 3 chunks
        Assert.Equal(4.5f, manifest.DurationSeconds);

        var expectedHash = Convert.ToHexString(SHA256.HashData(rawAudio)).ToLowerInvariant();
        Assert.Equal(expectedHash, manifest.Sha256Checksum);
    }

    [Fact]
    public void AssemblySession_Reassembles_Sequential_Chunks_Successfully()
    {
        var rawAudio = new byte[80000];
        Random.Shared.NextBytes(rawAudio);

        var chunkSize = 32 * 1024;
        var manifest = VoiceTakeTransferManifest.Create(
            "transfer_seq",
            "slot_a",
            10,
            "Host",
            rawAudio,
            chunkSize: chunkSize);

        var session = new VoiceTakeAssemblySession(manifest);
        Assert.False(session.IsComplete);

        for (var i = 0; i < manifest.TotalChunks; i++)
        {
            var offset = i * chunkSize;
            var length = Math.Min(chunkSize, manifest.TotalBytes - offset);
            var chunkData = new byte[length];
            Buffer.BlockCopy(rawAudio, offset, chunkData, 0, length);

            var added = session.TryAddChunk(i, chunkData, out var addError);
            Assert.True(added, addError);
        }

        Assert.True(session.IsComplete);
        Assert.Equal(1f, session.ProgressFraction);

        var verified = session.TryGetVerifiedAudio(out var audio, out var verifyError);
        Assert.True(verified, verifyError);
        Assert.NotNull(audio);
        Assert.Equal(rawAudio, audio);
    }

    [Fact]
    public void AssemblySession_Reassembles_Out_Of_Order_Chunks_Successfully()
    {
        var rawAudio = new byte[100000];
        Random.Shared.NextBytes(rawAudio);

        var chunkSize = 32 * 1024;
        var manifest = VoiceTakeTransferManifest.Create(
            "transfer_ooo",
            "slot_b",
            20,
            "Guest",
            rawAudio,
            chunkSize: chunkSize);

        var session = new VoiceTakeAssemblySession(manifest);

        var chunkIndices = Enumerable.Range(0, manifest.TotalChunks).Reverse().ToList();

        foreach (var i in chunkIndices)
        {
            var offset = i * chunkSize;
            var length = Math.Min(chunkSize, manifest.TotalBytes - offset);
            var chunkData = new byte[length];
            Buffer.BlockCopy(rawAudio, offset, chunkData, 0, length);

            var added = session.TryAddChunk(i, chunkData, out var addError);
            Assert.True(added, addError);
        }

        Assert.True(session.IsComplete);
        var verified = session.TryGetVerifiedAudio(out var audio, out var verifyError);
        Assert.True(verified, verifyError);
        Assert.Equal(rawAudio, audio);
    }

    [Fact]
    public void AssemblySession_Handles_Duplicate_Chunks_Idempotently()
    {
        var rawAudio = new byte[10000];
        Random.Shared.NextBytes(rawAudio);

        var manifest = VoiceTakeTransferManifest.Create("transfer_dup", "slot_c", 30, "Player", rawAudio);
        var session = new VoiceTakeAssemblySession(manifest);

        // Add chunk 0 once
        Assert.True(session.TryAddChunk(0, rawAudio, out var error1), error1);
        Assert.Equal(1, session.ChunksReceived);

        // Add chunk 0 again (duplicate)
        Assert.True(session.TryAddChunk(0, rawAudio, out var error2), error2);
        Assert.Equal(1, session.ChunksReceived); // Must not double count

        Assert.True(session.IsComplete);
        Assert.True(session.TryGetVerifiedAudio(out var audio, out _));
        Assert.Equal(rawAudio, audio);
    }

    [Fact]
    public void AssemblySession_Detects_Corrupt_Chunk_Data_Via_Sha256()
    {
        var rawAudio = new byte[20000];
        Random.Shared.NextBytes(rawAudio);

        var manifest = VoiceTakeTransferManifest.Create("transfer_corrupt", "slot_d", 40, "Player", rawAudio);
        var session = new VoiceTakeAssemblySession(manifest);

        // Corrupt one byte before adding
        var corruptData = (byte[])rawAudio.Clone();
        corruptData[5] = (byte)(corruptData[5] ^ 0xFF);

        Assert.True(session.TryAddChunk(0, corruptData, out _));
        Assert.True(session.IsComplete);

        var verified = session.TryGetVerifiedAudio(out var audio, out var verifyError);
        Assert.False(verified);
        Assert.Null(audio);
        Assert.Contains("Checksum mismatch", verifyError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssemblySession_Rejects_Out_Of_Bounds_Or_Invalid_Length_Chunks()
    {
        var rawAudio = new byte[32000];
        var manifest = VoiceTakeTransferManifest.Create("transfer_bounds", "slot_e", 50, "Player", rawAudio, chunkSize: 16000);
        var session = new VoiceTakeAssemblySession(manifest);

        // Negative chunk index
        Assert.False(session.TryAddChunk(-1, new byte[16000], out var err1));
        Assert.Contains("out of bounds", err1, StringComparison.OrdinalIgnoreCase);

        // Chunk index exceeding total chunks
        Assert.False(session.TryAddChunk(5, new byte[16000], out var err2));
        Assert.Contains("out of bounds", err2, StringComparison.OrdinalIgnoreCase);

        // Incorrect chunk length
        Assert.False(session.TryAddChunk(0, new byte[8000], out var err3));
        Assert.Contains("does not match expected length", err3, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssemblySession_Rejects_Oversized_Manifest()
    {
        var oversizedManifest = new VoiceTakeTransferManifest
        {
            TransferId = "bad_size",
            TotalBytes = MultiplayerProtocolConstants.MaxVoiceTakeBytes + 1000,
            ChunkSize = 32 * 1024,
            TotalChunks = 500,
        };

        var ex = Assert.Throws<ArgumentException>(() => new VoiceTakeAssemblySession(oversizedManifest));
        Assert.Contains("exceeds maximum allowed size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

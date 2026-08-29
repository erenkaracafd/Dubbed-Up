using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace DubbedUp.Godot.Network.VoiceTransport;

/// <summary>
/// Manages the state, buffer assembly, out-of-order chunk indexing, and cryptographic
/// integrity verification for an incoming voice take transfer.
/// </summary>
public sealed class VoiceTakeAssemblySession
{
    private readonly HashSet<int> _receivedChunkIndices = [];
    private readonly byte[] _buffer;
    private readonly DateTime _createdAtUtc = DateTime.UtcNow;

    public VoiceTakeTransferManifest Manifest { get; }

    public int ChunksReceived => _receivedChunkIndices.Count;

    public int TotalChunks => Manifest.TotalChunks;

    public float ProgressFraction => TotalChunks == 0 ? 1f : (float)ChunksReceived / TotalChunks;

    public bool IsComplete => ChunksReceived == TotalChunks;

    public DateTime CreatedAtUtc => _createdAtUtc;

    public VoiceTakeAssemblySession(VoiceTakeTransferManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.TotalBytes < 0 || manifest.TotalBytes > Protocol.MultiplayerProtocolConstants.MaxVoiceTakeBytes)
        {
            throw new ArgumentException($"Total bytes {manifest.TotalBytes} exceeds maximum allowed size of {Protocol.MultiplayerProtocolConstants.MaxVoiceTakeBytes}.", nameof(manifest));
        }

        if (manifest.ChunkSize <= 0)
        {
            throw new ArgumentException("Chunk size must be positive.", nameof(manifest));
        }

        Manifest = manifest;
        _buffer = new byte[manifest.TotalBytes];
    }

    public bool TryAddChunk(
        int chunkIndex,
        byte[] chunkData,
        [NotNullWhen(false)] out string? error)
    {
        if (chunkData is null)
        {
            error = "Chunk data is null.";
            return false;
        }

        if (chunkIndex < 0 || chunkIndex >= Manifest.TotalChunks)
        {
            error = $"Chunk index {chunkIndex} is out of bounds [0, {Manifest.TotalChunks}).";
            return false;
        }

        // Calculate expected chunk length
        var expectedOffset = chunkIndex * Manifest.ChunkSize;
        var expectedLength = Math.Min(Manifest.ChunkSize, Manifest.TotalBytes - expectedOffset);

        if (chunkData.Length != expectedLength)
        {
            error = $"Chunk length {chunkData.Length} does not match expected length {expectedLength} for chunk {chunkIndex}.";
            return false;
        }

        // Idempotent duplicate check
        if (_receivedChunkIndices.Contains(chunkIndex))
        {
            error = null;
            return true; // Already processed
        }

        Buffer.BlockCopy(chunkData, 0, _buffer, expectedOffset, expectedLength);
        _receivedChunkIndices.Add(chunkIndex);

        error = null;
        return true;
    }

    public bool TryGetVerifiedAudio(
        [NotNullWhen(true)] out byte[]? audioData,
        [NotNullWhen(false)] out string? error)
    {
        audioData = null;

        if (!IsComplete)
        {
            error = $"Transfer is incomplete ({ChunksReceived}/{TotalChunks} chunks received).";
            return false;
        }

        var calculatedChecksum = Convert.ToHexString(SHA256.HashData(_buffer)).ToLowerInvariant();
        if (!string.Equals(calculatedChecksum, Manifest.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Checksum mismatch: expected {Manifest.Sha256Checksum}, calculated {calculatedChecksum}. Payload is corrupt.";
            return false;
        }

        audioData = _buffer;
        error = null;
        return true;
    }
}

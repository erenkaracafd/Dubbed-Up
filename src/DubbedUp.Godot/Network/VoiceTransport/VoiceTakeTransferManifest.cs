using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace DubbedUp.Godot.Network.VoiceTransport;

/// <summary>
/// Defines the complete metadata and SHA-256 integrity manifest for a chunked voice take transfer.
/// </summary>
public sealed record VoiceTakeTransferManifest
{
    public const int DefaultChunkSizeBytes = 32 * 1024; // 32 KB chunks

    [JsonPropertyName("transfer_id")]
    public string TransferId { get; init; } = string.Empty;

    [JsonPropertyName("voice_slot_id")]
    public string VoiceSlotId { get; init; } = string.Empty;

    [JsonPropertyName("sender_peer_id")]
    public long SenderPeerId { get; init; }

    [JsonPropertyName("sender_name")]
    public string SenderName { get; init; } = string.Empty;

    [JsonPropertyName("total_bytes")]
    public int TotalBytes { get; init; }

    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; init; } = DefaultChunkSizeBytes;

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; init; }

    [JsonPropertyName("duration_seconds")]
    public float DurationSeconds { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256Checksum { get; init; } = string.Empty;

    public static VoiceTakeTransferManifest Create(
        string transferId,
        string voiceSlotId,
        long senderPeerId,
        string senderName,
        byte[] audioData,
        float durationSeconds = 0f,
        int chunkSize = DefaultChunkSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(audioData);

        var totalBytes = audioData.Length;
        var totalChunks = totalBytes == 0 ? 0 : (int)Math.Ceiling((double)totalBytes / chunkSize);
        var checksum = Convert.ToHexString(SHA256.HashData(audioData)).ToLowerInvariant();

        return new VoiceTakeTransferManifest
        {
            TransferId = string.IsNullOrWhiteSpace(transferId) ? Guid.NewGuid().ToString("N") : transferId,
            VoiceSlotId = voiceSlotId,
            SenderPeerId = senderPeerId,
            SenderName = senderName,
            TotalBytes = totalBytes,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks,
            DurationSeconds = durationSeconds,
            Sha256Checksum = checksum,
        };
    }
}

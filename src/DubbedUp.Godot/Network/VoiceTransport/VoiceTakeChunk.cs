using System.Text.Json.Serialization;

namespace DubbedUp.Godot.Network.VoiceTransport;

/// <summary>
/// Represents a single bounded binary chunk of a voice take transfer.
/// </summary>
public sealed record VoiceTakeChunk
{
    [JsonPropertyName("transfer_id")]
    public string TransferId { get; init; } = string.Empty;

    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; init; }

    [JsonPropertyName("data")]
    public byte[] Data { get; init; } = [];

    [JsonPropertyName("data_length")]
    public int DataLength => Data.Length;
}

using System.Text.Json.Serialization;

namespace DubbedUp.Godot.Network.Sync;

/// <summary>
/// Host-authoritative command specifying the exact target host-timestamp when
/// synchronized playback should commence on all clients.
/// </summary>
public sealed record ScheduledPlaybackCommand
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("round_number")]
    public int RoundNumber { get; init; }

    [JsonPropertyName("scene_id")]
    public string SceneId { get; init; } = string.Empty;

    [JsonPropertyName("idempotency_token")]
    public string IdempotencyToken { get; init; } = string.Empty;

    [JsonPropertyName("scheduled_host_time_ms")]
    public long ScheduledHostTimeMs { get; init; }

    [JsonPropertyName("playback_speed")]
    public float PlaybackSpeed { get; init; } = 1.0f;
}

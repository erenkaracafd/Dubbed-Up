using System.Text.Json.Serialization;

namespace DubbedUp.Godot.Network.Protocol;

public enum MultiplayerMessageType
{
    HandshakeRequest = 1,
    HandshakeResponse = 2,
    SceneSelected = 3,
    CharacterClaimRequest = 4,
    CharacterClaimResponse = 5,
    PlayerReadiness = 6,
    PhaseChange = 7,
    VoiceTakeHeader = 8,
    PlaybackSync = 9,
    VoteSubmit = 10,
}

public enum MultiplayerPhase
{
    Lobby = 0,
    SceneSelection = 1,
    CharacterSelect = 2,
    Recording = 3,
    Playback = 4,
    Voting = 5,
    Results = 6,
}

public sealed record MultiplayerEnvelope(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("t")] MultiplayerMessageType Type,
    [property: JsonPropertyName("seq")] long SequenceNumber,
    [property: JsonPropertyName("p")] string Payload);

public sealed record HandshakeRequestMessage(
    [property: JsonPropertyName("client_version")] int ClientProtocolVersion,
    [property: JsonPropertyName("player_name")] string PlayerName);

public sealed record HandshakeResponseMessage(
    [property: JsonPropertyName("accepted")] bool Accepted,
    [property: JsonPropertyName("server_version")] int ServerProtocolVersion,
    [property: JsonPropertyName("peer_id")] long AssignedPeerId,
    [property: JsonPropertyName("is_host")] bool IsHost,
    [property: JsonPropertyName("reject_reason")] string RejectReason);

public sealed record SceneSelectedMessage(
    [property: JsonPropertyName("scene_id")] string SceneId,
    [property: JsonPropertyName("checksum")] string SceneChecksumSha256,
    [property: JsonPropertyName("title")] string Title);

public sealed record CharacterClaimRequestMessage(
    [property: JsonPropertyName("voice_slot_id")] string VoiceSlotId,
    [property: JsonPropertyName("character_id")] string CharacterId);

public sealed record CharacterClaimResponseMessage(
    [property: JsonPropertyName("voice_slot_id")] string VoiceSlotId,
    [property: JsonPropertyName("character_id")] string CharacterId,
    [property: JsonPropertyName("claiming_peer_id")] long ClaimingPeerId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("reject_reason")] string RejectReason);

public sealed record PlayerReadinessMessage(
    [property: JsonPropertyName("peer_id")] long PeerId,
    [property: JsonPropertyName("is_ready")] bool IsReady);

public sealed record PhaseChangeMessage(
    [property: JsonPropertyName("target_phase")] MultiplayerPhase TargetPhase,
    [property: JsonPropertyName("sequence_number")] long SequenceNumber,
    [property: JsonPropertyName("timestamp_utc_ms")] long TimestampUtcMs);

public sealed record VoiceTakeHeaderMessage(
    [property: JsonPropertyName("voice_slot_id")] string VoiceSlotId,
    [property: JsonPropertyName("sender_peer_id")] long SenderPeerId,
    [property: JsonPropertyName("duration_sec")] float DurationSeconds,
    [property: JsonPropertyName("byte_length")] int ByteLength,
    [property: JsonPropertyName("sha256")] string Sha256Checksum);

public sealed record PlaybackSyncMessage(
    [property: JsonPropertyName("scheduled_host_time_ms")] long ScheduledStartHostTimeMs,
    [property: JsonPropertyName("scene_id")] string SceneId,
    [property: JsonPropertyName("playback_rate")] float PlaybackRate);

public sealed record VoteSubmitMessage(
    [property: JsonPropertyName("target_voice_slot_id")] string TargetVoiceSlotId,
    [property: JsonPropertyName("score")] int Score);

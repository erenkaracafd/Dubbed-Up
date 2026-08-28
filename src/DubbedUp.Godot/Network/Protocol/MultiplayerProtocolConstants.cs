namespace DubbedUp.Godot.Network.Protocol;

/// <summary>
/// Defines versioning and strict size constraints for all multiplayer protocol messages.
/// Enforces bounded allocations and the zero-media invariant across the wire.
/// </summary>
public static class MultiplayerProtocolConstants
{
    public const int CurrentProtocolVersion = 1;

    public const int MinProtocolVersion = 1;

    public const int MaxPlayerNameLength = 32;

    public const int MaxSceneIdLength = 64;

    public const int MaxSceneTitleLength = 128;

    public const int MaxCharacterIdLength = 64;

    public const int MaxVoiceSlotIdLength = 64;

    public const int MaxChecksumLength = 64;

    public const int MaxRejectReasonLength = 128;

    public const int MaxJsonPayloadLength = 64 * 1024; // 64 KB max for control message envelopes

    public const int MaxVoiceTakeBytes = 10 * 1024 * 1024; // 10 MB bound for 16-bit PCM takes

    public const float MaxVoiceTakeDurationSeconds = 120.0f;
}

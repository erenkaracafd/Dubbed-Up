using System.Diagnostics.CodeAnalysis;

namespace DubbedUp.Godot.Network.Protocol;

/// <summary>
/// Validates constraints, bounds, and authority rules on received protocol messages.
/// </summary>
public static class MultiplayerMessageValidator
{
    public static bool ValidateHandshakeRequest(
        HandshakeRequestMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Handshake request is null.";
            return false;
        }

        if (message.ClientProtocolVersion < MultiplayerProtocolConstants.MinProtocolVersion ||
            message.ClientProtocolVersion > MultiplayerProtocolConstants.CurrentProtocolVersion)
        {
            error = $"Client protocol version {message.ClientProtocolVersion} is incompatible with server version {MultiplayerProtocolConstants.CurrentProtocolVersion}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.PlayerName))
        {
            error = "Player name cannot be empty.";
            return false;
        }

        if (message.PlayerName.Length > MultiplayerProtocolConstants.MaxPlayerNameLength)
        {
            error = $"Player name exceeds maximum length of {MultiplayerProtocolConstants.MaxPlayerNameLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidateHandshakeResponse(
        HandshakeResponseMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Handshake response is null.";
            return false;
        }

        if (message.ServerProtocolVersion < MultiplayerProtocolConstants.MinProtocolVersion ||
            message.ServerProtocolVersion > MultiplayerProtocolConstants.CurrentProtocolVersion)
        {
            error = $"Server protocol version {message.ServerProtocolVersion} is incompatible.";
            return false;
        }

        if (message.RejectReason.Length > MultiplayerProtocolConstants.MaxRejectReasonLength)
        {
            error = $"Reject reason exceeds maximum length of {MultiplayerProtocolConstants.MaxRejectReasonLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidateSceneSelected(
        SceneSelectedMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Scene selected message is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.SceneId) || message.SceneId.Length > MultiplayerProtocolConstants.MaxSceneIdLength)
        {
            error = $"Scene ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxSceneIdLength}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.SceneChecksumSha256) || message.SceneChecksumSha256.Length > MultiplayerProtocolConstants.MaxChecksumLength)
        {
            error = $"Scene checksum is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxChecksumLength}.";
            return false;
        }

        if (message.Title.Length > MultiplayerProtocolConstants.MaxSceneTitleLength)
        {
            error = $"Scene title exceeds maximum length of {MultiplayerProtocolConstants.MaxSceneTitleLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidateCharacterClaimRequest(
        CharacterClaimRequestMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Character claim request is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.VoiceSlotId) || message.VoiceSlotId.Length > MultiplayerProtocolConstants.MaxVoiceSlotIdLength)
        {
            error = $"Voice slot ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxVoiceSlotIdLength}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.CharacterId) || message.CharacterId.Length > MultiplayerProtocolConstants.MaxCharacterIdLength)
        {
            error = $"Character ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxCharacterIdLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidateCharacterClaimResponse(
        CharacterClaimResponseMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Character claim response is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.VoiceSlotId) || message.VoiceSlotId.Length > MultiplayerProtocolConstants.MaxVoiceSlotIdLength)
        {
            error = $"Voice slot ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxVoiceSlotIdLength}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.CharacterId) || message.CharacterId.Length > MultiplayerProtocolConstants.MaxCharacterIdLength)
        {
            error = $"Character ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxCharacterIdLength}.";
            return false;
        }

        if (message.RejectReason.Length > MultiplayerProtocolConstants.MaxRejectReasonLength)
        {
            error = $"Reject reason exceeds maximum length of {MultiplayerProtocolConstants.MaxRejectReasonLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidatePlayerReadiness(
        PlayerReadinessMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Player readiness message is null.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidatePhaseChange(
        PhaseChangeMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Phase change message is null.";
            return false;
        }

        if (!Enum.IsDefined(typeof(MultiplayerPhase), message.TargetPhase))
        {
            error = $"Invalid target phase: {message.TargetPhase}.";
            return false;
        }

        if (message.SequenceNumber <= 0)
        {
            error = "Sequence number must be positive.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidateVoiceTakeHeader(
        VoiceTakeHeaderMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Voice take header message is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.VoiceSlotId) || message.VoiceSlotId.Length > MultiplayerProtocolConstants.MaxVoiceSlotIdLength)
        {
            error = $"Voice slot ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxVoiceSlotIdLength}.";
            return false;
        }

        if (message.DurationSeconds is <= 0 or > MultiplayerProtocolConstants.MaxVoiceTakeDurationSeconds)
        {
            error = $"Duration {message.DurationSeconds}s exceeds allowed range (0, {MultiplayerProtocolConstants.MaxVoiceTakeDurationSeconds}s].";
            return false;
        }

        if (message.ByteLength is <= 0 or > MultiplayerProtocolConstants.MaxVoiceTakeBytes)
        {
            error = $"Byte length {message.ByteLength} exceeds allowed range (0, {MultiplayerProtocolConstants.MaxVoiceTakeBytes} bytes].";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Sha256Checksum) || message.Sha256Checksum.Length > MultiplayerProtocolConstants.MaxChecksumLength)
        {
            error = $"Sha256 checksum is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxChecksumLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidatePlaybackSync(
        PlaybackSyncMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Playback sync message is null.";
            return false;
        }

        if (message.ScheduledStartHostTimeMs <= 0)
        {
            error = "Scheduled host start time must be positive.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.SceneId) || message.SceneId.Length > MultiplayerProtocolConstants.MaxSceneIdLength)
        {
            error = $"Scene ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxSceneIdLength}.";
            return false;
        }

        if (message.PlaybackRate <= 0)
        {
            error = "Playback rate must be positive.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool ValidateVoteSubmit(
        VoteSubmitMessage? message,
        [NotNullWhen(false)] out string? error)
    {
        if (message is null)
        {
            error = "Vote submit message is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.TargetVoiceSlotId) || message.TargetVoiceSlotId.Length > MultiplayerProtocolConstants.MaxVoiceSlotIdLength)
        {
            error = $"Target voice slot ID is invalid or exceeds maximum length of {MultiplayerProtocolConstants.MaxVoiceSlotIdLength}.";
            return false;
        }

        if (message.Score is < 0 or > 100)
        {
            error = $"Score {message.Score} is outside the valid range [0, 100].";
            return false;
        }

        error = null;
        return true;
    }
}

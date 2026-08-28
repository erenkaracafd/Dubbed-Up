using DubbedUp.Godot.Network.Protocol;
using Xunit;

namespace DubbedUp.Core.Tests.Network;

public sealed class MultiplayerProtocolTests
{
    [Fact]
    public void HandshakeRequest_Serialization_And_Validation_RoundTrip_Succeeds()
    {
        var msg = new HandshakeRequestMessage(1, "PlayerOne");
        var json = MultiplayerMessageSerializer.SerializeEnvelope(MultiplayerMessageType.HandshakeRequest, msg);

        var successEnvelope = MultiplayerMessageSerializer.TryDeserializeEnvelope(json, out var envelope, out var envError);
        Assert.True(successEnvelope, envError);
        Assert.NotNull(envelope);
        Assert.Equal(MultiplayerMessageType.HandshakeRequest, envelope.Type);
        Assert.Equal(1, envelope.Version);

        var successPayload = MultiplayerMessageSerializer.TryDeserializePayload<HandshakeRequestMessage>(envelope.Payload, out var deserialized, out var payloadError);
        Assert.True(successPayload, payloadError);
        Assert.NotNull(deserialized);
        Assert.Equal("PlayerOne", deserialized.PlayerName);
        Assert.Equal(1, deserialized.ClientProtocolVersion);

        var isValid = MultiplayerMessageValidator.ValidateHandshakeRequest(deserialized, out var valError);
        Assert.True(isValid, valError);
    }

    [Fact]
    public void HandshakeRequest_Rejects_Incompatible_Version_And_Oversized_Name()
    {
        var incompatibleMsg = new HandshakeRequestMessage(999, "ValidName");
        Assert.False(MultiplayerMessageValidator.ValidateHandshakeRequest(incompatibleMsg, out var verError));
        Assert.Contains("incompatible", verError, StringComparison.OrdinalIgnoreCase);

        var oversizedMsg = new HandshakeRequestMessage(1, new string('A', MultiplayerProtocolConstants.MaxPlayerNameLength + 1));
        Assert.False(MultiplayerMessageValidator.ValidateHandshakeRequest(oversizedMsg, out var nameError));
        Assert.Contains("exceeds maximum length", nameError, StringComparison.OrdinalIgnoreCase);

        var emptyNameMsg = new HandshakeRequestMessage(1, "   ");
        Assert.False(MultiplayerMessageValidator.ValidateHandshakeRequest(emptyNameMsg, out var emptyError));
        Assert.Contains("cannot be empty", emptyError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SceneSelected_Enforces_ZeroMedia_And_Checksum_Limits()
    {
        var msg = new SceneSelectedMessage("museum_mixup", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "Museum Mix-up");
        var json = MultiplayerMessageSerializer.SerializeEnvelope(MultiplayerMessageType.SceneSelected, msg);

        Assert.True(MultiplayerMessageSerializer.TryDeserializeEnvelope(json, out var envelope, out _));
        Assert.True(MultiplayerMessageSerializer.TryDeserializePayload<SceneSelectedMessage>(envelope!.Payload, out var deserialized, out _));
        Assert.True(MultiplayerMessageValidator.ValidateSceneSelected(deserialized, out _));

        // Invalid checksum length
        var invalidChecksum = new SceneSelectedMessage("museum_mixup", new string('X', 100), "Title");
        Assert.False(MultiplayerMessageValidator.ValidateSceneSelected(invalidChecksum, out var checksumError));
        Assert.Contains("checksum", checksumError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CharacterClaim_Validates_Ids_And_Response_Fields()
    {
        var request = new CharacterClaimRequestMessage("slot_01", "curator");
        Assert.True(MultiplayerMessageValidator.ValidateCharacterClaimRequest(request, out _));

        var invalidRequest = new CharacterClaimRequestMessage("", "curator");
        Assert.False(MultiplayerMessageValidator.ValidateCharacterClaimRequest(invalidRequest, out _));

        var response = new CharacterClaimResponseMessage("slot_01", "curator", 12345, true, string.Empty);
        Assert.True(MultiplayerMessageValidator.ValidateCharacterClaimResponse(response, out _));

        var rejectedResponse = new CharacterClaimResponseMessage("slot_01", "curator", 0, false, "Slot already claimed");
        Assert.True(MultiplayerMessageValidator.ValidateCharacterClaimResponse(rejectedResponse, out _));
    }

    [Fact]
    public void VoiceTakeHeader_Enforces_Size_And_Duration_Bounds()
    {
        var validHeader = new VoiceTakeHeaderMessage(
            "slot_01",
            12345,
            DurationSeconds: 12.5f,
            ByteLength: 2 * 1024 * 1024,
            Sha256Checksum: "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");

        Assert.True(MultiplayerMessageValidator.ValidateVoiceTakeHeader(validHeader, out _));

        var oversizedPayload = validHeader with { ByteLength = MultiplayerProtocolConstants.MaxVoiceTakeBytes + 100 };
        Assert.False(MultiplayerMessageValidator.ValidateVoiceTakeHeader(oversizedPayload, out var sizeError));
        Assert.Contains("Byte length", sizeError, StringComparison.OrdinalIgnoreCase);

        var excessiveDuration = validHeader with { DurationSeconds = MultiplayerProtocolConstants.MaxVoiceTakeDurationSeconds + 1.0f };
        Assert.False(MultiplayerMessageValidator.ValidateVoiceTakeHeader(excessiveDuration, out var durError));
        Assert.Contains("Duration", durError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlaybackSync_And_VoteSubmit_Validate_Expected_Bounds()
    {
        var sync = new PlaybackSyncMessage(1700000000000, "space_drift", 1.0f);
        Assert.True(MultiplayerMessageValidator.ValidatePlaybackSync(sync, out _));

        var invalidSync = sync with { PlaybackRate = -1.0f };
        Assert.False(MultiplayerMessageValidator.ValidatePlaybackSync(invalidSync, out _));

        var vote = new VoteSubmitMessage("slot_01", 85);
        Assert.True(MultiplayerMessageValidator.ValidateVoteSubmit(vote, out _));

        var invalidVote = vote with { Score = 150 };
        Assert.False(MultiplayerMessageValidator.ValidateVoteSubmit(invalidVote, out _));
    }

    [Fact]
    public void SequenceTracker_Rejects_Stale_And_Out_Of_Order_Commands()
    {
        var tracker = new MultiplayerSequenceTracker();

        Assert.True(tracker.TryProcessCommand(1));
        Assert.Equal(1, tracker.LastProcessedSequenceNumber);

        // Duplicate command (same sequence number)
        Assert.False(tracker.TryProcessCommand(1));

        // Out-of-order (older sequence number)
        Assert.False(tracker.TryProcessCommand(0));

        // Monotonically increasing command
        Assert.True(tracker.TryProcessCommand(2));
        Assert.Equal(2, tracker.LastProcessedSequenceNumber);

        Assert.True(tracker.TryProcessCommand(10));
        Assert.Equal(10, tracker.LastProcessedSequenceNumber);
        Assert.False(tracker.TryProcessCommand(5));
    }

    [Fact]
    public void Deserializer_Rejects_Malformed_JSON_And_Oversized_Envelopes()
    {
        var malformedJson = "{ \"v\": 1, \"t\": 1, \"p\": ";
        Assert.False(MultiplayerMessageSerializer.TryDeserializeEnvelope(malformedJson, out _, out var jsonError));
        Assert.Contains("Malformed", jsonError, StringComparison.OrdinalIgnoreCase);

        var oversizedPayload = new string('x', MultiplayerProtocolConstants.MaxJsonPayloadLength + 10);
        Assert.False(MultiplayerMessageSerializer.TryDeserializeEnvelope(oversizedPayload, out _, out var lenError));
        Assert.Contains("exceeds maximum allowed size", lenError, StringComparison.OrdinalIgnoreCase);
    }
}

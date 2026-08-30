using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace DubbedUp.Godot.Network.Protocol;

/// <summary>
/// Handles bounded, secure serialization and deserialization of protocol messages.
/// Enforces version compatibility and maximum payload limits.
/// </summary>
public static class MultiplayerMessageSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static string SerializeEnvelope<T>(
        MultiplayerMessageType type,
        T message,
        long sequenceNumber = 0,
        int version = MultiplayerProtocolConstants.CurrentProtocolVersion)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payloadJson = JsonSerializer.Serialize(message, JsonOptions);
        var envelope = new MultiplayerEnvelope(version, type, sequenceNumber, payloadJson);
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public static bool TryDeserializeEnvelope(
        string? json,
        [NotNullWhen(true)] out MultiplayerEnvelope? envelope,
        [NotNullWhen(false)] out string? error)
    {
        envelope = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Message envelope is empty.";
            return false;
        }

        if (json.Length > MultiplayerProtocolConstants.MaxJsonPayloadLength)
        {
            error = $"Message envelope exceeds maximum allowed size of {MultiplayerProtocolConstants.MaxJsonPayloadLength} bytes.";
            return false;
        }

        try
        {
            envelope = JsonSerializer.Deserialize<MultiplayerEnvelope>(json, JsonOptions);
            if (envelope is null)
            {
                error = "Deserialized envelope was null.";
                return false;
            }

            if (envelope.Version < MultiplayerProtocolConstants.MinProtocolVersion ||
                envelope.Version > MultiplayerProtocolConstants.CurrentProtocolVersion)
            {
                error = $"Unsupported protocol version {envelope.Version}. Expected version {MultiplayerProtocolConstants.CurrentProtocolVersion}.";
                envelope = null;
                return false;
            }

            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Malformed message envelope JSON: {ex.Message}";
            envelope = null;
            return false;
        }
    }

    public static bool TryDeserializePayload<T>(
        string? payloadJson,
        [NotNullWhen(true)] out T? message,
        [NotNullWhen(false)] out string? error)
    {
        message = default;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            error = "Message payload is empty.";
            return false;
        }

        if (payloadJson.Length > MultiplayerProtocolConstants.MaxJsonPayloadLength)
        {
            error = $"Message payload exceeds maximum allowed size of {MultiplayerProtocolConstants.MaxJsonPayloadLength} bytes.";
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);
            if (message is null)
            {
                error = "Deserialized message payload was null.";
                return false;
            }

            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Malformed message payload JSON: {ex.Message}";
            message = default;
            return false;
        }
    }
}

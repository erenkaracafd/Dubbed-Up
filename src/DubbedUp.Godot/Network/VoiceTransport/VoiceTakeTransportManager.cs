using Godot;
using System.Text.Json;

namespace DubbedUp.Godot.Network.VoiceTransport;

/// <summary>
/// Orchestrates chunked, bounded voice take transfers across connected peers using Godot RPCs.
/// Supports both ENet and Steam multiplayer transports seamlessly.
/// </summary>
public partial class VoiceTakeTransportManager : Node
{
    [Signal]
    public delegate void TransferStartedEventHandler(string transferId, string voiceSlotId, long senderPeerId, int totalBytes);

    [Signal]
    public delegate void TransferProgressEventHandler(string transferId, float progressFraction);

    [Signal]
    public delegate void TransferCompletedEventHandler(string transferId, string voiceSlotId, long senderPeerId, string senderName, byte[] audioData);

    [Signal]
    public delegate void TransferFailedEventHandler(string transferId, string voiceSlotId, string reason);

    private readonly Dictionary<string, VoiceTakeAssemblySession> _activeTransfers = [];
    private readonly object _lock = new();

    public void SendVoiceTake(
        string voiceSlotId,
        string senderName,
        byte[] audioData,
        float durationSeconds = 0f,
        int chunkSize = VoiceTakeTransferManifest.DefaultChunkSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(audioData);

        var senderPeerId = Multiplayer.GetUniqueId();
        var manifest = VoiceTakeTransferManifest.Create(
            Guid.NewGuid().ToString("N"),
            voiceSlotId,
            senderPeerId,
            senderName,
            audioData,
            durationSeconds,
            chunkSize);

        var manifestJson = JsonSerializer.Serialize(manifest);

        // 1. Announce transfer manifest to all peers
        Rpc(nameof(RpcAnnounceTransfer), manifestJson);

        // 2. Transmit chunks sequentially
        for (var i = 0; i < manifest.TotalChunks; i++)
        {
            var offset = i * manifest.ChunkSize;
            var length = Math.Min(manifest.ChunkSize, manifest.TotalBytes - offset);
            var chunkData = new byte[length];
            Buffer.BlockCopy(audioData, offset, chunkData, 0, length);

            Rpc(nameof(RpcReceiveChunk), manifest.TransferId, i, chunkData);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcAnnounceTransfer(string manifestJson)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<VoiceTakeTransferManifest>(manifestJson);
            if (manifest is null)
            {
                GD.PrintErr("[VoiceTransport] Received invalid or null transfer manifest.");
                return;
            }

            lock (_lock)
            {
                _activeTransfers[manifest.TransferId] = new VoiceTakeAssemblySession(manifest);
            }

            EmitSignal(SignalName.TransferStarted, manifest.TransferId, manifest.VoiceSlotId, manifest.SenderPeerId, manifest.TotalBytes);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VoiceTransport] Failed to deserialize manifest: {ex.Message}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcReceiveChunk(string transferId, int chunkIndex, byte[] chunkData)
    {
        VoiceTakeAssemblySession? session;
        lock (_lock)
        {
            if (!_activeTransfers.TryGetValue(transferId, out session))
            {
                GD.PrintErr($"[VoiceTransport] Received chunk for unknown transfer {transferId}.");
                return;
            }
        }

        if (!session.TryAddChunk(chunkIndex, chunkData, out var addError))
        {
            GD.PrintErr($"[VoiceTransport] Error adding chunk {chunkIndex} to {transferId}: {addError}");
            EmitSignal(SignalName.TransferFailed, transferId, session.Manifest.VoiceSlotId, addError);
            lock (_lock)
            {
                _activeTransfers.Remove(transferId);
            }
            return;
        }

        EmitSignal(SignalName.TransferProgress, transferId, session.ProgressFraction);

        if (session.IsComplete)
        {
            if (session.TryGetVerifiedAudio(out var verifiedAudio, out var verifyError))
            {
                EmitSignal(
                    SignalName.TransferCompleted,
                    transferId,
                    session.Manifest.VoiceSlotId,
                    session.Manifest.SenderPeerId,
                    session.Manifest.SenderName,
                    verifiedAudio);
            }
            else
            {
                GD.PrintErr($"[VoiceTransport] Integrity check failed for {transferId}: {verifyError}");
                EmitSignal(SignalName.TransferFailed, transferId, session.Manifest.VoiceSlotId, verifyError);
            }

            lock (_lock)
            {
                _activeTransfers.Remove(transferId);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcCancelTransfer(string transferId, string reason)
    {
        VoiceTakeAssemblySession? session;
        lock (_lock)
        {
            if (_activeTransfers.Remove(transferId, out session))
            {
                EmitSignal(SignalName.TransferFailed, transferId, session.Manifest.VoiceSlotId, reason);
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _activeTransfers.Clear();
        }
    }
}

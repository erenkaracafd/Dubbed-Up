using Godot;
using System.Text.Json;

namespace DubbedUp.Godot.Network.Sync;

/// <summary>
/// Coordinates peer clock synchronization, ready barrier validation, and scheduled
/// synchronized playback commands across the multiplayer network.
/// </summary>
public partial class PlaybackSyncCoordinator : Node
{
    public const int DefaultLeadTimeMs = 1500; // 1.5s network buffer

    [Signal]
    public delegate void PlaybackScheduledEventHandler(string sceneId, string idempotencyToken, float delaySeconds);

    [Signal]
    public delegate void PlaybackStartTriggeredEventHandler(string sceneId, string idempotencyToken);

    [Signal]
    public delegate void PlaybackSyncFailedEventHandler(string reason);

    private readonly NetworkClockSynchronizer _clockSynchronizer = new();
    private readonly PlaybackReadyBarrier _readyBarrier = new();
    private readonly HashSet<string> _processedTokens = [];

    private ScheduledPlaybackCommand? _pendingCommand;
    private double _countdownTimer = -1;

    public NetworkClockSynchronizer ClockSynchronizer => _clockSynchronizer;

    public PlaybackReadyBarrier ReadyBarrier => _readyBarrier;

    public bool IsSynchronized => _clockSynchronizer.SampleCount >= 3;

    public override void _Process(double delta)
    {
        if (_countdownTimer > 0)
        {
            _countdownTimer -= delta;
            if (_countdownTimer <= 0)
            {
                _countdownTimer = -1;
                if (_pendingCommand is not null)
                {
                    var cmd = _pendingCommand;
                    _pendingCommand = null;
                    EmitSignal(SignalName.PlaybackStartTriggered, cmd.SceneId, cmd.IdempotencyToken);
                }
            }
        }
    }

    public void StartClockSync()
    {
        _clockSynchronizer.Reset();
        SendPing();
    }

    private void SendPing()
    {
        if (Multiplayer.IsServer())
        {
            // Host clock offset is 0
            _clockSynchronizer.AddSample(0, 0, 0, 0);
            return;
        }

        var clientNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        RpcId(1, nameof(RpcClockPing), clientNow);
    }

    public void ReportSceneReady(string sceneId)
    {
        RpcId(1, nameof(RpcReportSceneReady), sceneId);
    }

    public void ReportTakesReady()
    {
        RpcId(1, nameof(RpcReportTakesReady));
    }

    public bool HostSchedulePlayback(
        string sceneId,
        int roundNumber = 1,
        string sessionId = "",
        int leadTimeMs = DefaultLeadTimeMs)
    {
        if (!Multiplayer.IsServer())
        {
            return false;
        }

        if (!_readyBarrier.IsAllReady)
        {
            var unready = string.Join(", ", _readyBarrier.GetUnreadyPeers());
            EmitSignal(SignalName.PlaybackSyncFailed, $"Cannot start: peers not ready ({unready}).");
            return false;
        }

        var hostNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var scheduledHostTime = hostNow + leadTimeMs;

        var command = new ScheduledPlaybackCommand
        {
            SessionId = sessionId,
            RoundNumber = roundNumber,
            SceneId = sceneId,
            IdempotencyToken = Guid.NewGuid().ToString("N"),
            ScheduledHostTimeMs = scheduledHostTime,
            PlaybackSpeed = 1.0f,
        };

        var commandJson = JsonSerializer.Serialize(command);
        Rpc(nameof(RpcSchedulePlaybackStart), commandJson);
        return true;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcClockPing(long clientSendTimeMs)
    {
        if (!Multiplayer.IsServer()) return;

        var senderId = Multiplayer.GetRemoteSenderId();
        var hostReceiveTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var hostSendTimeMs = hostReceiveTimeMs; // Immediate response

        RpcId(senderId, nameof(RpcClockPong), clientSendTimeMs, hostReceiveTimeMs, hostSendTimeMs);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcClockPong(long clientSendTimeMs, long hostReceiveTimeMs, long hostSendTimeMs)
    {
        var clientReceiveTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _clockSynchronizer.AddSample(clientSendTimeMs, hostReceiveTimeMs, hostSendTimeMs, clientReceiveTimeMs);

        if (_clockSynchronizer.SampleCount < 5)
        {
            // Send additional samples for median filter accuracy
            SendPing();
        }
        else
        {
            // Report clock sync complete to host
            RpcId(1, nameof(RpcReportClockSynced));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcReportSceneReady(string sceneId)
    {
        if (!Multiplayer.IsServer()) return;
        var senderId = Multiplayer.GetRemoteSenderId();
        _readyBarrier.SetPeerSceneReady(senderId, true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcReportTakesReady()
    {
        if (!Multiplayer.IsServer()) return;
        var senderId = Multiplayer.GetRemoteSenderId();
        _readyBarrier.SetPeerTakesReady(senderId, true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcReportClockSynced()
    {
        if (!Multiplayer.IsServer()) return;
        var senderId = Multiplayer.GetRemoteSenderId();
        _readyBarrier.SetPeerClockSynced(senderId, true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSchedulePlaybackStart(string commandJson)
    {
        try
        {
            var command = JsonSerializer.Deserialize<ScheduledPlaybackCommand>(commandJson);
            if (command is null)
            {
                EmitSignal(SignalName.PlaybackSyncFailed, "Invalid playback command received.");
                return;
            }

            // Idempotent duplicate check
            if (!_processedTokens.Add(command.IdempotencyToken))
            {
                GD.Print($"[PlaybackSync] Duplicate playback command {command.IdempotencyToken} discarded.");
                return;
            }

            var localNowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var delaySeconds = _clockSynchronizer.GetSecondsUntilStart(command.ScheduledHostTimeMs, localNowMs);

            if (delaySeconds < 0)
            {
                // Command arrived late
                GD.PrintErr($"[PlaybackSync] Playback command arrived late by {-delaySeconds:F2}s.");
                delaySeconds = 0;
            }

            _pendingCommand = command;
            _countdownTimer = Math.Max(0.01, delaySeconds);

            EmitSignal(SignalName.PlaybackScheduled, command.SceneId, command.IdempotencyToken, (float)delaySeconds);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[PlaybackSync] Failed to process playback schedule: {ex.Message}");
            EmitSignal(SignalName.PlaybackSyncFailed, ex.Message);
        }
    }

    public void Reset()
    {
        _clockSynchronizer.Reset();
        _readyBarrier.Reset();
        _processedTokens.Clear();
        _pendingCommand = null;
        _countdownTimer = -1;
    }
}

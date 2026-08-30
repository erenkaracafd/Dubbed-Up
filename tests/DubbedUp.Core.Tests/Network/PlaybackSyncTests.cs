using System.Text.Json;
using DubbedUp.Godot.Network.Sync;
using Xunit;

namespace DubbedUp.Core.Tests.Network;

public sealed class PlaybackSyncTests
{
    [Fact]
    public void ClockSynchronizer_Calculates_Offset_And_Filters_Outliers()
    {
        var sync = new NetworkClockSynchronizer();

        // Simulate symmetric 20ms network latency with 500ms host clock lead:
        // Client send: 1000, Host receive: 1510, Host send: 1510, Client receive: 1020
        // Offset = ((1510 - 1000) + (1510 - 1020)) / 2 = (510 + 490) / 2 = 500ms
        sync.AddSample(1000, 1510, 1510, 1020);
        sync.AddSample(2000, 2510, 2510, 2020);
        sync.AddSample(3000, 3510, 3510, 3020);

        // Add an outlier sample with 200ms latency spike
        sync.AddSample(4000, 4610, 4610, 4220);

        Assert.Equal(4, sync.SampleCount);
        Assert.Equal(500, sync.EstimatedClockOffsetMs);

        // Verify conversion from scheduled host timestamp to local start timestamp
        // Host scheduled: 10,000 -> Local target: 10,000 - 500 = 9,500
        var targetLocalTime = sync.GetLocalTimeForHostSchedule(10000);
        Assert.Equal(9500, targetLocalTime);

        // Current local time: 8,000 -> 1.5 seconds remaining
        var secondsUntilStart = sync.GetSecondsUntilStart(10000, 8000);
        Assert.Equal(1.5f, secondsUntilStart);
    }

    [Fact]
    public void ReadyBarrier_Evaluates_All_Peers_And_Readiness_Conditions()
    {
        var barrier = new PlaybackReadyBarrier();

        Assert.False(barrier.IsAllReady);

        barrier.RegisterPeer(1); // Host
        barrier.RegisterPeer(2); // Client 1
        barrier.RegisterPeer(3); // Client 2

        Assert.Equal(3, barrier.RegisteredPeerCount);
        Assert.False(barrier.IsAllReady);

        // Mark peer 1 ready
        barrier.SetPeerSceneReady(1, true);
        barrier.SetPeerTakesReady(1, true);
        barrier.SetPeerClockSynced(1, true);
        Assert.False(barrier.IsAllReady);

        // Mark peer 2 partially ready
        barrier.SetPeerSceneReady(2, true);
        barrier.SetPeerTakesReady(2, true);
        // Clock not yet synced for peer 2
        Assert.False(barrier.IsAllReady);
        var unready = barrier.GetUnreadyPeers();
        Assert.Contains(2, unready);
        Assert.Contains(3, unready);

        // Complete peer 2
        barrier.SetPeerClockSynced(2, true);

        // Peer 3 disconnects
        barrier.UnregisterPeer(3);

        // Now both remaining peers (1 and 2) are fully ready
        Assert.True(barrier.IsAllReady);
        Assert.Empty(barrier.GetUnreadyPeers());
    }

    [Fact]
    public void ScheduledPlaybackCommand_Serialization_RoundTrip_Succeeds()
    {
        var cmd = new ScheduledPlaybackCommand
        {
            SessionId = "session_abc",
            RoundNumber = 2,
            SceneId = "cooking_disaster",
            IdempotencyToken = "tok_12345",
            ScheduledHostTimeMs = 1750000000000,
            PlaybackSpeed = 1.0f,
        };

        var json = JsonSerializer.Serialize(cmd);
        var deserialized = JsonSerializer.Deserialize<ScheduledPlaybackCommand>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(cmd.SessionId, deserialized.SessionId);
        Assert.Equal(cmd.RoundNumber, deserialized.RoundNumber);
        Assert.Equal(cmd.SceneId, deserialized.SceneId);
        Assert.Equal(cmd.IdempotencyToken, deserialized.IdempotencyToken);
        Assert.Equal(cmd.ScheduledHostTimeMs, deserialized.ScheduledHostTimeMs);
        Assert.Equal(cmd.PlaybackSpeed, deserialized.PlaybackSpeed);
    }
}

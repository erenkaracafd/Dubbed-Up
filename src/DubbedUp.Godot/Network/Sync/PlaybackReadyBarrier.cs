namespace DubbedUp.Godot.Network.Sync;

/// <summary>
/// Host-authoritative ready barrier tracking scene availability, voice take completion,
/// and clock synchronization across all active room peers before playback can begin.
/// </summary>
public sealed class PlaybackReadyBarrier
{
    public sealed record PeerReadiness(long PeerId)
    {
        public bool SceneReady { get; set; }
        public bool TakesReady { get; set; }
        public bool ClockSynced { get; set; }

        public bool IsFullyReady => SceneReady && TakesReady && ClockSynced;
    }

    private readonly Dictionary<long, PeerReadiness> _peerStates = [];
    private readonly object _lock = new();

    public int RegisteredPeerCount
    {
        get
        {
            lock (_lock)
            {
                return _peerStates.Count;
            }
        }
    }

    public bool IsAllReady
    {
        get
        {
            lock (_lock)
            {
                return _peerStates.Count > 0 && _peerStates.Values.All(p => p.IsFullyReady);
            }
        }
    }

    public void RegisterPeer(long peerId)
    {
        lock (_lock)
        {
            if (!_peerStates.ContainsKey(peerId))
            {
                _peerStates[peerId] = new PeerReadiness(peerId);
            }
        }
    }

    public void UnregisterPeer(long peerId)
    {
        lock (_lock)
        {
            _peerStates.Remove(peerId);
        }
    }

    public void SetPeerSceneReady(long peerId, bool ready = true)
    {
        lock (_lock)
        {
            if (_peerStates.TryGetValue(peerId, out var state))
            {
                state.SceneReady = ready;
            }
        }
    }

    public void SetPeerTakesReady(long peerId, bool ready = true)
    {
        lock (_lock)
        {
            if (_peerStates.TryGetValue(peerId, out var state))
            {
                state.TakesReady = ready;
            }
        }
    }

    public void SetPeerClockSynced(long peerId, bool synced = true)
    {
        lock (_lock)
        {
            if (_peerStates.TryGetValue(peerId, out var state))
            {
                state.ClockSynced = synced;
            }
        }
    }

    public IReadOnlyList<long> GetUnreadyPeers()
    {
        lock (_lock)
        {
            return _peerStates.Values.Where(p => !p.IsFullyReady).Select(p => p.PeerId).ToList();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _peerStates.Clear();
        }
    }
}

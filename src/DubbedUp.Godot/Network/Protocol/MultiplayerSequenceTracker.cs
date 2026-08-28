namespace DubbedUp.Godot.Network.Protocol;

/// <summary>
/// Tracks sequence numbers for host-authoritative phase commands to ensure
/// idempotent processing and discard stale or out-of-order state transitions.
/// </summary>
public sealed class MultiplayerSequenceTracker
{
    private long _lastProcessedSequenceNumber = 0;

    public long LastProcessedSequenceNumber => _lastProcessedSequenceNumber;

    public bool TryProcessCommand(long sequenceNumber)
    {
        if (sequenceNumber <= _lastProcessedSequenceNumber)
        {
            return false;
        }

        _lastProcessedSequenceNumber = sequenceNumber;
        return true;
    }

    public void Reset(long initialSequenceNumber = 0)
    {
        _lastProcessedSequenceNumber = initialSequenceNumber;
    }
}

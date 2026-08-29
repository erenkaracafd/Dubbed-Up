namespace DubbedUp.Godot.Network.Sync;

/// <summary>
/// Measures RTT and estimates clock offset between client and host using NTP-style sampling.
/// Uses a sliding window with outlier rejection (median filter) to compute a stable host time estimate.
/// </summary>
public sealed class NetworkClockSynchronizer
{
    private const int MaxSamples = 7;
    private readonly List<ClockSample> _samples = [];
    private readonly object _lock = new();

    public readonly record struct ClockSample(long RttMs, long OffsetMs);

    public long EstimatedClockOffsetMs
    {
        get
        {
            lock (_lock)
            {
                if (_samples.Count == 0)
                {
                    return 0;
                }

                // Filter outliers by selecting the median offset of the lowest RTT samples
                var sorted = _samples.OrderBy(s => s.RttMs).Take(Math.Max(1, _samples.Count / 2 + 1)).ToList();
                var offsets = sorted.Select(s => s.OffsetMs).OrderBy(o => o).ToList();
                return offsets[offsets.Count / 2];
            }
        }
    }

    public int SampleCount
    {
        get
        {
            lock (_lock)
            {
                return _samples.Count;
            }
        }
    }

    public void AddSample(long clientSendTimeMs, long hostReceiveTimeMs, long hostSendTimeMs, long clientReceiveTimeMs)
    {
        // RTT = (t4 - t1) - (t3 - t2)
        var roundTripDuration = (clientReceiveTimeMs - clientSendTimeMs) - (hostSendTimeMs - hostReceiveTimeMs);
        var rttMs = Math.Max(0, roundTripDuration);

        // Offset = ((t2 - t1) + (t3 - t4)) / 2
        var offsetMs = ((hostReceiveTimeMs - clientSendTimeMs) + (hostSendTimeMs - clientReceiveTimeMs)) / 2;

        lock (_lock)
        {
            _samples.Add(new ClockSample(rttMs, offsetMs));
            if (_samples.Count > MaxSamples)
            {
                _samples.RemoveAt(0);
            }
        }
    }

    public long GetEstimatedHostTimeMs(long localTimeMs)
    {
        return localTimeMs + EstimatedClockOffsetMs;
    }

    public long GetLocalTimeForHostSchedule(long scheduledHostTimeMs)
    {
        return scheduledHostTimeMs - EstimatedClockOffsetMs;
    }

    public float GetSecondsUntilStart(long scheduledHostTimeMs, long currentLocalTimeMs)
    {
        var targetLocalTimeMs = GetLocalTimeForHostSchedule(scheduledHostTimeMs);
        var remainingMs = targetLocalTimeMs - currentLocalTimeMs;
        return (float)remainingMs / 1000f;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _samples.Clear();
        }
    }
}

using DubbedUp.Core.Game;

namespace DubbedUp.Core.VoiceTakes;

public sealed class VoiceTakeStore
{
    private readonly Dictionary<string, VoiceTake> _takesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VoiceTake> _latestTakeBySlot = new(StringComparer.Ordinal);

    public IReadOnlyList<VoiceTake> GetAllTakes() => _takesById.Values.ToArray();

    public void AddTake(VoiceTake take)
    {
        ArgumentNullException.ThrowIfNull(take);

        if (_takesById.ContainsKey(take.TakeId))
        {
            throw new GameRuleException($"Take with id '{take.TakeId}' already exists.");
        }

        _takesById[take.TakeId] = take;
        _latestTakeBySlot[take.VoiceSlotId] = take;
    }

    public VoiceTake? GetTake(string takeId)
    {
        if (string.IsNullOrWhiteSpace(takeId))
        {
            return null;
        }

        _takesById.TryGetValue(takeId, out var take);
        return take;
    }

    public VoiceTake? GetLatestTakeForSlot(string voiceSlotId)
    {
        if (string.IsNullOrWhiteSpace(voiceSlotId))
        {
            return null;
        }

        _latestTakeBySlot.TryGetValue(voiceSlotId, out var take);
        return take;
    }

    public bool HasTakeForSlot(string voiceSlotId)
    {
        if (string.IsNullOrWhiteSpace(voiceSlotId))
        {
            return false;
        }

        return _latestTakeBySlot.ContainsKey(voiceSlotId);
    }
}

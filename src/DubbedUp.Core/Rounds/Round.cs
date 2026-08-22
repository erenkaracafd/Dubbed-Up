using DubbedUp.Core.Characters;
using DubbedUp.Core.Game;

namespace DubbedUp.Core.Rounds;

public sealed class Round
{
    private readonly Dictionary<string, string> _characterPlayers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recordedVoiceSlotIds = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<VoiceSlotDefinition> _voiceSlots;
    private readonly HashSet<string> _playerIds;

    internal Round(
        string roundId,
        string sceneId,
        IReadOnlyList<string> playerIds,
        IReadOnlyList<VoiceSlotDefinition> voiceSlots)
    {
        RoundId = roundId;
        SceneId = sceneId;
        _playerIds = playerIds.ToHashSet(StringComparer.Ordinal);
        _voiceSlots = voiceSlots.ToArray();
        Phase = RoundPhase.AssigningCharacters;
    }

    public string RoundId { get; }

    public string SceneId { get; }

    public RoundPhase Phase { get; private set; }

    public IReadOnlyList<CharacterAssignment> CharacterAssignments => _characterPlayers
        .Select(pair => new CharacterAssignment(pair.Key, pair.Value))
        .OrderBy(assignment => assignment.CharacterId, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyCollection<string> RecordedVoiceSlotIds => _recordedVoiceSlotIds.ToArray();

    public void AssignCharacter(string characterId, string playerId)
    {
        EnsurePhase(RoundPhase.AssigningCharacters);

        if (!_playerIds.Contains(playerId))
        {
            throw new GameRuleException($"Unknown playerId '{playerId}'.");
        }

        var requiredCharacterIds = _voiceSlots.Select(slot => slot.CharacterId).ToHashSet(StringComparer.Ordinal);
        if (!requiredCharacterIds.Contains(characterId))
        {
            throw new GameRuleException($"CharacterId '{characterId}' has no voice slots in this round.");
        }

        if (!_characterPlayers.TryAdd(characterId, playerId))
        {
            throw new GameRuleException($"CharacterId '{characterId}' is already assigned.");
        }
    }

    public IReadOnlyList<VoiceSlotAssignment> GetVoiceSlotAssignments()
    {
        EnsureAllCharactersAssigned();
        return _voiceSlots
            .Select(slot => new VoiceSlotAssignment(
                slot.VoiceSlotId,
                slot.CharacterId,
                _characterPlayers[slot.CharacterId]))
            .ToArray();
    }

    public void StartRecording()
    {
        EnsurePhase(RoundPhase.AssigningCharacters);
        EnsureAllCharactersAssigned();
        Phase = RoundPhase.Recording;
    }

    public void MarkVoiceSlotRecorded(string voiceSlotId)
    {
        EnsurePhase(RoundPhase.Recording);
        if (_voiceSlots.All(slot => !string.Equals(slot.VoiceSlotId, voiceSlotId, StringComparison.Ordinal)))
        {
            throw new GameRuleException($"Unknown voiceSlotId '{voiceSlotId}'.");
        }

        if (!_recordedVoiceSlotIds.Add(voiceSlotId))
        {
            throw new GameRuleException($"VoiceSlotId '{voiceSlotId}' is already recorded.");
        }

        if (_recordedVoiceSlotIds.Count == _voiceSlots.Count)
        {
            Phase = RoundPhase.ReadyForPlayback;
        }
    }

    public void StartPlayback()
    {
        EnsurePhase(RoundPhase.ReadyForPlayback);
        Phase = RoundPhase.Playing;
    }

    public void FinishPlayback()
    {
        EnsurePhase(RoundPhase.Playing);
        Phase = RoundPhase.Voting;
    }

    public void CompleteVoting()
    {
        EnsurePhase(RoundPhase.Voting);
        Phase = RoundPhase.Complete;
    }

    private void EnsureAllCharactersAssigned()
    {
        var missing = _voiceSlots
            .Select(slot => slot.CharacterId)
            .Distinct(StringComparer.Ordinal)
            .Where(characterId => !_characterPlayers.ContainsKey(characterId))
            .OrderBy(characterId => characterId, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new GameRuleException($"Missing character assignments: {string.Join(", ", missing)}.");
        }
    }

    private void EnsurePhase(RoundPhase expected)
    {
        if (Phase != expected)
        {
            throw new GameRuleException($"Round must be in phase '{expected}', but is '{Phase}'.");
        }
    }
}

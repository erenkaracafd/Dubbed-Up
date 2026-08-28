using Steamworks;

namespace DubbedUp.Godot.Steam;

/// <summary>
/// Owns the Steam client lifecycle and lightweight lobby membership/state.
/// Lobby metadata is deliberately restricted to identifiers and phase state;
/// media and voice payloads belong to separate local/P2P paths.
/// </summary>
public sealed class SteamLobbyService : IDisposable
{
    private readonly List<IDisposable> _callbacks = [];
    private CallResult<LobbyCreated_t>? _createLobbyResult;
    private CallResult<LobbyEnter_t>? _joinLobbyResult;
    private bool _isInitialized;
    private bool _isDisposed;
    private CSteamID _currentLobbyId = CSteamID.Nil;
    private IReadOnlyList<SteamLobbyMember> _members = [];

    public event Action<bool, string>? AvailabilityChanged;

    public event Action<ulong, IReadOnlyList<SteamLobbyMember>>? LobbyChanged;

    public event Action<ulong>? LobbyJoinRequested;

    public bool IsAvailable => _isInitialized;

    public ulong CurrentLobbyId => _currentLobbyId.m_SteamID;

    public IReadOnlyList<SteamLobbyMember> Members => _members;

    public bool Initialize()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isInitialized)
        {
            return true;
        }

        try
        {
            if (!SteamAPI.IsSteamRunning() || !SteamAPI.Init())
            {
                AvailabilityChanged?.Invoke(false, "Steam is not running or SteamAPI initialization failed.");
                return false;
            }

            _callbacks.Add(Callback<LobbyEnter_t>.Create(OnLobbyEntered));
            _callbacks.Add(Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdated));
            _callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested));

            _createLobbyResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinLobbyResult = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);
            _isInitialized = true;
            AvailabilityChanged?.Invoke(true, $"Steam initialized for {SteamFriends.GetPersonaName()}.");
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or InvalidOperationException)
        {
            AvailabilityChanged?.Invoke(false, $"Steam is unavailable: {ex.Message}");
            Shutdown();
            return false;
        }
    }

    public void RunCallbacks()
    {
        if (_isInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public bool CreateLobby(int maxMembers = 8)
    {
        if (!_isInitialized || maxMembers is < 2 or > 8 || _createLobbyResult is null)
        {
            return false;
        }

        LeaveLobby();
        _createLobbyResult.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers));
        return true;
    }

    public bool JoinLobby(ulong lobbyId)
    {
        if (!_isInitialized || lobbyId == 0 || _joinLobbyResult is null)
        {
            return false;
        }

        LeaveLobby();
        _joinLobbyResult.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobbyId)));
        return true;
    }

    public void LeaveLobby()
    {
        if (!_isInitialized || _currentLobbyId == CSteamID.Nil)
        {
            return;
        }

        SteamMatchmaking.LeaveLobby(_currentLobbyId);
        _currentLobbyId = CSteamID.Nil;
        _members = [];
        LobbyChanged?.Invoke(0, _members);
    }

    public bool OpenInviteOverlay()
    {
        if (!_isInitialized || _currentLobbyId == CSteamID.Nil)
        {
            return false;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(_currentLobbyId);
        return true;
    }

    public bool SetMetadata(string key, string value)
    {
        return _isInitialized
            && _currentLobbyId != CSteamID.Nil
            && SteamLobbyMetadata.IsAllowed(key, value)
            && SteamMatchmaking.SetLobbyData(_currentLobbyId, key, value);
    }

    public string GetMetadata(string key)
    {
        if (!_isInitialized || _currentLobbyId == CSteamID.Nil || !SteamLobbyMetadata.IsAllowed(key, string.Empty))
        {
            return string.Empty;
        }

        return SteamMatchmaking.GetLobbyData(_currentLobbyId, key);
    }

    public void Shutdown()
    {
        LeaveLobby();

        _createLobbyResult?.Dispose();
        _createLobbyResult = null;
        _joinLobbyResult?.Dispose();
        _joinLobbyResult = null;

        foreach (var callback in _callbacks)
        {
            callback.Dispose();
        }

        _callbacks.Clear();

        if (_isInitialized)
        {
            SteamAPI.Shutdown();
            _isInitialized = false;
            AvailabilityChanged?.Invoke(false, "Steam shut down.");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Shutdown();
        _isDisposed = true;
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
    {
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            AvailabilityChanged?.Invoke(true, $"Steam lobby creation failed: {result.m_eResult}.");
            return;
        }

        _currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
        SetMetadata(SteamLobbyMetadata.ProtocolVersionKey, "1");
        RefreshMembers();
    }

    private void OnLobbyEntered(LobbyEnter_t result, bool ioFailure)
    {
        if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            AvailabilityChanged?.Invoke(true, $"Steam lobby join failed: {result.m_EChatRoomEnterResponse}.");
            return;
        }

        _currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
        RefreshMembers();
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        OnLobbyEntered(result, false);
    }

    private void OnLobbyChatUpdated(LobbyChatUpdate_t update)
    {
        if (_currentLobbyId.m_SteamID == update.m_ulSteamIDLobby)
        {
            RefreshMembers();
        }
    }

    private void OnLobbyJoinRequested(GameLobbyJoinRequested_t request)
    {
        LobbyJoinRequested?.Invoke(request.m_steamIDLobby.m_SteamID);
    }

    private void RefreshMembers()
    {
        if (!_isInitialized || _currentLobbyId == CSteamID.Nil)
        {
            _members = [];
            LobbyChanged?.Invoke(0, _members);
            return;
        }

        var owner = SteamMatchmaking.GetLobbyOwner(_currentLobbyId);
        var members = new List<SteamLobbyMember>();
        var memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyId);
        for (var index = 0; index < memberCount; index++)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyId, index);
            members.Add(new SteamLobbyMember(
                member.m_SteamID,
                SteamFriends.GetFriendPersonaName(member),
                member == owner));
        }

        _members = members;
        LobbyChanged?.Invoke(_currentLobbyId.m_SteamID, _members);
    }
}

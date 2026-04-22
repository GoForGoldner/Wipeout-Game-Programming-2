using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public enum MatchState { Lobby, PerkSelection, Playing, Countdown, Finished, Joinning }

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    public NetworkVariable<MatchState> State = new NetworkVariable<MatchState>(
        MatchState.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> PlayerCount = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkList<PlayerPerkSelection> PerkSelections;

    public NetworkVariable<int> PlayersInLevel = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentLevelIndex = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int MaxPlayerCount => maxPlayerCount;
    [SerializeField] int maxPlayerCount = 12;

    [Header("Debug Modes")]
    [Tooltip("Allow matches to start with 1 player. Use to check all 3 levels connect.")]
    public bool debugSoloMode = false;
    [Tooltip("Allow matches to start with 3 players. Use to test elimination (3 -> 2 -> 1).")]
    public bool debugTrioMode = false;
    [SerializeField] int normalMinPlayers = 4;

    [Header("Scene Flow")]
    [SerializeField] string lobbySceneName = "LobbyScene";
    [SerializeField] List<string> gameplaySceneNames = new List<string> { "Round1", "Round2", "Round3" };

    public string LobbySceneName => lobbySceneName;

    public string GetCurrentGameplaySceneName()
    {
        if (gameplaySceneNames == null || gameplaySceneNames.Count == 0)
            return string.Empty;

        int index = Mathf.Clamp(CurrentLevelIndex.Value, 0, gameplaySceneNames.Count - 1);
        return gameplaySceneNames[index];
    }

    public int MinPlayersToStart
    {
        get
        {
            if (debugSoloMode) return 1;
            if (debugTrioMode) return 3;
            return normalMinPlayers;
        }
    }

    public bool IsFinalLevel => CurrentLevelIndex.Value >= 2;

    public string LobbyCode => lobbyCode;
    string lobbyCode;

    readonly HashSet<ulong> eliminated = new HashSet<ulong>();
    NetworkList<ulong> eliminatedSync;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        eliminatedSync = new NetworkList<ulong>();
        PerkSelections = new NetworkList<PlayerPerkSelection>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientChanged;

        UpdatePlayerCount();
        RebuildPerkSelections();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientChanged;
        }
    }

    void OnClientChanged(ulong _)
    {
        UpdatePlayerCount();
        RebuildPerkSelections();
    }

    void Update()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        int actual = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (PlayerCount.Value != actual)
            PlayerCount.Value = actual;
    }

    void UpdatePlayerCount()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;
        PlayerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
    }

    int GetExpectedPerkChooserCount()
    {
        if (PlayersInLevel.Value > 0)
            return PlayersInLevel.Value;

        return NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;
    }

    void RebuildPerkSelections()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        var oldSelections = new Dictionary<ulong, PlayerPerkSelection>();
        foreach (var entry in PerkSelections)
        {
            oldSelections[entry.clientId] = entry;
        }

        PerkSelections.Clear();

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // After Round 1 starts, only survivors should participate in later perk picks.
            if (PlayersInLevel.Value > 0 && IsEliminated(clientId))
                continue;

            if (oldSelections.TryGetValue(clientId, out var oldEntry))
            {
                PerkSelections.Add(new PlayerPerkSelection(clientId, oldEntry.perkIndex, false));
            }
            else
            {
                PerkSelections.Add(new PlayerPerkSelection(clientId, -1, false));
            }
        }
    }

    void UpdatePerkSelection(ulong clientId, int perkIndex, bool isReady)
    {
        if (!IsServer) return;

        for (int i = 0; i < PerkSelections.Count; i++)
        {
            if (PerkSelections[i].clientId == clientId)
            {
                PerkSelections[i] = new PlayerPerkSelection(clientId, perkIndex, isReady);
                return;
            }
        }

        PerkSelections.Add(new PlayerPerkSelection(clientId, perkIndex, isReady));
    }

    bool AreAllPlayersReadyForPerks()
    {
        if (!IsServer || NetworkManager.Singleton == null) return false;
        if (PerkSelections.Count == 0) return false;

        int expectedCount = GetExpectedPerkChooserCount();
        if (PerkSelections.Count != expectedCount) return false;

        foreach (var entry in PerkSelections)
        {
            if (!entry.isReady || entry.perkIndex < 0)
                return false;
        }

        return true;
    }

    public void BeginPerkSelection()
    {
        if (!IsServer) return;

        RebuildPerkSelections();
        State.Value = MatchState.PerkSelection;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitPerkSelectionServerRpc(int perkIndex, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        // After Round 1 has begun, eliminated players are not allowed to pick perks anymore.
        if (PlayersInLevel.Value > 0 && IsEliminated(senderId))
        {
            Debug.Log($"Ignoring perk selection from eliminated client {senderId}.");
            return;
        }

        UpdatePerkSelection(senderId, perkIndex, true);

        if (AreAllPlayersReadyForPerks())
        {
            if (PlayersInLevel.Value <= 0)
                StartInitialMatch();
            else
                StartCurrentRound();
        }
    }

    public int GetLocalPerkIndex()
    {
        if (NetworkManager.Singleton == null) return -1;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        foreach (var entry in PerkSelections)
        {
            if (entry.clientId == localId)
                return entry.perkIndex;
        }

        return -1;
    }

    // ────────────────────────────── Match flow ──────────────────────────────

    public void StartInitialMatch()
    {
        if (!IsServer) return;

        CurrentLevelIndex.Value = 0;
        PlayersInLevel.Value = PlayerCount.Value;

        eliminated.Clear();
        eliminatedSync.Clear();

        State.Value = MatchState.Playing;
    }

    public void StartCurrentRound()
    {
        if (!IsServer) return;
        State.Value = MatchState.Playing;
    }

    public void BeginNextRoundPerkSelection(int survivingPlayers)
    {
        if (!IsServer) return;

        CurrentLevelIndex.Value++;
        PlayersInLevel.Value = survivingPlayers;

        RebuildPerkSelections();
        State.Value = MatchState.PerkSelection;
    }

    public void AdvanceLevel(int newPlayersInLevel)
    {
        if (!IsServer) return;
        CurrentLevelIndex.Value++;
        PlayersInLevel.Value = newPlayersInLevel;
    }

    public void EndMatch()
    {
        if (!IsServer) return;
        State.Value = MatchState.Finished;
    }

    public void ActivateLobby(string joinCode)
    {
        if (!IsServer) return;
        lobbyCode = joinCode;
        State.Value = MatchState.Lobby;
    }

    public void ActivateJoining()
    {
        if (!IsServer) return;
        State.Value = MatchState.Joinning;
    }

    // ────────────────────────────── Elimination ──────────────────────────────

    public void MarkEliminated(ulong clientId)
    {
        if (!IsServer) return;
        if (eliminated.Add(clientId))
        {
            eliminatedSync.Add(clientId);
        }
    }

    public bool IsEliminated(ulong clientId)
    {
        if (IsServer) return eliminated.Contains(clientId);

        for (int i = 0; i < eliminatedSync.Count; i++)
            if (eliminatedSync[i] == clientId) return true;
        return false;
    }

    public int GetQualifierCount()
    {
        if (IsFinalLevel) return 1;

        int n = PlayersInLevel.Value;
        if (n <= 1) return 1;

        return Mathf.Max(1, Mathf.CeilToInt(n * 2f / 3f));
    }
}
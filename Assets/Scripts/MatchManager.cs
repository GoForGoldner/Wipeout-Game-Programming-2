using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public enum MatchState { Lobby, Playing, Countdown, Finished, Joinning }

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

    // Number of players still "alive" (not eliminated) at the start of the current level.
    // Used to compute qualifier counts.
    public NetworkVariable<int> PlayersInLevel = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // 0 = Level 1, 1 = Level 2, 2 = Level 3 (final)
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
    [SerializeField] int normalMinPlayers = 6;

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

    // Server-only set of eliminated clientIds. Persists across level scenes because
    // MatchManager is DontDestroyOnLoad. Clients query via IsEliminated NetworkBehaviour RPC-style method below.
    readonly HashSet<ulong> eliminated = new HashSet<ulong>();

    // Synced version of the eliminated set (NetworkList only holds the ids).
    // Kept in sync with `eliminated` by the server. Clients read this.
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
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientChanged;

        UpdatePlayerCount();
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

    void OnClientChanged(ulong _) => UpdatePlayerCount();

    void Update()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;
        int actual = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (PlayerCount.Value != actual)
            PlayerCount.Value = actual;
    }

    void UpdatePlayerCount()
    {
        if (!IsServer) return;
        PlayerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
    }

    // ────────────────────────────── Match flow ──────────────────────────────

    public void StartMatch()
    {
        if (!IsServer) return;
        State.Value = MatchState.Playing;
        CurrentLevelIndex.Value = 0;
        PlayersInLevel.Value = PlayerCount.Value;

        eliminated.Clear();
        eliminatedSync.Clear();
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
        // Server has the authoritative set; clients check via the synced list.
        if (IsServer) return eliminated.Contains(clientId);

        for (int i = 0; i < eliminatedSync.Count; i++)
            if (eliminatedSync[i] == clientId) return true;
        return false;
    }

    /// <summary>
    /// How many players should qualify to advance from the current level.
    /// Final level always keeps 1. Earlier levels keep ceil(PlayersInLevel * 2/3), min 1.
    /// </summary>
    public int GetQualifierCount()
    {
        if (IsFinalLevel) return 1;
        int n = PlayersInLevel.Value;
        if (n <= 1) return 1;
        return Mathf.Max(1, Mathf.CeilToInt(n * 2f / 3f));
    }
}

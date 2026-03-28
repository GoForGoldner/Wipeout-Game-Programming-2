using UnityEngine;
using Unity.Netcode;

public enum MatchState{Lobby, Playing, Countdown, Finished, Joinning}

public class MatchManager : NetworkBehaviour
{   
    public static MatchManager Instance{get; private set;}
    public NetworkVariable<MatchState> State = new
    NetworkVariable<MatchState>(
        MatchState.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> PlayerCount = new NetworkVariable<int>
        (0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int MaxPlayerCount => maxPlayerCount;
    [SerializeField] int maxPlayerCount = 12;

    public string LobbyCode => lobbyCode;
    string lobbyCode;
    

    void Awake()
    {
       if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback +=
        OnClientChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback -=
        OnClientChanged;

        UpdatePlayerCount();
    }

    void OnClientChanged(ulong _)
    {
        UpdatePlayerCount();
    }

    void UpdatePlayerCount()
    {
        PlayerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
    }

    public void StartMatch()
    {
        if(!IsServer) return;
        State.Value = MatchState.Playing;
    }
    public void EndMatch()
    {
        if(!IsServer) return;
        State.Value = MatchState.Finished;
    }
    public void ActivateLobby(string joinCode)
    {
        if(!IsServer) return;
        lobbyCode = joinCode;
        State.Value = MatchState.Lobby;
    }
    public void ActivateJoining()
    {
        if(!IsServer) return;
        State.Value = MatchState.Joinning;
    }
}

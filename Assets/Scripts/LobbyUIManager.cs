using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    public GameObject startMatchButton;
    public GameObject waitMatchText;
    public string gameplaySceneName;
    public TMP_Text lobbyCodeTxt;

    Button startButton;
    MatchManager matchManager;

    void Start()
    {
        matchManager = FindFirstObjectByType<MatchManager>();
        if (!matchManager) return;

        matchManager.State.OnValueChanged += OnStateChanged;
        OnStateChanged(MatchState.Lobby, matchManager.State.Value);

        startButton = startMatchButton.GetComponent<Button>();
        startButton.onClick.AddListener(OnStartMatchClicked);
    }

    void OnStartMatchClicked()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        int playerCount = matchManager.PlayerCount.Value;
        int minRequired = matchManager.MinPlayersToStart;
        int actualConnected = NetworkManager.Singleton.ConnectedClients.Count;

        Debug.Log($"PlayerCount.Value = {playerCount}, ConnectedClients.Count = {actualConnected}, MinRequired = {minRequired}");

        if (playerCount < minRequired)
        {
            Debug.Log($"Need {minRequired} players to start (currently {playerCount}).");
            return;
        }

        matchManager.StartMatch();
        SceneLoader.LoadNetworked(gameplaySceneName);
    }

    void OnStateChanged(MatchState oldState, MatchState newState)
    {
        if (newState != MatchState.Lobby) return;

        if (NetworkManager.Singleton.IsHost)
        {
            startMatchButton.SetActive(true);
            waitMatchText.SetActive(false);
            if (lobbyCodeTxt)
                lobbyCodeTxt.text = matchManager.LobbyCode;
        }
        else
        {
            waitMatchText.SetActive(true);
            startMatchButton.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (matchManager)
            matchManager.State.OnValueChanged -= OnStateChanged;
    }
}
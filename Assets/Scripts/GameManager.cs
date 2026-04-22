using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public GameObject resultPanel;
    public GameObject qualifiedText;
    public GameObject eliminatedText;
    public GameObject winText;
    public GameObject loseText;

    [Header("Cameras")]
    [Tooltip("Overhead spectator camera for this level. Enabled for eliminated clients.")]
    public GameObject spectatorCamera;

    [Header("Level Progression")]
    [Tooltip("Scene name to load after this level ends. Leave blank on the final level.")]
    public string nextSceneName = "";

    [Tooltip("Scene to return to after the match ends.")]
    public string startSceneName = "StartScene";

    [Tooltip("Seconds to show the result panel before transitioning.")]
    public float transitionDelay = 4f;

    NetworkList<ulong> finishOrder;

    NetworkVariable<bool> levelClosed = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    NetworkVariable<ulong> winnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    bool transitionStarted;
    bool localResultShown;

    void Awake()
    {
        Instance = this;
        if (resultPanel) resultPanel.SetActive(false);
        if (spectatorCamera) spectatorCamera.SetActive(false);

        finishOrder = new NetworkList<ulong>();
    }

    public override void OnNetworkSpawn()
    {
        winnerClientId.OnValueChanged += OnWinnerChanged;
        levelClosed.OnValueChanged += OnLevelClosedChanged;
        finishOrder.OnListChanged += OnFinishOrderChanged;

        if (MatchManager.Instance != null &&
            MatchManager.Instance.IsEliminated(NetworkManager.Singleton.LocalClientId))
        {
            EnterSpectatorMode();
        }
    }

    public override void OnNetworkDespawn()
    {
        winnerClientId.OnValueChanged -= OnWinnerChanged;
        levelClosed.OnValueChanged -= OnLevelClosedChanged;
        finishOrder.OnListChanged -= OnFinishOrderChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportFinishServerRpc(ServerRpcParams rpcParams = default)
    {
        if (levelClosed.Value) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (MatchManager.Instance != null && MatchManager.Instance.IsEliminated(senderId))
            return;

        for (int i = 0; i < finishOrder.Count; i++)
            if (finishOrder[i] == senderId) return;

        finishOrder.Add(senderId);

        bool isFinal = MatchManager.Instance != null && MatchManager.Instance.IsFinalLevel;

        if (isFinal)
        {
            if (winnerClientId.Value == ulong.MaxValue)
            {
                winnerClientId.Value = senderId;
                CloseLevel();
            }
            return;
        }

        int qualifierCount = MatchManager.Instance != null
            ? MatchManager.Instance.GetQualifierCount()
            : 1;

        if (finishOrder.Count >= qualifierCount)
        {
            CloseLevel();
        }
    }

    void CloseLevel()
    {
        if (!IsServer || levelClosed.Value) return;

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (MatchManager.Instance.IsEliminated(clientId)) continue;

            bool finished = false;
            for (int i = 0; i < finishOrder.Count; i++)
            {
                if (finishOrder[i] == clientId)
                {
                    finished = true;
                    break;
                }
            }

            if (!finished)
                MatchManager.Instance.MarkEliminated(clientId);
        }

        levelClosed.Value = true;
    }

    void OnFinishOrderChanged(NetworkListEvent<ulong> change)
    {
        if (localResultShown) return;

        bool isFinal = MatchManager.Instance != null && MatchManager.Instance.IsFinalLevel;
        if (isFinal) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < finishOrder.Count; i++)
        {
            if (finishOrder[i] == localId)
            {
                ShowResult(qualified: true, isFinalLevel: false);
                return;
            }
        }
    }

    void OnLevelClosedChanged(bool oldVal, bool newVal)
    {
        if (!newVal) return;

        bool isFinal = MatchManager.Instance != null && MatchManager.Instance.IsFinalLevel;

        if (!isFinal && !localResultShown)
        {
            ShowResult(qualified: false, isFinalLevel: false);
            EnterSpectatorMode();
        }

        if (IsServer && !transitionStarted)
        {
            transitionStarted = true;
            StartCoroutine(isFinal ? ServerEndMatchAfterDelay() : ServerAdvanceAfterDelay());
        }
    }

    void OnWinnerChanged(ulong oldVal, ulong newVal)
    {
        if (newVal == ulong.MaxValue) return;
        if (localResultShown) return;

        bool localWon = NetworkManager.Singleton.LocalClientId == newVal;
        ShowResult(qualified: localWon, isFinalLevel: true, localWon: localWon);
    }

    void ShowResult(bool qualified, bool isFinalLevel, bool localWon = false)
    {
        if (localResultShown) return;
        localResultShown = true;

        if (resultPanel) resultPanel.SetActive(true);

        if (isFinalLevel)
        {
            if (winText) winText.SetActive(localWon);
            if (loseText) loseText.SetActive(!localWon);
            if (qualifiedText) qualifiedText.SetActive(false);
            if (eliminatedText) eliminatedText.SetActive(false);
        }
        else
        {
            if (qualifiedText) qualifiedText.SetActive(qualified);
            if (eliminatedText) eliminatedText.SetActive(!qualified);
            if (winText) winText.SetActive(false);
            if (loseText) loseText.SetActive(false);
        }

        if (qualified && PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.AddWin();
        }
    }

    void EnterSpectatorMode()
    {
        if (spectatorCamera) spectatorCamera.SetActive(true);

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient != null && localClient.PlayerObject != null)
        {
            var controller = localClient.PlayerObject.GetComponent<PlayerController>();
            if (controller) controller.enabled = false;
        }

        foreach (var orbit in FindObjectsByType<OrbitCamera>(FindObjectsSortMode.None))
        {
            if (orbit.cam) orbit.cam.gameObject.SetActive(false);
        }
    }

    IEnumerator ServerAdvanceAfterDelay()
    {
        yield return new WaitForSeconds(transitionDelay);

        int survivors = finishOrder.Count;

        if (MatchManager.Instance == null)
        {
            Debug.LogError("GameManager: MatchManager.Instance is null.");
            yield break;
        }

        MatchManager.Instance.BeginNextRoundPerkSelection(survivors);

        string lobbyScene = MatchManager.Instance.LobbySceneName;
        if (string.IsNullOrEmpty(lobbyScene))
        {
            Debug.LogError("GameManager: Lobby scene name is empty.");
            yield break;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
    }

    IEnumerator ServerEndMatchAfterDelay()
    {
        yield return new WaitForSeconds(transitionDelay);

        EndMatchClientRpc(startSceneName);
        yield return null;

        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(startSceneName);
    }

    [ClientRpc]
    void EndMatchClientRpc(string sceneToLoad)
    {
        if (IsServer) return;
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(sceneToLoad);
    }
}
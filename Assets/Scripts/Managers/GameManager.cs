using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public GameObject resultPanel;
    public GameObject qualifiedText;   // "Qualified!"
    public GameObject eliminatedText;  // "Eliminated - Spectating"
    public GameObject winText;         // "You Win!" (final level)
    public GameObject loseText;        // "You Lose"  (final level)

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

    // Ordered list of clientIds that have finished THIS level.
    NetworkList<ulong> finishOrder;

    // Set to true by the server when the qualifier quota is reached.
    NetworkVariable<bool> levelClosed = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Final-level winner (only used when IsFinalLevel).
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

        // If this client is already eliminated (carried over from a previous level),
        // activate spectator mode immediately on scene load.
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

    // ─────────────────────────── Finish handling ───────────────────────────

    [ServerRpc(RequireOwnership = false)]
    public void ReportFinishServerRpc(ServerRpcParams rpcParams = default)
    {
        if (levelClosed.Value) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        // Eliminated players shouldn't be able to finish.
        if (MatchManager.Instance != null && MatchManager.Instance.IsEliminated(senderId))
            return;

        // Ignore duplicate finishes.
        for (int i = 0; i < finishOrder.Count; i++)
            if (finishOrder[i] == senderId) return;

        finishOrder.Add(senderId);

        bool isFinal = MatchManager.Instance != null && MatchManager.Instance.IsFinalLevel;

        if (isFinal)
        {
            // First finisher wins the match.
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

        // Mark everyone who DIDN'T finish as eliminated.
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (MatchManager.Instance.IsEliminated(clientId)) continue; // already out

            bool finished = false;
            for (int i = 0; i < finishOrder.Count; i++)
                if (finishOrder[i] == clientId) { finished = true; break; }

            if (!finished)
                MatchManager.Instance.MarkEliminated(clientId);
        }

        levelClosed.Value = true;
    }

    // ────────────────────────────── UI flow ──────────────────────────────

    void OnFinishOrderChanged(NetworkListEvent<ulong> change)
    {
        // Show "Qualified!" to this client the instant they cross the line
        // (non-final levels only; final-level winner gets a different banner).
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

        // If I never finished and level just closed, I'm eliminated.
        if (!isFinal && !localResultShown)
        {
            ShowResult(qualified: false, isFinalLevel: false);
            EnterSpectatorMode();
        }

        // Server drives the transition timer.
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

        // Single-level win counter: +1 whenever you qualify (including final-level winner).
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

        // Disable ALL orbit cameras in the scene (the one rigged to the local player).
        // The spectator camera has higher depth so it renders on top anyway, but this
        // prevents dual AudioListener warnings and stops mouse-look input consumption.
        foreach (var orbit in FindObjectsByType<OrbitCamera>(FindObjectsSortMode.None))
        {
            if (orbit.cam) orbit.cam.gameObject.SetActive(false);
        }
    }

    // ────────────────────────── Server transitions ──────────────────────────

    IEnumerator ServerAdvanceAfterDelay()
    {
        yield return new WaitForSeconds(transitionDelay);

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("GameManager: nextSceneName is empty but level is not final. Ending match.");
            yield return ServerEndMatchAfterDelay();
            yield break;
        }

        int survivors = finishOrder.Count; // those who qualified this level
        MatchManager.Instance.AdvanceLevel(survivors);

        NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    IEnumerator ServerEndMatchAfterDelay()
    {
        yield return new WaitForSeconds(transitionDelay);

        // Tell every non-host client to shut down and load the start scene.
        EndMatchClientRpc(startSceneName);
        yield return null;

        // Host shuts down last.
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(startSceneName);
    }

    [ClientRpc]
    void EndMatchClientRpc(string sceneToLoad)
    {
        if (IsServer) return; // host handles its own shutdown above
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(sceneToLoad);
    }
}

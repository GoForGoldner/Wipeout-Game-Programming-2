using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public GameObject resultPanel;
    public GameObject winText;
    public GameObject loseText;

    NetworkVariable<ulong> winnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        Instance = this;
        resultPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        winnerClientId.OnValueChanged += OnWinnerChanged;
    }

    void OnWinnerChanged(ulong oldVal, ulong newVal)
    {
        if (newVal == ulong.MaxValue) return;

        bool isWinner = NetworkManager.Singleton.LocalClientId == newVal;
        winText.SetActive(isWinner);
        loseText.SetActive(!isWinner);
        resultPanel.SetActive(true);

        // Persist win count for the local winning player only.
        // Each client runs this, but only the actual winner matches the ID.
        if (isWinner && PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.AddWin();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportFinishServerRpc(ServerRpcParams rpcParams = default)
    {
        if (winnerClientId.Value != ulong.MaxValue) return;
        winnerClientId.Value = rpcParams.Receive.SenderClientId;
    }
}
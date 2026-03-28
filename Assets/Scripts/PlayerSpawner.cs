using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    public string gameplaySceneName = "GameScene";
    public string spawnPointParentTag = "SpawnPoint";

    NetworkManager nm;

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

    void Start()
    {
        nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.ConnectionApprovalCallback += Approval;
    }

    public void SubscribeSceneManager()
    {
        nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
    }

    void OnDestroy()
    {
        if (nm == null) return;
        nm.ConnectionApprovalCallback -= Approval;

        if (nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
    }

    void Approval(NetworkManager.ConnectionApprovalRequest req,
                  NetworkManager.ConnectionApprovalResponse res)
    {
        res.Approved = true;
        res.CreatePlayerObject = false;
    }

    void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!nm.IsServer) return;

        if (SceneManager.GetActiveScene().name != gameplaySceneName)
            return;

        SpawnAllPlayers();
    }

    void SpawnAllPlayers()
    {
        GameObject parent = GameObject.FindGameObjectWithTag(spawnPointParentTag);
        if (parent == null)
        {
            Debug.LogError("No spawn point parent found with tag: " + spawnPointParentTag);
            return;
        }

        List<Transform> spawnPoints = new List<Transform>();
        foreach (Transform t in parent.transform)
            spawnPoints.Add(t);

        if (spawnPoints.Count == 0)
        {
            Debug.LogError("No child spawn points found");
            return;
        }

        int index = 0;
        foreach (ulong clientId in nm.ConnectedClientsIds)
        {
            // skip if already spawned
            if (nm.SpawnManager.GetPlayerNetworkObject(clientId) != null)
                continue;

            Transform sp = spawnPoints[index % spawnPoints.Count];
            index++;

            GameObject player = Instantiate(
                nm.NetworkConfig.PlayerPrefab, sp.position, sp.rotation);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
    }
}
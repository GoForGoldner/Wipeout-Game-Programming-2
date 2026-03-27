using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public string spawnPointParentTag = "SpawnPoint";

    NetworkManager nm;

    GameObject playerPrefab;
    void Start()
    {
        nm = NetworkManager.Singleton;
        if (nm == null) return;

        playerPrefab = nm.NetworkConfig.PlayerPrefab;  // save it
        nm.NetworkConfig.PlayerPrefab = null;           // prevent auto-spawn
        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback += Approval;
        nm.OnClientConnectedCallback += OnClientConnected;
    }

    void OnDestroy()
    {
        if (nm == null) return;
        nm.ConnectionApprovalCallback -= Approval;
        nm.OnClientConnectedCallback -= OnClientConnected;
    }

    void Approval(NetworkManager.ConnectionApprovalRequest req,
                  NetworkManager.ConnectionApprovalResponse res)
    {
        res.Approved = true;
        res.CreatePlayerObject = false;
    }

    void OnClientConnected(ulong clientId)
    {
        if (!nm.IsServer) return;

        // Gather spawn points from tagged parent
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

        // Pick a spawn point based on how many players are already spawned
        int index = (int)(nm.ConnectedClientsIds.Count - 1) % spawnPoints.Count;
        Transform sp = spawnPoints[index];

        //spawn

        GameObject player = Instantiate(playerPrefab, sp.position, sp.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
    }
}
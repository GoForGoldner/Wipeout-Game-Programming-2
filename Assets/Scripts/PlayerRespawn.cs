using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn")]
    public string spawnPointParentTag = "SpawnPoint";
    public float respawnDelay = 0.5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip deathClip;
    [Range(0f, 1f)] public float deathVolume = 1f;

    [Header("Optional References")]
    public CharacterController characterController;

    private bool isRespawning = false;

    public void Die()
    {
        if (isRespawning) return;

        // Eliminated players shouldn't respawn - they're just spectating.
        var netObj = GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && MatchManager.Instance != null
            && MatchManager.Instance.IsEliminated(netObj.OwnerClientId))
        {
            return;
        }

        StartCoroutine(RespawnRoutine());
    }
    Transform GetRandomSpawnPoint()
    {
        GameObject parent = GameObject.FindGameObjectWithTag(spawnPointParentTag);
        if (parent == null)
        {
            Debug.LogError("No parent found with tag: " + spawnPointParentTag);
            return null;
        }

        List<Transform> spawnPoints = new List<Transform>();
        foreach (Transform t in parent.transform)
            spawnPoints.Add(t);

        Debug.Log("Found " + spawnPoints.Count + " spawn points");

        if (spawnPoints.Count == 0) return null;

        return spawnPoints[Random.Range(0, spawnPoints.Count)];
    }

    IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip, deathVolume);

        if (characterController != null)
            characterController.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        Transform sp = GetRandomSpawnPoint();
        if (sp != null)
        {
            transform.position = sp.position;
            transform.rotation = sp.rotation;
        }

        if (characterController != null)
            characterController.enabled = true;

        isRespawning = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DeathlyTrap"))
        {
            Die();
        }
    }
}
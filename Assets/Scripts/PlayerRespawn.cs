using System.Collections;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn")]
    public Transform respawnPoint;
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
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        isRespawning = true;

        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip, deathVolume);

        if (characterController != null)
            characterController.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint != null)
            transform.position = respawnPoint.position;

        if (respawnPoint != null)
            transform.rotation = respawnPoint.rotation;

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
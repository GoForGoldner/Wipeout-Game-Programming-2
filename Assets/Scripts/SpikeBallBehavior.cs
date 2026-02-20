using UnityEngine;

public class SpikeBallBehavior : MonoBehaviour
{
    public string requiredTag = "KillZone";

    void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag))
            Destroy(gameObject);
    }
}


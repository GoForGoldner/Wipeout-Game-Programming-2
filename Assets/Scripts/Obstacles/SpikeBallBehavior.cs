using UnityEngine;

public class SpikeBallBehavior : MonoBehaviour
{
    public string requiredTag = "KillZone";
    public float lifeTime = 4f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag))
            Destroy(gameObject);
    }
}
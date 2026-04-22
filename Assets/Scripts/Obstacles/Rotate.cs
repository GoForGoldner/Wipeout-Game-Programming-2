using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Rotate : MonoBehaviour
{
    public float speed = 100f;

    void Update()
    {
        transform.Rotate(0f, speed * Time.deltaTime, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();
        if (respawn != null)
            respawn.Die();
    }
}

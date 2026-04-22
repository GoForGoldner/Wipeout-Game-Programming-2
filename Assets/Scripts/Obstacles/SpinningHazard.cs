using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpinningHazard : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationSpeed = 720f;

    [Header("Translation")]
    public float zMin = -5f;
    public float zMax = 5f;
    public float moveSpeed = 3f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime, 0f, 0f, Space.Self);

        float t = Mathf.PingPong(Time.time * moveSpeed, 1f);
        float z = Mathf.Lerp(zMin, zMax, t);
        transform.localPosition = new Vector3(startPosition.x, startPosition.y, startPosition.z + z);
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();
        if (respawn != null)
            respawn.Die();
    }
}

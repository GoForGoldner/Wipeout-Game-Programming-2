using UnityEngine;

public class Hammer : MonoBehaviour
{
    public float maxAngle = 45f;     // degrees left/right
    public float speed = 1.5f;       // swings per second-ish
    public Vector3 localAxis = Vector3.forward; // change if needed

    Quaternion startRot;

    void Start()
    {
        startRot = transform.localRotation;
    }

    void Update()
    {
        float angle = maxAngle * Mathf.Sin(Time.time * speed);
        transform.localRotation = startRot * Quaternion.AngleAxis(angle, localAxis);
    }
}
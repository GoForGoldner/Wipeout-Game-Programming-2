using UnityEngine;

public class Hammer : MonoBehaviour
{
    public float maxAngle = 45f;
    public float speed = 1.5f;
    public Vector3 localAxis = Vector3.forward;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip whooshClip;
    [Range(0f, 1f)] public float whooshVolume = 0.5f;

    private Quaternion startRot;
    private float prevSinValue;

    void Start()
    {
        startRot = transform.localRotation;
    }

    void Update()
    {
        float sinValue = Mathf.Sin(Time.time * speed);
        float angle = maxAngle * sinValue;

        transform.localRotation = startRot * Quaternion.AngleAxis(angle, localAxis);

        DetectCenterCrossingAndPlaySound(sinValue);
        prevSinValue = sinValue;
    }

    void DetectCenterCrossingAndPlaySound(float sinValue)
    {
        if (audioSource == null || whooshClip == null) return;

        bool crossedCenter =
            (prevSinValue < 0f && sinValue >= 0f) ||
            (prevSinValue > 0f && sinValue <= 0f);

        if (crossedCenter)
        {
            PlayWhoosh();
        }
    }

    void PlayWhoosh()
    {
        audioSource.PlayOneShot(whooshClip, whooshVolume);
    }
}
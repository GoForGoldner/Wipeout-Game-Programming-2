using UnityEngine;

public class SmartHammer : MonoBehaviour
{
    public enum State { Idle, Alerted, Aggressive }

    [Header("Swing")]
    public float maxAngle = 45f;
    public float baseSpeed = 1.5f;
    public Vector3 localAxis = Vector3.forward;

    [Header("Sensor")]
    [Tooltip("Distance below the pivot (along world -Y) where the sensor sphere sits. Roughly the length of the hammer arm.")]
    public float sensorYOffset = 3f;

    [Header("AI")]
    public float alertRange = 8f;
    public float aggressiveRange = 4f;
    [Tooltip("Extra distance added before the hammer drops a state. Prevents flicker.")]
    public float hysteresis = 1.5f;
    public float alertedSpeedMultiplier = 1.5f;
    public float aggressiveSpeedMultiplier = 2.5f;
    public float scanInterval = 0.25f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip whooshClip;
    [Range(0f, 1f)] public float whooshVolume = 0.5f;

    Quaternion startRot;
    float prevSinValue;
    float swingTime;
    float nextScanTime;
    float currentSpeedMultiplier = 1f;
    State currentState = State.Idle;

    Vector3 GetSensorPos()
    {
        Transform parent = transform.parent;
        if (parent != null)
            return parent.TransformPoint(transform.localPosition + Vector3.down * sensorYOffset);
        return transform.position + Vector3.down * sensorYOffset;
    }

    void Start()
    {
        startRot = transform.localRotation;
    }

    void Update()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            UpdateAIState();
        }

        swingTime += Time.deltaTime * baseSpeed * currentSpeedMultiplier;

        float sinValue = Mathf.Sin(swingTime);
        float angle = maxAngle * sinValue;
        transform.localRotation = startRot * Quaternion.AngleAxis(angle, localAxis);

        DetectCenterCrossingAndPlaySound(sinValue);
        prevSinValue = sinValue;
    }

    void UpdateAIState()
    {
        Vector3 sensorPos = GetSensorPos();

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        float nearestDistSq = float.MaxValue;
        bool foundAny = false;

        foreach (var p in players)
        {
            if (!p.gameObject.activeInHierarchy) continue;
            float distSq = (p.transform.position - sensorPos).sqrMagnitude;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                foundAny = true;
            }
        }

        float nearestDist = foundAny ? Mathf.Sqrt(nearestDistSq) : float.MaxValue;

        // Hysteresis: use larger ranges to LEAVE a state than to enter it.
        float effectiveAlert = currentState == State.Idle ? alertRange : alertRange + hysteresis;
        float effectiveAggressive = currentState == State.Aggressive
            ? aggressiveRange + hysteresis
            : aggressiveRange;

        if (!foundAny || nearestDist > effectiveAlert)
        {
            currentState = State.Idle;
            currentSpeedMultiplier = 1f;
        }
        else if (nearestDist > effectiveAggressive)
        {
            currentState = State.Alerted;
            currentSpeedMultiplier = alertedSpeedMultiplier;
        }
        else
        {
            currentState = State.Aggressive;
            currentSpeedMultiplier = aggressiveSpeedMultiplier;
        }
    }

    void DetectCenterCrossingAndPlaySound(float sinValue)
    {
        if (audioSource == null || whooshClip == null) return;

        bool crossedCenter =
            (prevSinValue < 0f && sinValue >= 0f) ||
            (prevSinValue > 0f && sinValue <= 0f);

        if (crossedCenter) audioSource.PlayOneShot(whooshClip, whooshVolume);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 sensorPos = GetSensorPos();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sensorPos, alertRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(sensorPos, aggressiveRange);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, sensorPos);
    }
}
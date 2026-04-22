using UnityEngine;

/// <summary>
/// Smart version of SpinningHazard. Watches for nearby players and adjusts
/// behavior across three states: Idle (slow), Alerted (faster), Aggressive
/// (fastest + targets the player's side of the path).
/// </summary>
[RequireComponent(typeof(Collider))]
public class SmartSpinningHazard : MonoBehaviour
{
    public enum State { Idle, Alerted, Aggressive }

    [Header("Rotation")]
    public float baseRotationSpeed = 720f;

    [Header("Translation")]
    public float zMin = -5f;
    public float zMax = 5f;
    public float baseMoveSpeed = 3f;

    [Header("AI")]
    public float alertRange = 8f;
    public float aggressiveRange = 4f;
    public float alertedMultiplier = 1.5f;
    public float aggressiveMultiplier = 2.5f;
    [Tooltip("How quickly to bias toward the player's side of the path. 0 = no targeting.")]
    [Range(0f, 1f)] public float aggressivePathBias = 0.5f;
    public float scanInterval = 0.25f;

    Vector3 startPosition;
    float pingPongTime;
    float nextScanTime;
    float currentMultiplier = 1f;
    float currentTargetT = -1f; // -1 means use ping-pong default
    State currentState = State.Idle;

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void Update()
    {
        // ── AI scan ──
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            UpdateAIState();
        }

        // ── Rotation ──
        transform.Rotate(baseRotationSpeed * currentMultiplier * Time.deltaTime, 0f, 0f, Space.Self);

        // ── Translation ──
        pingPongTime += Time.deltaTime * baseMoveSpeed * currentMultiplier;

        float t = currentTargetT >= 0f
            ? Mathf.Lerp(GetCurrentT(), currentTargetT, Time.deltaTime * 2f * currentMultiplier)
            : Mathf.PingPong(pingPongTime, 1f);

        float z = Mathf.Lerp(zMin, zMax, t);
        transform.localPosition = new Vector3(startPosition.x, startPosition.y, startPosition.z + z);
    }

    float GetCurrentT()
    {
        float currentZ = transform.localPosition.z - startPosition.z;
        return Mathf.InverseLerp(zMin, zMax, currentZ);
    }

    void UpdateAIState()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Transform nearestPlayer = null;
        float nearestDistSq = float.MaxValue;

        foreach (var p in players)
        {
            if (!p.enabled) continue;
            float distSq = (p.transform.position - transform.position).sqrMagnitude;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearestPlayer = p.transform;
            }
        }

        float nearestDist = Mathf.Sqrt(nearestDistSq);

        if (nearestPlayer == null || nearestDist > alertRange)
        {
            currentState = State.Idle;
            currentMultiplier = 1f;
            currentTargetT = -1f; // resume normal ping-pong
        }
        else if (nearestDist > aggressiveRange)
        {
            currentState = State.Alerted;
            currentMultiplier = alertedMultiplier;
            currentTargetT = -1f;
        }
        else
        {
            currentState = State.Aggressive;
            currentMultiplier = aggressiveMultiplier;

            // Target the player's z position along the hazard's path.
            // Convert the player's world position into the same local space as startPosition.
            Vector3 playerLocal = transform.parent != null
                ? transform.parent.InverseTransformPoint(nearestPlayer.position)
                : nearestPlayer.position;

            float playerZOffset = playerLocal.z - startPosition.z;
            float targetT = Mathf.InverseLerp(zMin, zMax, playerZOffset);

            // Blend between current ping-pong and player target.
            float pingPongT = Mathf.PingPong(pingPongTime, 1f);
            currentTargetT = Mathf.Lerp(pingPongT, targetT, aggressivePathBias);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();
        if (respawn != null)
            respawn.Die();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alertRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, aggressiveRange);
    }
}
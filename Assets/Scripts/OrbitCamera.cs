using UnityEngine;
using UnityEngine.InputSystem;

// Attach to CameraPivot. Main Camera is a child at local position (0, 0, -distance).
public class OrbitCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public PlayerController playerController;
    public Camera cam;

    [Header("Orbit")]
    public float lookSpeedX = 120f;
    public float lookSpeedY = 80f;
    public float verticalMin = -70f;
    public float verticalMax = 70f;
    public float distance = 4f;

    [Header("Follow")]
    public float followSpeed = 6f;
    public float verticalFollowSpeed = 3f;
    public float jumpFollowSpeed = 8f;

    [Header("Roll")]
    public float rollSpeed = 8f;

    [Header("Collision")]
    public float collisionRadius = 0.3f;
    public LayerMask collisionMask;

    // Matches Quaternion.AngleAxis(step * 90f, Vector3.right)
    static readonly Vector3[] GravityUp =
    {
        Vector3.up,      // 0 – normal
        Vector3.forward, // 1 – 90° around X
        Vector3.down,    // 2 – 180° around X
        Vector3.back,    // 3 – 270° around X
    };

    float yaw;
    float pitch = 17.5f;
    float currentRoll;
    Vector3 currentPivotPos;

    void Awake()
    {
        if (target) currentPivotPos = target.position;
        if (!cam) cam = GetComponentInChildren<Camera>();
    }

    void LateUpdate()
    {
        if (target == null || Mouse.current == null) return;

        int step = playerController != null ? playerController.gravityStepIndex : 0;
        Vector3 gravityUp = GravityUp[step];
        bool inAir = playerController != null && !playerController.isGrounded;

        // ── Pivot follow ──────────────────────────────────────────────────────
        // Split follow into "along gravity" vs "perpendicular to gravity".
        // The gravity-axis follow gets the floaty vertical speed;
        // the two perpendicular axes get the snappier followSpeed.

        Vector3 targetPos = target.position;
        Vector3 toTarget = targetPos - currentPivotPos;

        // Component along gravity-up (the "vertical" from the player's perspective)
        float gravComp = Vector3.Dot(toTarget, gravityUp);
        Vector3 gravPart = gravityUp * gravComp;

        // Remaining horizontal component
        Vector3 horizPart = toTarget - gravPart;

        float vSpeed = inAir ? jumpFollowSpeed : verticalFollowSpeed;

        bool justLanded = !inAir && (playerController != null && playerController.wasGrounded == false);

        currentPivotPos += horizPart * (followSpeed * Time.deltaTime);

        if (inAir || justLanded)
            currentPivotPos += gravPart;
        else
            currentPivotPos += gravPart * (verticalFollowSpeed * Time.deltaTime);

        transform.position = currentPivotPos;

        // ── Roll ──────────────────────────────────────────────────────────────
        float targetRoll = step * -90f;
        currentRoll = Mathf.LerpAngle(currentRoll, targetRoll, rollSpeed * Time.deltaTime);

        // ── Mouse input (remapped per gravity step) ───────────────────────────
        Vector2 delta = Mouse.current.delta.ReadValue() * Time.deltaTime;
        switch (step)
        {
            case 0: yaw += delta.x * lookSpeedX; pitch -= delta.y * lookSpeedY; break;
            case 1: yaw -= delta.y * lookSpeedX; pitch -= delta.x * lookSpeedY; break;
            case 2: yaw -= delta.x * lookSpeedX; pitch += delta.y * lookSpeedY; break;
            case 3: yaw += delta.y * lookSpeedX; pitch += delta.x * lookSpeedY; break;
        }
        pitch = Mathf.Clamp(pitch, verticalMin, verticalMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, currentRoll);
        transform.rotation = rotation;

        // ── Collision ─────────────────────────────────────────────────────────
        Vector3 desiredCamPos = transform.position + rotation * new Vector3(0f, 0f, -distance);
        Vector3 dir = (desiredCamPos - transform.position).normalized;
        float desiredDist = distance;

        if (Physics.SphereCast(transform.position, collisionRadius, dir,
                               out RaycastHit hit, distance, collisionMask))
            desiredDist = Mathf.Clamp(hit.distance - collisionRadius, 0.5f, distance);

        if (cam)
            cam.transform.localPosition = new Vector3(0f, 0f, -desiredDist);
    }
}
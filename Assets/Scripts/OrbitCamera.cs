using UnityEngine;
using UnityEngine.InputSystem;

// Attach to CameraPivot. Main Camera is a child.
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
    float sensitivityMultiplier = 1f;

    [Header("Follow")]
    public float followSpeed = 6f;
    public float verticalFollowSpeed = 3f;
    public float jumpFollowSpeed = 8f;

    [Header("Gravity Transition")]
    public float gravTransitionSpeed = 5f;

    [Header("Smoothing")]
    public int inputSmoothFrames = 3;

    [Header("Collision")]
    public float collisionRadius = 0.3f;
    public LayerMask collisionMask;

    static readonly Vector3[] GravityUp =
    {
        Vector3.up,
        Vector3.forward,
        Vector3.down,
        Vector3.back,
    };

    Vector3 smoothGravUp;
    Vector3 camHorizDir;
    float pitch = 17.5f;
    int lastStep;
    Vector2[] inputBuffer;
    int inputBufferIndex;
    Vector3 currentPivotPos;

    void Awake()
    {
        if (target) currentPivotPos = target.position;
        if (!cam) cam = GetComponentInChildren<Camera>();
        inputBuffer = new Vector2[Mathf.Max(1, inputSmoothFrames)];
        smoothGravUp = GravityUp[0];
        camHorizDir = Vector3.forward;

        if (PlayerProgressManager.Instance != null)
        {
            sensitivityMultiplier = PlayerProgressManager.Instance.Data.settings.mouseSensitivity;
            PlayerProgressManager.Instance.OnProgressUpdated += OnSettingsUpdated;
        }
    }

    void OnDestroy()
    {
        if (PlayerProgressManager.Instance != null)
            PlayerProgressManager.Instance.OnProgressUpdated -= OnSettingsUpdated;
    }

    void OnSettingsUpdated()
    {
        sensitivityMultiplier = PlayerProgressManager.Instance.Data.settings.mouseSensitivity;
    }

    void LateUpdate()
    {
        if (target == null || Mouse.current == null) return;

        int step = playerController != null ? playerController.gravityStepIndex : 0;
        Vector3 gravUp = GravityUp[step];

        if (step != lastStep)
        {
            System.Array.Clear(inputBuffer, 0, inputBuffer.Length);
            inputBufferIndex = 0;
            lastStep = step;
        }

        smoothGravUp = Vector3.Slerp(smoothGravUp, gravUp, gravTransitionSpeed * Time.deltaTime);
        Vector3 reprojected = Vector3.ProjectOnPlane(camHorizDir, smoothGravUp);
        camHorizDir = reprojected.sqrMagnitude > 0.001f
            ? reprojected.normalized
            : SafeRefFwd(smoothGravUp);

        bool inAir = playerController != null && !playerController.isGrounded;

        Vector3 toTarget = target.position - currentPivotPos;
        float gravComp = Vector3.Dot(toTarget, gravUp);
        Vector3 gravPart = gravUp * gravComp;
        Vector3 horizPart = toTarget - gravPart;

        float vSpeed = inAir ? jumpFollowSpeed : verticalFollowSpeed;
        bool justLanded = !inAir && playerController != null && !playerController.wasGrounded;

        currentPivotPos += horizPart * (followSpeed * Time.deltaTime);
        bool canSnap = step == 0 && (inAir || justLanded);
        currentPivotPos += canSnap ? gravPart : gravPart * (vSpeed * Time.deltaTime);

        transform.position = currentPivotPos;

        Vector2 rawDelta = Mouse.current.delta.ReadValue() * (1f / 60f);
        inputBuffer[inputBufferIndex % inputBuffer.Length] = rawDelta;
        inputBufferIndex++;

        Vector2 smoothed = Vector2.zero;
        foreach (var v in inputBuffer) smoothed += v;
        smoothed /= inputBuffer.Length;

        camHorizDir = Quaternion.AngleAxis(smoothed.x * lookSpeedX * sensitivityMultiplier, smoothGravUp) * camHorizDir;
        camHorizDir = Vector3.ProjectOnPlane(camHorizDir, smoothGravUp).normalized;

        pitch -= smoothed.y * lookSpeedY * sensitivityMultiplier;
        pitch = Mathf.Clamp(pitch, verticalMin, verticalMax);

        Vector3 camRight = Vector3.Cross(smoothGravUp, camHorizDir).normalized;
        Vector3 camDir = Quaternion.AngleAxis(pitch, camRight) * (-camHorizDir);

        transform.rotation = Quaternion.LookRotation(camHorizDir, smoothGravUp);

        if (cam)
        {
            cam.transform.position = currentPivotPos + camDir * distance;
            cam.transform.rotation = Quaternion.LookRotation(-camDir, smoothGravUp);
        }
    }

    static Vector3 SafeRefFwd(Vector3 up)
    {
        Vector3 v = Mathf.Abs(Vector3.Dot(Vector3.forward, up)) < 0.9f
            ? Vector3.ProjectOnPlane(Vector3.forward, up)
            : Vector3.ProjectOnPlane(Vector3.right, up);
        return v.normalized;
    }
}

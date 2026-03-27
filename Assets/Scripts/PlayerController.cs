using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference moveActionRef;
    public InputActionReference jumpActionRef;
    public InputActionReference rotateLeftActionRef;
    public InputActionReference rotateRightActionRef;

    [Header("References")]
    public Transform cameraPivot;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 20f;
    public float deceleration = 25f;
    public float rotationSpeed = 12f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 1.8f;
    public float gravity = -28f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;
    public float fallMultiplier = 2f;

    [Header("Dive")]
    public float diveForwardSpeed = 10f;
    public float diveDownSpeed = -4f;

    [Header("Animation")]
    public Animator animator;
    public string animIdle = "Idle";
    public string animRunFwd = "RunFWD";
    public string animRunBwd = "RunBWD";
    public string animRunLeft = "RunLeft";
    public string animRunRight = "RunRight";
    public string animDive = "Slide";
    public string animJump = "Jump";
    public string animFall = "Jump"; // set to a Fall state name if you have one

    [Header("Ground Check")]
    public LayerMask collisionMask = ~0; // default: everything

    [Header("Gravity Flip")]
    public bool gravityFlipEnabled = true;
    public float flipRotateSpeed = 720f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip jumpClip;
    [Range(0f, 1f)] public float jumpVolume = 1f;

    // Gravity-up vectors per step (step 0 = normal, 1 = right wall, 2 = ceiling, 3 = left wall)
    static readonly Vector3[] GravityUp =
    {
        Vector3.up,
        Vector3.left,
        Vector3.down,
        Vector3.right,
    };

    CharacterController cc;
    Vector3 localVelocity;
    Vector3 currentLocalMove;
    Vector3 worldDiveDir;
    float lastGroundedTime = -999f;
    float lastJumpPressedTime = -999f;

    string currentAnim;
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool wasGrounded;
    bool isDiving;
    bool hasJumped;
    float diveEndTime = -999f;

    public int gravityStepIndex;
    Quaternion gravityTargetRot;
    bool isFlipping;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        gravityTargetRot = transform.rotation;
        LockCursor();
    }

    void OnEnable()
    {
        moveActionRef?.action.Enable();
        jumpActionRef?.action.Enable();
        rotateLeftActionRef?.action.Enable();
        rotateRightActionRef?.action.Enable();
    }

    void OnDisable()
    {
        moveActionRef?.action.Disable();
        jumpActionRef?.action.Disable();
        rotateLeftActionRef?.action.Disable();
        rotateRightActionRef?.action.Disable();
    }

    void Update()
    {
        HandleCursor();
        HandleFlipInput();
        AnimateFlip();
        CheckGrounded();
        HandleJumpInput();
        Move();
    }

    // ── Grounded ──────────────────────────────────────────────────────────────

    void CheckGrounded()
    {
        wasGrounded = isGrounded;

        // Raycast along the player's local down (gravity direction)
        // cc.isGrounded only works for world-down gravity, so always use the raycast.
        // Start the ray from slightly inside the capsule bottom to avoid self-hit,
        // and use a layer mask so it never hits the player itself.
        Vector3 rayOrigin = transform.position + transform.up * 0.05f;
        isGrounded = cc.isGrounded ||
            Physics.Raycast(rayOrigin, -transform.up, cc.height / 2f + 0.2f, collisionMask);

        if (isGrounded && !wasGrounded)
        {
            if (isDiving)
            {
                diveEndTime = Time.time + 0.5f;
                PlayAnim(animDive, 0f);
            }
            hasJumped = false;
        }

        // Clear dive once the landing hold time expires
        if (isDiving && isGrounded && Time.time >= diveEndTime)
            isDiving = false;

        if (isGrounded)
            lastGroundedTime = Time.time;
    }

    // ── Jump ──────────────────────────────────────────────────────────────────

    void HandleJumpInput()
    {
        if (jumpActionRef == null) return;

        if (jumpActionRef.action.WasPressedThisFrame())
        {
            if (isGrounded || (Time.time - lastGroundedTime) <= coyoteTime)
            {
                localVelocity.y = Mathf.Sqrt(2f * (-gravity) * jumpHeight);
                lastJumpPressedTime = -999f;
                lastGroundedTime = -999f;
                hasJumped = true;
                isDiving = false;
                PlayAnim(animJump, 0f);
                PlayJumpSound();
            }
            else if (hasJumped && !isDiving)
            {
                isDiving = true;
                hasJumped = false;
                worldDiveDir = transform.forward * diveForwardSpeed;
                localVelocity.y = diveDownSpeed;
            }
            else
            {
                lastJumpPressedTime = Time.time;
            }
        }

        if (!isGrounded
            && (Time.time - lastJumpPressedTime) <= jumpBuffer
            && (Time.time - lastGroundedTime) <= coyoteTime)
        {
            localVelocity.y = Mathf.Sqrt(2f * (-gravity) * jumpHeight);
            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
            hasJumped = true;
            PlayJumpSound();
        }
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    void Move()
    {
        if (cc == null || !cc.enabled) return;

        Vector2 moveValue = moveActionRef != null
            ? moveActionRef.action.ReadValue<Vector2>()
            : Vector2.zero;

        Vector3 up = transform.up; // always gravity-relative

        Vector3 moveDir = Vector3.zero;
        if (cameraPivot && moveValue.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cameraPivot.forward, up).normalized;
            Vector3 camRight = Vector3.Cross(up, camForward).normalized;
            moveDir = (camRight * moveValue.x + camForward * moveValue.y).normalized * moveValue.magnitude;
        }

        Vector3 targetWorldMove = moveDir * moveSpeed;

        // Block horizontal movement during dive landing recovery
        bool inDiveRecovery = isDiving && isGrounded;
        if (inDiveRecovery) targetWorldMove = Vector3.zero;
        Vector3 currentWorldMove = transform.TransformDirection(
            new Vector3(currentLocalMove.x, 0f, currentLocalMove.z));

        if (!isDiving)
        {
            float rate = moveDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
            currentWorldMove = Vector3.MoveTowards(currentWorldMove, targetWorldMove, rate * Time.deltaTime);
        }
        else
        {
            currentWorldMove = worldDiveDir;
        }

        Vector3 localHorizontal = transform.InverseTransformDirection(currentWorldMove);
        currentLocalMove = new Vector3(localHorizontal.x, 0f, localHorizontal.z);

        // Only rotate toward movement when not flipping
        if (!isFlipping && currentWorldMove.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(currentWorldMove, up),
                rotationSpeed * Time.deltaTime);
        }

        if (isGrounded && localVelocity.y < 0f)
        {
            localVelocity.y = -2f;
        }
        else if (!isGrounded)
        {
            float multiplier = localVelocity.y < 0f ? fallMultiplier : 1f;
            localVelocity.y += gravity * multiplier * Time.deltaTime;
        }
        Vector3 verticalWorld = up * localVelocity.y;
        cc.Move((currentWorldMove + verticalWorld) * Time.deltaTime);

        UpdateAnimation(moveValue, currentWorldMove);
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    void PlayAnim(string anim, float crossFade = 0.1f)
    {
        if (anim == currentAnim) return;
        currentAnim = anim;
        animator.CrossFadeInFixedTime(anim, crossFade);
    }

    void PlayJumpSound()
    {
        //Debug.Log("PlayJumpSound called");

        if (audioSource == null)
        {
            //Debug.LogWarning("AudioSource is NULL");
            return;
        }

        if (jumpClip == null)
        {
            //Debug.LogWarning("jumpClip is NULL");
            return;
        }

        audioSource.PlayOneShot(jumpClip, jumpVolume);
    }

    void UpdateAnimation(Vector2 moveValue, Vector3 currentWorldMove)
    {
        if (animator == null) return;

        // Don't interrupt animation during gravity flip rotation
        if (isFlipping) return;

        if (isDiving) { PlayAnim(animDive); return; }
        if (!isGrounded) { PlayAnim(animJump, 0f); return; }
        if (moveValue.sqrMagnitude < 0.01f) { PlayAnim(animIdle); return; }

        // currentWorldMove is already in world space; project to local for dir check.
        // transform.up is always the gravity-relative up, so InverseTransformDirection
        // gives us forward/right relative to the player's current gravity orientation.
        Vector3 localMove = transform.InverseTransformDirection(currentWorldMove);
        float absX = Mathf.Abs(localMove.x);
        float absZ = Mathf.Abs(localMove.z);

        string target;
        if (absZ >= absX)
            target = localMove.z >= 0 ? animRunFwd : animRunBwd;
        else
            target = localMove.x >= 0 ? animRunRight : animRunLeft;

        PlayAnim(target);
    }

    // ── Gravity Flip ──────────────────────────────────────────────────────────

    void HandleFlipInput()
    {
        if (!gravityFlipEnabled || isFlipping) return;

        bool left = rotateLeftActionRef != null && rotateLeftActionRef.action.WasPressedThisFrame();
        bool right = rotateRightActionRef != null && rotateRightActionRef.action.WasPressedThisFrame();

        if (left) FlipStep(-1);
        if (right) FlipStep(+1);
    }

    void FlipStep(int dir)
    {
        gravityStepIndex = (gravityStepIndex + dir + 4) % 4;

        // Rotate around the world X axis so gravity cycles: down→right→up→left
        gravityTargetRot = Quaternion.AngleAxis(gravityStepIndex * 90f, Vector3.right);

        localVelocity.y = 0f;
        isFlipping = true;

        // Play jump/fall anim to signal the transition
        PlayAnim(animJump, 0.05f);
    }

    void AnimateFlip()
    {
        if (!isFlipping) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, gravityTargetRot, flipRotateSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, gravityTargetRot) < 0.5f)
        {
            transform.rotation = gravityTargetRot;
            isFlipping = false;
            // Force grounded re-check next frame so animation snaps cleanly
            lastGroundedTime = -999f;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetGravityFlipEnabled(bool enabled) => gravityFlipEnabled = enabled;

    public void ResetGravityOrientation()
    {
        gravityStepIndex = 0;
        gravityTargetRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        localVelocity.y = 0f;
        isFlipping = false;
        isDiving = false;
        hasJumped = false;
        transform.rotation = gravityTargetRot;
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();
    }

    void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    void UnlockCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
}
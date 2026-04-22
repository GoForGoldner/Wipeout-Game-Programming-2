using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
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
    public string animFall = "Jump";

    [Header("Ground Check")]
    public LayerMask collisionMask = ~0;

    [Header("Gravity Flip")]
    public bool gravityFlipEnabled = true;
    public float flipRotateSpeed = 720f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip jumpClip;
    [Range(0f, 1f)] public float jumpVolume = 1f;

    [Header("Anti Gravity")]
    public float antiGravityJumpHeight = 3.0f;
    public float antiGravityGravity = -12f;
    public float antiGravityFallMultiplier = 1.0f;

    InputAction moveAction;

    static readonly Vector3[] GravityUp =
    {
        Vector3.up,
        Vector3.forward,
        Vector3.down,
        Vector3.back,
    };

    CharacterController cc;
    Vector3 localVelocity;
    Vector3 currentLocalMove;
    Vector3 worldDiveDir;
    float lastGroundedTime = -999f;
    float lastJumpPressedTime = -999f;
    float normalJumpHeight;
    float normalGravity;
    float normalFallMultiplier;
    float normalMoveSpeed;
    float normalDiveForwardSpeed;
    float normalAntiGravityJumpHeight;
    float normalAntiGravityGravity;

    float baseMoveSpeed;
    float baseJumpHeight;
    float baseGravity;
    float baseFallMultiplier;
    float baseDiveForwardSpeed;
    float baseAntiGravityJumpHeight;
    float baseAntiGravityGravity;

    bool antiGravityActive;

    string currentAnim;
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool wasGrounded;
    [HideInInspector] public bool isGravityGrounded;
    bool isDiving;
    bool hasJumped;
    bool hasFlippedInAir;
    float diveEndTime = -999f;

    bool groundedByCollider;

    public int gravityStepIndex;
    Quaternion gravityTargetRot;
    bool isFlipping;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        gravityTargetRot = transform.rotation;

        baseMoveSpeed = moveSpeed;
        baseJumpHeight = jumpHeight;
        baseGravity = gravity;
        baseFallMultiplier = fallMultiplier;
        baseDiveForwardSpeed = diveForwardSpeed;
        baseAntiGravityJumpHeight = antiGravityJumpHeight;
        baseAntiGravityGravity = antiGravityGravity;

        normalMoveSpeed = baseMoveSpeed;
        normalJumpHeight = baseJumpHeight;
        normalGravity = baseGravity;
        normalFallMultiplier = baseFallMultiplier;
        normalDiveForwardSpeed = baseDiveForwardSpeed;
        normalAntiGravityJumpHeight = baseAntiGravityJumpHeight;
        normalAntiGravityGravity = baseAntiGravityGravity;

        if (IsOwner)
            LockCursor();
    }

    void OnEnable()
    {
        if (!IsOwner) return;

        moveActionRef?.action.Enable();
        jumpActionRef?.action.Enable();
        rotateLeftActionRef?.action.Enable();
        rotateRightActionRef?.action.Enable();
    }

    void OnDisable()
    {
        if (!IsOwner) return;

        moveActionRef?.action.Disable();
        jumpActionRef?.action.Disable();
        rotateLeftActionRef?.action.Disable();
        rotateRightActionRef?.action.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleCursor();
        HandleFlipInput();
        AnimateFlip();
        CheckGrounded();
        HandleJumpInput();
        Move();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsOwner) return;

        if (Vector3.Dot(hit.normal, transform.up) > 0.5f)
            groundedByCollider = true;
    }

    void CheckGrounded()
    {
        wasGrounded = isGrounded;

        isGravityGrounded = groundedByCollider;
        groundedByCollider = false;

        isGrounded = cc.isGrounded || isGravityGrounded;

        if (isGrounded && !wasGrounded)
        {
            if (isDiving)
            {
                diveEndTime = Time.time + 0.5f;
                PlayAnim(animDive, 0f);
            }
            hasJumped = false;
            hasFlippedInAir = false;
            currentAnim = "";
        }

        if (isDiving && isGrounded && Time.time >= diveEndTime)
            isDiving = false;

        if (isGrounded)
            lastGroundedTime = Time.time;
    }

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

    void Move()
    {
        if (cc == null || !cc.enabled) return;

        Vector2 moveValue = moveActionRef != null
            ? moveActionRef.action.ReadValue<Vector2>()
            : Vector2.zero;

        Vector3 up = transform.up;

        Vector3 moveDir = Vector3.zero;
        if (cameraPivot && moveValue.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cameraPivot.forward, up).normalized;
            Vector3 camRight = Vector3.Cross(up, camForward).normalized;
            moveDir = (camRight * moveValue.x + camForward * moveValue.y).normalized * moveValue.magnitude;
        }

        Vector3 targetWorldMove = moveDir * moveSpeed;

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

        if (!isFlipping && currentWorldMove.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(currentWorldMove, up),
                rotationSpeed * Time.deltaTime);
        }

        if (isFlipping)
        {
            localVelocity.y = 0f;
        }
        else if (isGrounded && localVelocity.y < 0f)
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

    void PlayAnim(string anim, float crossFade = 0.1f)
    {
        if (animator == null) return;
        if (anim == currentAnim) return;

        currentAnim = anim;
        animator.CrossFadeInFixedTime(anim, crossFade);
    }

    void PlayJumpSound()
    {
        if (audioSource == null) return;
        if (jumpClip == null) return;

        audioSource.PlayOneShot(jumpClip, jumpVolume);
    }

    void UpdateAnimation(Vector2 moveValue, Vector3 currentWorldMove)
    {
        if (animator == null) return;
        if (isFlipping) return;

        if (isDiving)
        {
            PlayAnim(animDive);
            return;
        }

        if (!isGrounded)
        {
            if (Time.time - lastGroundedTime > 0.1f)
                PlayAnim(animJump, 0f);
            return;
        }

        if (moveValue.sqrMagnitude < 0.01f)
        {
            PlayAnim(animIdle);
            return;
        }

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

    void HandleFlipInput()
    {
        if (!gravityFlipEnabled || isFlipping) return;
        if (!isGrounded && hasFlippedInAir) return;

        bool left = rotateLeftActionRef != null && rotateLeftActionRef.action.WasPressedThisFrame();
        bool right = rotateRightActionRef != null && rotateRightActionRef.action.WasPressedThisFrame();

        if (left) FlipStep(-1);
        if (right) FlipStep(+1);
    }

    void FlipStep(int dir)
    {
        gravityStepIndex = (gravityStepIndex + dir + 4) % 4;

        Vector3 newUp = GravityUp[gravityStepIndex];

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, newUp);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.ProjectOnPlane(-transform.up, newUp);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.ProjectOnPlane(Vector3.right, newUp);

        gravityTargetRot = Quaternion.LookRotation(flatForward.normalized, newUp);

        if (!isGrounded) hasFlippedInAir = true;

        localVelocity.y = 0f;
        isFlipping = true;
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
            currentAnim = "";
            lastGroundedTime = -999f;
        }
    }

    public void ResetToBaseStats()
    {
        normalMoveSpeed = baseMoveSpeed;
        normalJumpHeight = baseJumpHeight;
        normalGravity = baseGravity;
        normalFallMultiplier = baseFallMultiplier;
        normalDiveForwardSpeed = baseDiveForwardSpeed;
        normalAntiGravityJumpHeight = baseAntiGravityJumpHeight;
        normalAntiGravityGravity = baseAntiGravityGravity;

        moveSpeed = normalMoveSpeed;
        diveForwardSpeed = normalDiveForwardSpeed;
        antiGravityJumpHeight = normalAntiGravityJumpHeight;
        antiGravityGravity = normalAntiGravityGravity;

        if (antiGravityActive)
        {
            jumpHeight = antiGravityJumpHeight;
            gravity = antiGravityGravity;
            fallMultiplier = antiGravityFallMultiplier;
        }
        else
        {
            jumpHeight = normalJumpHeight;
            gravity = normalGravity;
            fallMultiplier = normalFallMultiplier;
        }
    }

    public void ApplyPerk(PerkData perk)
    {
        ResetToBaseStats();

        if (perk == null)
            return;

        normalMoveSpeed += perk.speedBonus;
        normalJumpHeight += perk.jumpBonus;
        normalDiveForwardSpeed += perk.diveBonus;
        normalAntiGravityJumpHeight += perk.antiGravityJumpBonus;
        normalAntiGravityGravity += perk.antiGravityGravityBonus;

        moveSpeed = normalMoveSpeed;
        diveForwardSpeed = normalDiveForwardSpeed;
        antiGravityJumpHeight = normalAntiGravityJumpHeight;
        antiGravityGravity = normalAntiGravityGravity;

        if (antiGravityActive)
        {
            jumpHeight = antiGravityJumpHeight;
            gravity = antiGravityGravity;
            fallMultiplier = antiGravityFallMultiplier;
        }
        else
        {
            jumpHeight = normalJumpHeight;
            gravity = normalGravity;
            fallMultiplier = normalFallMultiplier;
        }

        Debug.Log("Applied perk: " + perk.perkName);
    }

    public void SetAntiGravity(bool enabled)
    {
        antiGravityActive = enabled;

        if (enabled)
        {
            jumpHeight = antiGravityJumpHeight;
            gravity = antiGravityGravity;
            fallMultiplier = antiGravityFallMultiplier;
        }
        else
        {
            jumpHeight = normalJumpHeight;
            gravity = normalGravity;
            fallMultiplier = normalFallMultiplier;
        }
    }

    public void SetGravityFlipEnabled(bool enabled) => gravityFlipEnabled = enabled;
    public void ToggleGravityFlip() => gravityFlipEnabled = !gravityFlipEnabled;

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

    void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
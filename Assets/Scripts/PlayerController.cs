using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleWipeoutController_NewInputStyle : MonoBehaviour
{
    [Header("Input (New Input System)")]
    public InputActionReference moveActionRef;
    public InputActionReference jumpActionRef;   // Button

    [Header("References")]
    public Transform cameraTransform;

    [Header("Movement")]
    public float moveSpeed = 6.5f;
    public float acceleration = 18f;
    public float rotationSpeed = 14f;

    [Header("Jump / Gravity")]
    public float jumpHeight = 1.6f;
    public float gravity = -22f;
    public float coyoteTime = 0.10f;
    public float jumpBuffer = 0.10f;

    CharacterController cc;

    Vector3 velocity;        // y velocity lives here
    Vector3 currentMove;     // smoothed horizontal velocity (x,z)

    float lastGroundedTime = -999f;
    float lastJumpPressedTime = -999f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        if (!cameraTransform && Camera.main)
            cameraTransform = Camera.main.transform;
    }

    void OnEnable()
    {
        if (moveActionRef) moveActionRef.action.Enable();
        if (jumpActionRef) jumpActionRef.action.Enable();
    }

    void OnDisable()
    {
        if (moveActionRef) moveActionRef.action.Disable();
        if (jumpActionRef) jumpActionRef.action.Disable();
    }

    void Update()
    {
        GetInputAndTimestamps(out Vector3 moveDir);

        // Smooth accel
        Vector3 targetMove = moveDir * moveSpeed;
        currentMove = Vector3.MoveTowards(currentMove, targetMove, acceleration * Time.deltaTime);

        // Rotate toward movement
        Vector3 flat = new Vector3(currentMove.x, 0f, currentMove.z);
        if (flat.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Gravity
        if (cc.isGrounded && velocity.y < 0f)
            velocity.y = -2f; // stick to ground

        velocity.y += gravity * Time.deltaTime;

        // Apply move
        Vector3 motion = new Vector3(currentMove.x, 0f, currentMove.z) + Vector3.up * velocity.y;
        cc.Move(motion * Time.deltaTime);
    }

    void GetInputAndTimestamps(out Vector3 moveDir)
    {
        // Read move (prof style)
        Vector2 moveValue = Vector2.zero;
        if (moveActionRef)
            moveValue = moveActionRef.action.ReadValue<Vector2>();

        // Read jump press (buffer)
        if (jumpActionRef && jumpActionRef.action.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;

        // Grounded timestamp (coyote)
        if (cc.isGrounded)
            lastGroundedTime = Time.time;

        // Camera-relative direction
        Vector3 input = new Vector3(moveValue.x, 0f, moveValue.y);
        input = Vector3.ClampMagnitude(input, 1f);

        moveDir = input;
        if (cameraTransform)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            moveDir = right * input.x + forward * input.z;
            moveDir = Vector3.ClampMagnitude(moveDir, 1f);
        }

        // Jump (buffer + coyote)
        bool canCoyoteJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool bufferedJump = (Time.time - lastJumpPressedTime) <= jumpBuffer;

        if (bufferedJump && canCoyoteJump)
        {
            velocity.y = Mathf.Sqrt(2f * (-gravity) * jumpHeight);
            lastJumpPressedTime = -999f; // consume
            lastGroundedTime = -999f;    // consume
        }
    }
}
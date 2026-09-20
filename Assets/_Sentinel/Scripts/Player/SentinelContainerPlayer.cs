using UnityEngine;
using UnityEngine.InputSystem;

// The root is at the feet, at unit scale. Only the visual child turns/scales.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
public sealed class SentinelContainerPlayer : MonoBehaviour
{
    public Transform movementReference;
    public Transform visual;
    public float walkSpeed = 1.4f;
    public float runSpeed = 2.8f;
    public float slowSpeed = 0.55f;
    public float crouchSpeed = 0.4f;
    public float jumpHeight = 0.5f;
    public float gravity = 12f;
    public float crouchHeight = 0.55f;
    public bool IsCrouching { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;

    CharacterController controller;
    Vector3 velocity;
    Vector3 visualScale;
    float standingHeight;
    float coyote;
    float jumpBuffer;
    readonly Collider[] overhead = new Collider[16];

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        if (visual != null) visualScale = visual.localScale;
    }

    void Update()
    {
        if (Time.deltaTime <= 0) return;
        var keyboard = Keyboard.current;
        bool input = Application.isFocused && keyboard != null;
        Vector2 direction = Vector2.zero;
        if (input)
        {
            direction.x = ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1 : 0)
                - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1 : 0);
            direction.y = ((keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) ? 1 : 0)
                - ((keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) ? 1 : 0);
        }
        bool slow = input && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        bool run = input && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        Simulate(direction, run, slow, input && keyboard.cKey.isPressed,
            input && keyboard.spaceKey.wasPressedThisFrame, input && keyboard.spaceKey.isPressed, Time.deltaTime);
    }

    // Explicit input also allows deterministic replay and editor verification.
    public void Simulate(Vector2 direction, bool run, bool slow, bool crouch, bool jumpPressed, bool jumpHeld, float dt)
    {
        if (dt <= 0) return;
        Physics.SyncTransforms();
        direction = Vector2.ClampMagnitude(direction, 1);
        SetCrouch(crouch);
        float speed = IsCrouching ? crouchSpeed : slow ? slowSpeed : run ? runSpeed : walkSpeed;
        Vector3 target = new Vector3(direction.x, 0, direction.y) * speed;
        Vector3 planar = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z), target,
            (IsGrounded ? 12f : 5f) * dt);
        velocity.x = planar.x;
        velocity.z = planar.z;
        coyote = IsGrounded && velocity.y <= 0 ? 0.1f : coyote - dt;
        jumpBuffer -= dt;
        if (jumpPressed) jumpBuffer = 0.12f;
        if (IsGrounded && velocity.y < 0) velocity.y = -1f;
        if (jumpBuffer > 0 && coyote > 0 && !IsCrouching)
        {
            velocity.y = Mathf.Sqrt(2 * gravity * jumpHeight);
            jumpBuffer = coyote = 0;
        }
        float gravityMultiplier = velocity.y > 0 && jumpHeld ? 1 : 1.8f;
        velocity.y = Mathf.Max(velocity.y - gravity * gravityMultiplier * dt, -20f);
        Vector3 worldVelocity = movementReference != null ? movementReference.TransformDirection(velocity) : velocity;
        CollisionFlags flags = controller.Move(worldVelocity * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
        if (visual != null && direction.sqrMagnitude > 0.01f)
            visual.localRotation = Quaternion.Slerp(visual.localRotation,
                Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y)), 1 - Mathf.Exp(-12 * dt));
    }

    public void SetCrouch(bool requested)
    {
        if (!requested && IsCrouching && !CanStand()) requested = true;
        IsCrouching = requested;
        controller.height = requested ? Mathf.Max(crouchHeight, controller.radius * 2) : standingHeight;
        controller.center = Vector3.up * (controller.height * 0.5f);
        if (visual != null)
        {
            visual.localScale = new Vector3(visualScale.x, visualScale.y * controller.height / standingHeight, visualScale.z);
            visual.localPosition = Vector3.up * (controller.height * 0.5f);
        }
    }

    public bool CanStand()
    {
        float radius = controller.radius * 0.95f;
        // Check the newly occupied head space, excluding our own controller.
        Vector3 bottom = transform.TransformPoint(Vector3.up * (controller.height - radius));
        Vector3 top = transform.TransformPoint(Vector3.up * (standingHeight - radius));
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overhead, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (overhead[i] != controller && !overhead[i].transform.IsChildOf(transform)) return false;
        return count < overhead.Length;
    }

    void OnDisable()
    {
        velocity = Vector3.zero;
        coyote = jumpBuffer = 0;
    }
}

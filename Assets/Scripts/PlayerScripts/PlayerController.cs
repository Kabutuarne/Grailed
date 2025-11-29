using UnityEngine;
using UnityEngine.InputSystem;   // new Input System

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    [Header("Sprint / Stamina")]
    public float staminaUsePerSecond = 25f;
    public float staminaRegenPerSecond = 15f;

    [Header("Look / Camera")]
    public Transform cameraPivot;       // child of DEF-head, at eye position
    public float lookSensitivity = 0.1f;
    public float maxHeadYaw = 75f;      // how far the head can twist left/right
    public float maxPitchUp = 80f;
    public float maxPitchDown = -80f;
    public float bodyTurnSpeed = 360f;  // deg/sec: how fast body aligns to view

    [Header("UI")]
    public PlayerUI playerUI;           // assign in Inspector

    private CharacterController controller;
    private PlayerInputActions input;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private float verticalVelocity;

    // "View" vs "Body"
    private float cameraPitch;  // vertical
    private float targetYaw;    // where the player is looking
    private float bodyYaw;      // where the body is actually facing

    private Animator animator;
    private bool wasGrounded;

    private PlayerStats stats;
    private PlayerStatusEffects statusEffects;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        stats = GetComponent<PlayerStats>();
        statusEffects = GetComponent<PlayerStatusEffects>();

        input = new PlayerInputActions();
        input.Player.Enable();

        // Movement input
        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Look input
        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        // Backpack (Tab) toggle
        input.Player.Backpack.performed += ctx => ToggleBackpack();
    }

    void OnEnable()
    {
        if (input == null) return;
        input.Player.Enable();
    }

    void OnDisable()
    {
        if (input == null) return;
        input.Player.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        float y = transform.eulerAngles.y;
        bodyYaw = y;
        targetYaw = y;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    void HandleLook()
    {
        // If backpack is open, don't rotate camera
        if (playerUI != null && playerUI.IsBackpackOpen)
            return;

        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        // where we want to look (unclamped yaw)
        targetYaw += mouseX;

        // vertical pitch
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, maxPitchDown, maxPitchUp);

        // difference between body direction and view direction
        float deltaYaw = Mathf.DeltaAngle(bodyYaw, targetYaw);

        // clamp head twist
        float headYaw = Mathf.Clamp(deltaYaw, -maxHeadYaw, maxHeadYaw);

        // rotate only the camera pivot relative to the head
        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(cameraPitch, headYaw, 0f);
        }
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

        // If backpack open, no movement input, just gravity & stamina regen
        if (playerUI != null && playerUI.IsBackpackOpen)
        {
            if (isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);

            // Stamina still regenerates while you're standing around in menus
            if (stats != null)
                stats.RegenStamina(staminaRegenPerSecond * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
                animator.SetBool("IsSprinting", false);
                animator.SetBool("IsCrouching", false);
                animator.SetBool("IsGrounded", isGrounded);
            }

            wasGrounded = isGrounded;
            return;
        }

        // input flags
        bool sprintHeld = input.Player.Sprint.IsPressed();

        // crouch with C (raw keyboard)
        bool crouchHeld = Keyboard.current != null && Keyboard.current.cKey.isPressed;

        // no sprint while crouched
        if (crouchHeld)
            sprintHeld = false;

        // local movement direction
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        // sprint logic uses stamina
        bool wantsToSprint = sprintHeld && inputDir.z > 0.1f && !crouchHeld;
        bool canSprint = stats != null && stats.stamina > 0.1f;
        bool isSprinting = wantsToSprint && canSprint;

        // select speed
        float speed;
        if (crouchHeld)
            speed = crouchSpeed;
        else if (isSprinting)
            speed = sprintSpeed;
        else
            speed = walkSpeed;

        // apply status effects speed multiplier
        float speedMult = statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;
        speed *= speedMult;

        Vector3 move = transform.TransformDirection(inputDir);

        // jump / gravity
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;

        // no jumping while crouched
        if (isGrounded && !crouchHeld && input.Player.Jump.WasPressedThisFrame())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (animator != null)
                animator.SetTrigger("Jump");
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * speed * Time.deltaTime);

        // stamina use / regen
        if (stats != null)
        {
            if (isSprinting && new Vector3(move.x, 0f, move.z).sqrMagnitude > 0.001f)
                stats.ConsumeStamina(staminaUsePerSecond * Time.deltaTime);
            else if (isGrounded && !sprintHeld)
                stats.RegenStamina(staminaRegenPerSecond * Time.deltaTime);
        }

        // --- BODY TURN TOWARD VIEW ---

        Vector3 horizontalMove = new Vector3(move.x, 0f, move.z);
        bool isMoving = horizontalMove.sqrMagnitude > 0.001f;

        if (isMoving)
        {
            float maxStep = bodyTurnSpeed * Time.deltaTime;
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetYaw, maxStep);
        }

        // apply body rotation
        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);

        // --- ANIMATOR PARAMETERS ---
        if (animator != null)
        {
            float inputMagnitude = inputDir.magnitude;
            float targetSpeed01 = 0f;

            if (inputMagnitude > 0.01f)
            {
                if (crouchHeld)
                    targetSpeed01 = 0.5f;   // crouch move
                else if (isSprinting)
                    targetSpeed01 = 1f;     // sprint
                else
                    targetSpeed01 = 0.5f;   // walk
            }

            animator.SetFloat("Speed", targetSpeed01, 0.1f, Time.deltaTime);

            float forwardInput = moveInput.y;
            float forwardSign;
            if (forwardInput > 0.01f)
                forwardSign = 1f;
            else if (forwardInput < -0.01f)
                forwardSign = -1f;
            else
                forwardSign = 1f; // strafing or idle

            animator.SetFloat("ForwardSign", forwardSign);
            animator.SetBool("IsCrouching", crouchHeld);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsSprinting", isSprinting);

            if (isGrounded && !wasGrounded)
                animator.SetTrigger("Land");
        }

        wasGrounded = isGrounded;
    }

    void ToggleBackpack()
    {
        if (playerUI == null)
            return;

        playerUI.ToggleBackpack();

        if (playerUI.IsBackpackOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

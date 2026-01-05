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
    public float staminaRegenPerSecond = 15f; // legacy base fallback — PlayerStats now provides per-player derived rate

    [Header("Look / Camera")]
    public Transform cameraPivot;       // child of Player at eye height (NOT head bone)
    public float lookSensitivity = 0.1f;
    public float maxHeadYaw = 75f;      // how far the "head" can twist left/right
    public float maxPitchUp = 80f;
    public float maxPitchDown = -80f;
    public float bodyTurnSpeed = 360f;  // deg/sec: how fast body aligns to view

    [Header("Camera Height")]
    public Vector3 standingCamLocalPos = new Vector3(0, 1.6f, 0);
    public Vector3 crouchingCamLocalPos = new Vector3(0, 1.0f, 0);
    public float camHeightLerpSpeed = 10f;

    [Header("Head / Rig")]
    public Transform headBone;          // assign DEF-head here
    public float headTurnSpeed = 10f;   // how fast head rotates toward camera

    [Header("UI")]
    public PlayerUI playerUI;           // assign in Inspector

    private CharacterController controller;
    private PlayerInputActions input;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private float verticalVelocity;

    // "View" vs "Body"
    private float cameraPitch;  // vertical
    private float targetYaw;    // where the player is looking (clamped around body)
    private float bodyYaw;      // where the body is actually facing

    private Animator animator;
    private bool wasGrounded;

    private PlayerStats stats;
    private PlayerStatusEffects statusEffects;
    private PlayerInventory inventory;
    private PlayerConsume consumer;

    private bool isCrouching;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        stats = GetComponent<PlayerStats>();
        statusEffects = GetComponent<PlayerStatusEffects>();
        inventory = GetComponent<PlayerInventory>();
        consumer = GetComponent<PlayerConsume>();

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

        // Consume input
        input.Player.Consume.performed += ctx => OnConsumeInput();

        // Drop item input
        input.Player.DropItem.performed += ctx => OnDropItemInput();
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

        if (cameraPivot != null)
            cameraPivot.localPosition = standingCamLocalPos;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        UpdateCameraHeight();
    }

    void LateUpdate()
    {
        // Apply camera pivot rotation after body rotation to avoid jitter
        if (cameraPivot != null)
        {
            float headYaw = Mathf.DeltaAngle(bodyYaw, targetYaw);
            cameraPivot.localRotation = Quaternion.Euler(cameraPitch, headYaw, 0f);
        }
        UpdateHeadRotation();
    }

    // ----------------- NEW INPUT HANDLERS -----------------

    void OnConsumeInput()
    {
        if (inventory == null || consumer == null)
            return;

        // Backpack open: consume hovered backpack item
        if (playerUI != null && playerUI.IsBackpackOpen)
        {
            var slot = InventorySlotUI.HoveredSlot;
            if (slot != null && slot.slotType == InventorySlotUI.SlotType.Backpack && slot.slotIndex >= 0)
            {
                consumer.TryStartConsumeFromBackpack(slot.slotIndex);
                return;
            }
        }

        // Backpack closed (or no hovered item): consume item in hand
        consumer.TryStartConsumeFromHand();
    }

    void OnDropItemInput()
    {
        if (inventory == null)
            return;

        Transform dropOrigin = cameraPivot != null ? cameraPivot : transform;

        // Backpack open: drop hovered backpack item
        if (playerUI != null && playerUI.IsBackpackOpen)
        {
            if (playerUI.TryDropHoveredBackpackItem(dropOrigin))
                return;
        }

        // Backpack closed (or no hovered item): drop item in hand
        inventory.DropFromHand(dropOrigin);
    }

    // ----------------- EXISTING STUFF -----------------

    void HandleLook()
    {
        // If backpack is open, don't rotate camera
        if (playerUI != null && playerUI.IsBackpackOpen)
            return;

        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        // vertical pitch
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, maxPitchDown, maxPitchUp);

        // yaw clamped around body to avoid "jump" when you hit the limit
        float desiredYaw = targetYaw + mouseX;
        float deltaFromBody = Mathf.DeltaAngle(bodyYaw, desiredYaw);
        deltaFromBody = Mathf.Clamp(deltaFromBody, -maxHeadYaw, maxHeadYaw);
        targetYaw = bodyYaw + deltaFromBody;

        // Defer camera pivot rotation to LateUpdate to align with final body rotation
        // This avoids per-frame mismatch between view and body causing jitter.
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

            if (stats != null)
                stats.RegenStamina(stats.staminaRegenPerSecond * Time.deltaTime);

            isCrouching = false;

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

        bool sprintHeld = input.Player.Sprint.IsPressed();

        // crouch with C (raw keyboard)
        bool crouchHeld = Keyboard.current != null && Keyboard.current.cKey.isPressed;
        isCrouching = crouchHeld;

        if (crouchHeld)
            sprintHeld = false;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        bool wantsToSprint = sprintHeld && inputDir.z > 0.1f && !crouchHeld;
        bool canSprint = stats != null && stats.stamina > 0.1f;
        bool isSprinting = wantsToSprint && canSprint;

        float speedMult = statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;
        float speed;
        if (crouchHeld)
            speed = crouchSpeed * speedMult; // crouch uses fixed base scaled by effects
        else if (isSprinting)
            speed = (stats != null ? stats.sprintSpeed : sprintSpeed);
        else
            speed = (stats != null ? stats.walkSpeed : walkSpeed);

        Vector3 move = transform.TransformDirection(inputDir);

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;

        if (isGrounded && !crouchHeld && input.Player.Jump.WasPressedThisFrame())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (animator != null)
                animator.SetTrigger("Jump");
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * speed * Time.deltaTime);

        if (stats != null)
        {
            if (isSprinting && new Vector3(move.x, 0f, move.z).sqrMagnitude > 0.001f)
                stats.ConsumeStamina(staminaUsePerSecond * Time.deltaTime);
            else if (isGrounded && !sprintHeld)
                stats.RegenStamina(stats.staminaRegenPerSecond * Time.deltaTime);
        }

        Vector3 horizontalMove = new Vector3(move.x, 0f, move.z);
        bool isMoving = horizontalMove.sqrMagnitude > 0.001f;

        if (isMoving)
        {
            float maxStep = bodyTurnSpeed * Time.deltaTime;
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetYaw, maxStep);
        }

        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);

        if (animator != null)
        {
            float inputMagnitude = inputDir.magnitude;
            float targetSpeed01 = 0f;

            if (inputMagnitude > 0.01f)
            {
                if (crouchHeld)
                    targetSpeed01 = 0.5f;
                else if (isSprinting)
                    targetSpeed01 = 1f;
                else
                    targetSpeed01 = 0.5f;
            }

            animator.SetFloat("Speed", targetSpeed01, 0.1f, Time.deltaTime);

            float forwardInput = moveInput.y;
            float forwardSign;
            if (forwardInput > 0.01f)
                forwardSign = 1f;
            else if (forwardInput < -0.01f)
                forwardSign = -1f;
            else
                forwardSign = 1f;

            animator.SetFloat("ForwardSign", forwardSign);
            animator.SetBool("IsCrouching", crouchHeld);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsSprinting", isSprinting);

            if (isGrounded && !wasGrounded)
                animator.SetTrigger("Land");
        }

        wasGrounded = isGrounded;
    }

    void UpdateCameraHeight()
    {
        if (cameraPivot == null) return;

        Vector3 targetPos = isCrouching ? crouchingCamLocalPos : standingCamLocalPos;

        cameraPivot.localPosition = Vector3.Lerp(
            cameraPivot.localPosition,
            targetPos,
            camHeightLerpSpeed * Time.deltaTime
        );
    }

    void UpdateHeadRotation()
    {
        if (headBone == null || cameraPivot == null)
            return;

        Vector3 lookDir = cameraPivot.forward;
        if (lookDir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetWorldRot = Quaternion.LookRotation(lookDir, Vector3.up);

        Transform parent = headBone.parent;
        if (parent != null)
        {
            Quaternion targetLocal = Quaternion.Inverse(parent.rotation) * targetWorldRot;
            headBone.localRotation = Quaternion.Slerp(
                headBone.localRotation,
                targetLocal,
                Time.deltaTime * headTurnSpeed
            );
        }
        else
        {
            headBone.rotation = Quaternion.Slerp(
                headBone.rotation,
                targetWorldRot,
                Time.deltaTime * headTurnSpeed
            );
        }
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

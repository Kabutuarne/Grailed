using UnityEngine;
using UnityEngine.InputSystem;

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
    public Transform playerCamera;
    public Transform cameraPivot;
    public float lookSensitivity = 0.1f;
    public float maxPitchUp = 80f;
    public float maxPitchDown = -80f;

    [Header("Crouch")]
    public float standingHeight = 1.8f;
    public float crouchingHeight = 1.0f;
    public float standingCameraHeight = 1.6f;
    public float crouchingCameraHeight = 1.0f;
    public float crouchLerpSpeed = 10f;

    [Header("UI")]
    public PlayerUI playerUI;

    private CharacterController controller;
    private PlayerInputActions input;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private float verticalVelocity;
    private float cameraPitch;

    private PlayerStats stats;
    private PlayerStatusEffects statusEffects;
    private PlayerInventory inventory;
    private PlayerConsume consumer;

    private bool isCrouching;

    private Vector3 externalVelocity;
    public float knockbackDecay = 8f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        stats = GetComponent<PlayerStats>();
        statusEffects = GetComponent<PlayerStatusEffects>();
        inventory = GetComponent<PlayerInventory>();
        consumer = GetComponent<PlayerConsume>();

        input = new PlayerInputActions();
        input.Player.Enable();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        input.Player.Backpack.performed += ctx => ToggleBackpack();
        input.Player.Consume.performed += ctx => OnConsumeInput();
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

        if (cameraPivot != null)
        {
            Vector3 p = cameraPivot.localPosition;
            p.y = standingCameraHeight;
            cameraPivot.localPosition = p;
        }

        controller.height = standingHeight;
        controller.center = new Vector3(0, standingHeight / 2f, 0);
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        UpdateCrouch();
    }

    // ----------------- INPUT -----------------

    void OnConsumeInput()
    {
        if (inventory == null || consumer == null)
            return;

        if (playerUI != null && playerUI.IsBackpackOpen)
        {
            var slot = InventorySlotUI.HoveredSlot;

            if (slot != null &&
                slot.slotType == InventorySlotUI.SlotType.Backpack &&
                slot.slotIndex >= 0)
            {
                consumer.TryStartConsumeFromBackpack(slot.slotIndex);
                return;
            }
        }

        consumer.TryStartConsumeFromHand();
    }

    void OnDropItemInput()
    {
        if (inventory == null)
            return;

        Transform dropOrigin = playerCamera != null ? playerCamera : transform;

        if (playerUI != null && playerUI.IsBackpackOpen)
        {
            if (playerUI.TryDropHoveredAccessoryItem(dropOrigin))
                return;

            if (playerUI.TryDropHoveredBackpackItem(dropOrigin))
                return;
        }

        inventory.DropFromHand(dropOrigin);
    }

    // ----------------- LOOK -----------------

    void HandleLook()
    {
        if (playerUI != null && playerUI.IsBackpackOpen)
            return;

        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, maxPitchDown, maxPitchUp);

        transform.Rotate(0f, mouseX, 0f);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    // ----------------- MOVEMENT -----------------

    void HandleMovement()
    {
        bool grounded = controller.isGrounded;

        if (playerUI != null && playerUI.IsBackpackOpen)
        {
            if (grounded && verticalVelocity < 0)
                verticalVelocity = -1f;

            verticalVelocity += gravity * Time.deltaTime;

            controller.Move(
                new Vector3(0, verticalVelocity, 0) * Time.deltaTime +
                externalVelocity * Time.deltaTime
            );

            if (stats != null)
                stats.RegenStamina(stats.staminaRegenPerSecond * Time.deltaTime);

            isCrouching = false;

            externalVelocity = Vector3.MoveTowards(
                externalVelocity,
                Vector3.zero,
                knockbackDecay * Time.deltaTime
            );

            return;
        }

        bool sprintHeld = input.Player.Sprint.IsPressed();
        bool crouchHeld = Keyboard.current != null && Keyboard.current.cKey.isPressed;

        isCrouching = crouchHeld;

        if (crouchHeld)
            sprintHeld = false;

        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        bool wantsSprint = sprintHeld && inputDir.z > 0.1f && !crouchHeld;
        bool canSprint = stats != null && stats.stamina > 0.1f;
        bool sprinting = wantsSprint && canSprint;

        float speedMult = statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;

        float speed;

        if (crouchHeld)
            speed = crouchSpeed * speedMult;
        else if (sprinting)
            speed = stats != null ? stats.sprintSpeed : sprintSpeed;
        else
            speed = stats != null ? stats.walkSpeed : walkSpeed;

        Vector3 move = transform.TransformDirection(inputDir);

        if (grounded && verticalVelocity < 0)
            verticalVelocity = -1f;

        if (grounded && !crouchHeld && input.Player.Jump.WasPressedThisFrame())
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move((move * speed + externalVelocity) * Time.deltaTime);

        if (stats != null)
        {
            if (sprinting && new Vector3(move.x, 0, move.z).sqrMagnitude > 0.001f)
                stats.ConsumeStamina(staminaUsePerSecond * Time.deltaTime);
            else if (grounded && !sprintHeld)
                stats.RegenStamina(stats.staminaRegenPerSecond * Time.deltaTime);
        }

        externalVelocity = Vector3.MoveTowards(
            externalVelocity,
            Vector3.zero,
            knockbackDecay * Time.deltaTime
        );
    }

    // ----------------- CROUCH -----------------

    void UpdateCrouch()
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCamY = isCrouching ? crouchingCameraHeight : standingCameraHeight;

        controller.height = Mathf.Lerp(
            controller.height,
            targetHeight,
            crouchLerpSpeed * Time.deltaTime
        );

        controller.center = new Vector3(0, controller.height / 2f, 0);

        if (cameraPivot != null)
        {
            Vector3 pos = cameraPivot.localPosition;

            pos.y = Mathf.Lerp(
                pos.y,
                targetCamY,
                crouchLerpSpeed * Time.deltaTime
            );

            cameraPivot.localPosition = pos;
        }
    }

    // ----------------- BACKPACK -----------------

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

    // ----------------- EXPLOSION -----------------

    public void ApplyExplosionKnockback(
        Vector3 origin,
        float force,
        float radius,
        float upwardsModifier)
    {
        Vector3 center = controller != null
            ? controller.bounds.center
            : transform.position;

        Vector3 dir = center - origin;

        float dist = dir.magnitude;

        if (dist <= 0.0001f || radius <= 0.0001f)
            return;

        dir /= dist;

        float atten = Mathf.Clamp01(1f - (dist / radius));

        Vector3 impulse = dir * force * atten;

        if (upwardsModifier != 0)
            impulse += Vector3.up * upwardsModifier;

        Vector3 horiz = new Vector3(impulse.x, 0, impulse.z);

        externalVelocity += horiz;
        verticalVelocity += impulse.y;
    }
}
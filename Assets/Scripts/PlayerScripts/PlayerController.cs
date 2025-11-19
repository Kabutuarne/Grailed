using UnityEngine;
using UnityEngine.InputSystem;   // <-- important

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    [Header("Look")]
    public float lookSensitivity = 0.1f;
    public Transform cameraPivot;   // assign in Inspector (CameraPivot)

    private CharacterController controller;
    private PlayerInputActions input;   // generated class
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalVelocity;
    private float cameraPitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        input = new PlayerInputActions();
        input.Player.Enable();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        input.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        input.Player.Look.canceled += ctx => lookInput = Vector2.zero;
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
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        float speed = input.Player.Sprint.IsPressed() ? sprintSpeed : walkSpeed;

        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        move = transform.TransformDirection(move);

        if (isGrounded && verticalVelocity < 0)
            verticalVelocity = -1f;

        if (isGrounded && input.Player.Jump.WasPressedThisFrame())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * speed * Time.deltaTime);
    }

    void HandleLook()
    {
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        // horizontal rotate player
        transform.Rotate(Vector3.up * mouseX);

        // vertical rotate camera pivot
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    public float interactRange = 3f;
    public LayerMask interactMask;
    public Camera cam;

    private PlayerInputActions input;
    private PlayerInventory inventory;

    void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        inventory = GetComponent<PlayerInventory>();
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

    void Update()
    {
        if (input.Player.Interact.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, interactRange, interactMask))
        {
            ItemPickup item = hit.collider.GetComponent<ItemPickup>();
            if (item != null)
            {
                inventory.PickupToBackpack(item.gameObject);
                item.OnPickedUp();
            }
        }
    }
}

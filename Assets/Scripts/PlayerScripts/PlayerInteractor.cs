using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    public float interactRange = 3f;
    public LayerMask interactMask;
    public Camera cam;

    private PlayerInputActions input;
    private PlayerInventory inventory;
    private ItemPickup lookedAtItem;

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
        // Each frame check what we're looking at and log when it changes (debug)
        CheckLookAtItem();

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
                bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
                if (placedInBackpack)
                    item.OnPickedUp();
            }
        }
    }

    void CheckLookAtItem()
    {
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, interactRange, interactMask))
        {
            ItemPickup item = hit.collider.GetComponent<ItemPickup>();
            if (item != null)
            {
                if (item != lookedAtItem)
                {
                    lookedAtItem = item;
                    Debug.Log($"Looking at item: {item.itemName} (gameobject {item.gameObject.name})");
                }
                return;
            }
        }

        // no item looked at
        if (lookedAtItem != null)
        {
            lookedAtItem = null;
            // optional: log when stop looking
        }
    }
}

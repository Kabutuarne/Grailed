using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    public float interactRange = 3f;
    public LayerMask interactMask;
    public Camera cam;

    [Header("Priority")]
    [Tooltip("If true, doors are interacted with before items when both are hit.")]
    public bool doorsTakePriority = true;

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
        if (cam == null) return;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, interactRange, interactMask))
        {
            // Get components from collider OR parent (common with mesh colliders on child objects)
            Door door = hit.collider.GetComponentInParent<Door>();
            ItemPickup item = hit.collider.GetComponent<ItemPickup>();

            // Optionally check parent for items too (depends on your prefab setup)
            if (item == null)
                item = hit.collider.GetComponentInParent<ItemPickup>();

            if (doorsTakePriority)
            {
                if (door != null)
                {
                    door.Interact();
                    return;
                }

                if (item != null)
                {
                    bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
                    if (placedInBackpack)
                        item.OnPickedUp();
                    return;
                }
            }
            else
            {
                if (item != null)
                {
                    bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
                    if (placedInBackpack)
                        item.OnPickedUp();
                    return;
                }

                if (door != null)
                {
                    door.Interact();
                    return;
                }
            }
        }
    }

    void CheckLookAtItem()
    {
        if (cam == null) return;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, interactRange, interactMask))
        {
            ItemPickup item = hit.collider.GetComponent<ItemPickup>();
            if (item == null)
                item = hit.collider.GetComponentInParent<ItemPickup>();

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
        }
    }
}
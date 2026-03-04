using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerInteractor : MonoBehaviour
{
    public float interactRange = 3f;
    public LayerMask interactMask;
    public Camera cam;

    [Header("Priority")]
    [Tooltip("If true, doors are interacted with before items when both are hit.")]
    public bool doorsTakePriority = true;

    [Header("UI")]
    [Tooltip("Assign PlayerUI so we can disable world interaction while backpack is open.")]
    public PlayerUI playerUI;

    [Tooltip("If true, prevents world interaction when the pointer is over any UI.")]
    public bool uiPointerSafe = true;

    private PlayerInputActions input;
    private PlayerInventory inventory;
    private ItemPickup lookedAtItem;

    private bool lastBackpackOpen;

    void Awake()
    {
        input = new PlayerInputActions();
        inventory = GetComponent<PlayerInventory>();

        // Do NOT enable here unconditionally; we gate it based on backpack state.
        // We'll enable/disable in Update once playerUI is known/ready.
    }

    void OnEnable()
    {
        if (input == null) input = new PlayerInputActions();
        // Defer enabling until Update() so we can respect backpack state immediately.
    }

    void OnDisable()
    {
        if (input == null) return;
        input.Player.Disable();
    }

    void Start()
    {
        // Initialize state and apply correct enable/disable immediately
        lastBackpackOpen = (playerUI != null && playerUI.IsBackpackOpen);
        ApplyInputState(lastBackpackOpen);
    }

    void Update()
    {
        bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);

        // Auto-toggle this component's input map when backpack opens/closes
        if (backpackOpen != lastBackpackOpen)
        {
            lastBackpackOpen = backpackOpen;
            ApplyInputState(backpackOpen);

            // Clear look-at state when UI opens
            if (backpackOpen)
                lookedAtItem = null;
        }

        // If UI open, do nothing (input map is disabled, but this is extra safety)
        if (backpackOpen)
            return;

        // UI-pointer safe: if mouse is over UI, don't raycast/interact with world.
        // This prevents clicks intended for UI (drag, drop, consume, tooltip hover) from also triggering Interact.
        if (uiPointerSafe && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Keep your debug look-at behavior
        CheckLookAtItem();

        // Only try interact if this input map is enabled and Interact pressed
        if (input != null && input.Player.enabled && input.Player.Interact.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    void ApplyInputState(bool backpackOpen)
    {
        if (input == null) return;

        if (backpackOpen)
        {
            // Disable ONLY this script's gameplay Interact map while UI is open
            input.Player.Disable();
        }
        else
        {
            input.Player.Enable();
        }
    }

    void TryInteract()
    {
        if (cam == null || inventory == null) return;

        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, interactRange, interactMask))
            return;

        Door door = hit.collider.GetComponentInParent<Door>();
        Chest chest = hit.collider.GetComponentInParent<Chest>();
        KartographGeneratorInteract kartographGen = hit.collider.GetComponent<KartographGeneratorInteract>();

        ItemPickup item = hit.collider.GetComponent<ItemPickup>();
        if (item == null)
            item = hit.collider.GetComponentInParent<ItemPickup>();

        if (doorsTakePriority)
        {
            if (door != null) { door.Interact(); return; }
            if (chest != null) { chest.Interact(); return; }
            if (kartographGen != null) { kartographGen.Interact(); return; }

            if (item != null)
            {
                bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
                if (placedInBackpack) item.OnPickedUp();
                return;
            }
        }
        else
        {
            if (item != null)
            {
                bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
                if (placedInBackpack) item.OnPickedUp();
                return;
            }

            if (door != null) { door.Interact(); return; }
            if (chest != null) { chest.Interact(); return; }
        }
    }

    void CheckLookAtItem()
    {
        if (cam == null) return;

        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, interactRange, interactMask))
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

        if (lookedAtItem != null)
            lookedAtItem = null;
    }
}
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

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

    [Header("World Text")]
    public Vector3 textOffset = new Vector3(0f, 1.5f, 0f);
    public float textScale = 0.05f;
    public bool billboardTextToCamera = true;
    public float textFontSize = 8f;

    private PlayerInputActions input;
    private PlayerInventory inventory;
    private ItemPickup lookedAtItem;
    private bool lastBackpackOpen;
    private bool interactionLocked;

    private GameObject worldTextObject;
    private TextMeshPro worldText;

    void Awake()
    {
        input = new PlayerInputActions();
        inventory = GetComponent<PlayerInventory>();
        CreateWorldText();
    }

    void OnEnable()
    {
        if (input == null)
            input = new PlayerInputActions();
    }

    void OnDisable()
    {
        if (input != null)
            input.Player.Disable();

        ClearCurrentItemVisuals();
    }

    void Start()
    {
        lastBackpackOpen = playerUI != null && playerUI.IsBackpackOpen;
        ApplyInputState(lastBackpackOpen);
        HideWorldText();
    }

    void Update()
    {
        bool backpackOpen = playerUI != null && playerUI.IsBackpackOpen;

        if (backpackOpen != lastBackpackOpen)
        {
            lastBackpackOpen = backpackOpen;
            ApplyInputState(backpackOpen);

            if (backpackOpen)
                ClearCurrentItemVisuals();
        }

        if (backpackOpen || interactionLocked)
        {
            ClearCurrentItemVisuals();
            return;
        }

        if (uiPointerSafe &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            ClearCurrentItemVisuals();
            return;
        }

        CheckLookAtItem();

        if (lookedAtItem != null)
        {
            UpdateWorldTextPosition();

            if (billboardTextToCamera && cam != null && worldTextObject != null)
            {
                worldTextObject.transform.forward =
                    worldTextObject.transform.position - cam.transform.position;
            }
        }

        if (input != null &&
            input.Player.enabled &&
            input.Player.Interact.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;

        if (locked)
            ClearCurrentItemVisuals();
    }

    void ApplyInputState(bool backpackOpen)
    {
        if (input == null)
            return;

        if (backpackOpen)
            input.Player.Disable();
        else
            input.Player.Enable();
    }

    void TryInteract()
    {
        if (cam == null || inventory == null)
            return;

        if (!TryGetRaycastHit(out RaycastHit hit))
            return;

        ItemPickup item = GetItemPickupFromHit(hit);
        IInteractable interactable = GetInteractableFromHit(hit);

        if (doorsTakePriority)
        {
            if (TryInteractWith(interactable))
                return;

            if (TryPickupItem(item))
                return;
        }
        else
        {
            if (TryPickupItem(item))
                return;

            if (TryInteractWith(interactable))
                return;
        }

        KartographGeneratorInteract kartographGen = hit.collider.GetComponent<KartographGeneratorInteract>();
        if (kartographGen != null)
            kartographGen.Interact();
    }

    bool TryInteractWith(IInteractable interactable)
    {
        if (interactable == null)
            return false;

        if (!interactable.CanInteract(gameObject))
            return false;

        interactable.Interact(gameObject);
        return true;
    }

    bool TryPickupItem(ItemPickup item)
    {
        if (item == null)
            return false;

        bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
        if (!placedInBackpack)
            return false;

        item.OnPickedUp();

        if (item == lookedAtItem)
            ClearCurrentItemVisuals();

        return true;
    }

    void CheckLookAtItem()
    {
        if (cam == null)
            return;

        if (TryGetRaycastHit(out RaycastHit hit))
        {
            ItemPickup item = GetItemPickupFromHit(hit);

            if (item != null)
            {
                if (item != lookedAtItem)
                    SetLookedAtItem(item);

                return;
            }
        }

        if (lookedAtItem != null)
            ClearCurrentItemVisuals();
    }

    bool TryGetRaycastHit(out RaycastHit hit)
    {
        Vector3 origin = cam.transform.position;
        Vector3 direction = cam.transform.forward;

        return Physics.Raycast(origin, direction, out hit, interactRange, interactMask);
    }

    IInteractable GetInteractableFromHit(RaycastHit hit)
    {
        MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractable interactable)
                return interactable;
        }

        return null;
    }

    ItemPickup GetItemPickupFromHit(RaycastHit hit)
    {
        ItemPickup item = hit.collider.GetComponent<ItemPickup>();
        if (item == null)
            item = hit.collider.GetComponentInParent<ItemPickup>();

        return item;
    }

    void SetLookedAtItem(ItemPickup newItem)
    {
        ClearCurrentItemVisuals();

        lookedAtItem = newItem;
        ShowWorldText(GetItemTitle(lookedAtItem), lookedAtItem.transform.position + textOffset);
    }

    void ClearCurrentItemVisuals()
    {
        lookedAtItem = null;
        HideWorldText();
    }

    string GetItemTitle(ItemPickup itemPickup)
    {
        if (itemPickup == null)
            return "Item";

        MonoBehaviour[] behaviours = itemPickup.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IItemDisplayName named &&
                !string.IsNullOrWhiteSpace(named.DisplayName))
            {
                return named.DisplayName;
            }
        }

        return !string.IsNullOrWhiteSpace(itemPickup.itemName)
            ? itemPickup.itemName
            : itemPickup.gameObject.name;
    }

    void CreateWorldText()
    {
        worldTextObject = new GameObject("ItemHoverWorldText");
        worldTextObject.transform.SetParent(null);
        worldTextObject.transform.localScale = Vector3.one * textScale;

        worldText = worldTextObject.AddComponent<TextMeshPro>();
        worldText.alignment = TextAlignmentOptions.Center;
        worldText.fontSize = textFontSize;
        worldText.text = "";
        worldText.color = Color.white;
        worldText.outlineWidth = 0.2f;

        worldTextObject.SetActive(false);
    }

    void ShowWorldText(string text, Vector3 position)
    {
        if (worldTextObject == null || worldText == null)
            return;

        worldText.text = text;
        worldTextObject.transform.position = position;
        worldTextObject.SetActive(true);
    }

    void HideWorldText()
    {
        if (worldTextObject != null)
            worldTextObject.SetActive(false);
    }

    void UpdateWorldTextPosition()
    {
        if (lookedAtItem == null || worldTextObject == null)
            return;

        worldTextObject.transform.position = lookedAtItem.transform.position + textOffset;
    }

    void OnDestroy()
    {
        if (worldTextObject != null)
            Destroy(worldTextObject);
    }
}
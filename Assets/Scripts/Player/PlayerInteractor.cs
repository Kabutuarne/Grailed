using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Item Hover Visuals")]
    [Tooltip("Optional: child object name to toggle as glow/highlight, e.g. 'Glow'")]
    public string glowChildName = "Glow";

    [Tooltip("Optional: instead of child name, toggle the first child/object with this tag.")]
    public string glowTag = "ItemGlow";

    [Header("World Text")]
    public Vector3 textOffset = new Vector3(0f, 1.5f, 0f);
    public float textScale = 0.05f;
    public bool billboardTextToCamera = true;
    public float textFontSize = 8f;

    private PlayerInputActions input;
    private PlayerInventory inventory;
    private ItemPickup lookedAtItem;
    private bool lastBackpackOpen;

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
        if (input == null) input = new PlayerInputActions();
    }

    void OnDisable()
    {
        if (input == null) return;
        input.Player.Disable();
        ClearCurrentItemVisuals();
    }

    void Start()
    {
        lastBackpackOpen = (playerUI != null && playerUI.IsBackpackOpen);
        ApplyInputState(lastBackpackOpen);
        HideWorldText();
    }

    void Update()
    {
        bool backpackOpen = (playerUI != null && playerUI.IsBackpackOpen);

        if (backpackOpen != lastBackpackOpen)
        {
            lastBackpackOpen = backpackOpen;
            ApplyInputState(backpackOpen);

            if (backpackOpen)
            {
                ClearCurrentItemVisuals();
            }
        }

        if (backpackOpen)
            return;

        if (uiPointerSafe && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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

        if (input != null && input.Player.enabled && input.Player.Interact.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    void ApplyInputState(bool backpackOpen)
    {
        if (input == null) return;

        if (backpackOpen)
            input.Player.Disable();
        else
            input.Player.Enable();
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
                if (placedInBackpack)
                {
                    item.OnPickedUp();

                    if (item == lookedAtItem)
                        ClearCurrentItemVisuals();
                }
                return;
            }
        }
        else
        {
            if (item != null)
            {
                bool placedInBackpack = inventory.PickupToBackpack(item.gameObject);
                if (placedInBackpack)
                {
                    item.OnPickedUp();

                    if (item == lookedAtItem)
                        ClearCurrentItemVisuals();
                }
                return;
            }

            if (door != null) { door.Interact(); return; }
            if (chest != null) { chest.Interact(); return; }
            if (kartographGen != null) { kartographGen.Interact(); return; }
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
                    SetLookedAtItem(item);
                }
                return;
            }
        }

        if (lookedAtItem != null)
            ClearCurrentItemVisuals();
    }

    void SetLookedAtItem(ItemPickup newItem)
    {
        ClearCurrentItemVisuals();

        lookedAtItem = newItem;

        ToggleItemGlow(lookedAtItem, true);
        ShowWorldText(GetItemTitle(lookedAtItem), lookedAtItem.transform.position + textOffset);

        Debug.Log($"Looking at item: {lookedAtItem.itemName} (gameobject {lookedAtItem.gameObject.name})");
    }

    void ClearCurrentItemVisuals()
    {
        if (lookedAtItem != null)
        {
            ToggleItemGlow(lookedAtItem, false);
            lookedAtItem = null;
        }

        HideWorldText();
    }

    void ToggleItemGlow(ItemPickup item, bool state)
    {
        if (item == null) return;
    }
    Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    string GetItemTitle(ItemPickup itemPickup)
    {
        if (itemPickup == null) return "Item";

        ScrollItem scrollItem = itemPickup.GetComponent<ScrollItem>();
        if (scrollItem == null) scrollItem = itemPickup.GetComponentInParent<ScrollItem>();
        if (scrollItem != null) return scrollItem.title;

        Accessory accessory = itemPickup.GetComponent<Accessory>();
        if (accessory == null) accessory = itemPickup.GetComponentInParent<Accessory>();
        if (accessory != null) return accessory.title;

        DecorationItem decoration = itemPickup.GetComponent<DecorationItem>();
        if (decoration == null) decoration = itemPickup.GetComponentInParent<DecorationItem>();
        if (decoration != null) return decoration.title;

        WandItem wand = itemPickup.GetComponent<WandItem>();
        if (wand == null) wand = itemPickup.GetComponentInParent<WandItem>();
        if (wand != null) return wand.title;

        ConsumableItem consumable = itemPickup.GetComponent<ConsumableItem>();
        if (consumable == null) consumable = itemPickup.GetComponentInParent<ConsumableItem>();
        if (consumable != null) return consumable.title;

        return itemPickup.itemName;
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
        if (worldTextObject == null || worldText == null) return;

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
        if (lookedAtItem == null || worldTextObject == null) return;
        worldTextObject.transform.position = lookedAtItem.transform.position + textOffset;
    }

    void OnDestroy()
    {
        if (worldTextObject != null)
            Destroy(worldTextObject);
    }
}
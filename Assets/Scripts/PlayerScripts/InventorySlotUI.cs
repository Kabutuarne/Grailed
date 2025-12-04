using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public Text label;
    public Image icon;

    public Sprite emptyIcon;

    public enum SlotType
    {
        Backpack,
        RightHand,
        Accessory
    }

    [Header("Slot Type")]
    public SlotType slotType = SlotType.Backpack;

    [HideInInspector] public int slotIndex = -1;
    [HideInInspector] public PlayerInventory inventory;

    static InventorySlotUI hoveredSlot;
    static InventorySlotUI draggingSlot;

    // NEW: expose hovered slot
    public static InventorySlotUI HoveredSlot => hoveredSlot;

    private PlayerUI playerUI;

    void Awake()
    {
        playerUI = GetComponentInParent<PlayerUI>();
    }

    public void SetItem(GameObject item, int index = -1, PlayerInventory inv = null)
    {
        slotIndex = index;
        inventory = inv;

        if (label != null)
            label.text = item != null ? item.name : "";

        if (icon != null)
        {
            Sprite s = emptyIcon;

            if (item != null)
            {
                var pickup = item.GetComponent<ConsumableItem>();
                if (pickup != null && pickup.inventoryIcon != null)
                    s = pickup.inventoryIcon;
            }

            icon.sprite = s;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoveredSlot = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoveredSlot == this)
            hoveredSlot = null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventory == null || playerUI == null)
            return;

        if (!playerUI.IsBackpackOpen)
            return;

        GameObject item = null;

        switch (slotType)
        {
            case SlotType.Backpack:
                if (slotIndex >= 0 && slotIndex < inventory.backpack.Length)
                    item = inventory.backpack[slotIndex];
                break;

            case SlotType.RightHand:
                item = inventory.rightHandItem;
                break;

            case SlotType.Accessory:
                if (slotIndex >= 0 && slotIndex < inventory.accessories.Length)
                    item = inventory.accessories[slotIndex];
                break;
        }

        if (item == null)
            return;

        draggingSlot = this;

        Sprite dragSprite = icon != null ? icon.sprite : null;
        playerUI.StartDrag(item, dragSprite, this, slotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingSlot != this || playerUI == null)
            return;

        if (!playerUI.IsBackpackOpen)
            return;

        playerUI.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingSlot != this || playerUI == null)
        {
            draggingSlot = null;
            return;
        }

        draggingSlot = null;

        if (!playerUI.IsBackpackOpen)
        {
            playerUI.EndDrag();
            return;
        }

        if (hoveredSlot != null)
        {
            playerUI.HandleDrop(hoveredSlot);
        }
        else
        {
            playerUI.EndDrag();
        }
    }
}

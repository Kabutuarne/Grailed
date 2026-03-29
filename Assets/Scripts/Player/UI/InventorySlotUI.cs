using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler
{
    public Text label;
    public Image icon;

    public Sprite emptyIcon;

    public enum SlotType
    {
        Backpack,
        RightHand,
        Accessory,
        WandInternal
    }

    [Header("Slot Type")]
    public SlotType slotType = SlotType.Backpack;

    [HideInInspector] public int slotIndex = -1;
    [HideInInspector] public PlayerInventory inventory;
    [HideInInspector] public WandItem wandOwner;
    [HideInInspector] public int wandSlotIndex = -1;

    static InventorySlotUI hoveredSlot;
    static InventorySlotUI draggingSlot;

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
            label.text = item != null ? ItemTooltipDataUtility.GetDisplayName(item) : string.Empty;

        if (icon != null)
        {
            Sprite s = emptyIcon;

            if (item != null)
            {
                Sprite resolved = ItemTooltipDataUtility.GetInventoryIcon(item);
                if (resolved != null)
                    s = resolved;
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

            case SlotType.WandInternal:
                if (wandOwner != null && wandSlotIndex >= 0)
                    item = wandOwner.GetSlotItem(wandSlotIndex);
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
            playerUI.HandleDrop(hoveredSlot);
        else
            playerUI.EndDrag();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playerUI == null)
            return;

        playerUI.NotifySlotClicked(this);
    }
}
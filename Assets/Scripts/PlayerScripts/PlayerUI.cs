using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    public PlayerStatusEffects statusEffects;
    public PlayerInventory inventory;

    [Header("HUD Root (Always-on HUD)")]
    public GameObject hudRoot;

    [Header("HUD Bars (always-on HUD)")]
    public Slider healthBarHUD;
    public Slider manaBarHUD;
    public Slider sprintBarHUD;

    [Header("Backpack Bars")]
    public Slider healthBarBackpack;
    public Slider manaBarBackpack;

    [Header("Status Effects UI (Backpack)")]
    public Transform statusEffectsRoot;
    public GameObject statusEffectPrefab;

    [Header("Hand Slots UI (Backpack)")]
    public InventorySlotUI rightHandSlot;

    [Header("Accessory Slots UI (Backpack)")]
    public InventorySlotUI[] accessorySlots = new InventorySlotUI[4];

    [Header("Backpack Inventory Slots (3x3)")]
    public InventorySlotUI[] backpackSlots;

    [Header("Character Sheet Texts (Backpack)")]
    public Text healthText;
    public Text manaText;
    public Text staminaText;
    public Text intelligenceText;
    public Text strengthText;
    public Text staminaAttrText;
    public Text agilityText;

    [Header("Backpack Root Panel")]
    public GameObject backpackRoot;
    [Header("Item Tooltip")]
    public GameObject itemTooltipPrefab; // prefab with ItemTooltip component

    public bool IsBackpackOpen => backpackRoot != null && backpackRoot.activeSelf;

    private Canvas uiCanvas;
    private GameObject dragIconGO;
    private Image dragIconImage;
    private GameObject currentlyDraggedItem;
    private InventorySlotUI dragSourceSlot;
    private int dragSourceIndex = -1;
    private GameObject tooltipGO;
    private ItemTooltip tooltipController;

    void Start()
    {
        if (backpackRoot != null)
            backpackRoot.SetActive(false);

        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;

        uiCanvas = GetComponentInParent<Canvas>();
        if (uiCanvas == null)
            uiCanvas = FindFirstObjectByType<Canvas>();

        // instantiate tooltip (hidden) if prefab provided
        if (itemTooltipPrefab != null && uiCanvas != null)
        {
            tooltipGO = Instantiate(itemTooltipPrefab, uiCanvas.transform, false);
            tooltipController = tooltipGO.GetComponent<ItemTooltip>();
            if (tooltipGO != null)
                tooltipGO.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleInventoryChanged;

        if (dragIconGO != null)
            Destroy(dragIconGO);
    }

    void Update()
    {
        UpdateBars();
        UpdateStatusEffects();
        UpdateHands();
        UpdateBackpackSlots();
        UpdateAccessories();
        UpdateCharacterSheet();

        if (IsBackpackOpen && dragIconGO != null)
        {
            Vector2 pos = UnityEngine.InputSystem.Mouse.current != null
                ? (Vector2)UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            UpdateDrag(pos);
        }
        UpdateTooltip();
    }

    void UpdateTooltip()
    {
        if (tooltipGO == null || tooltipController == null || uiCanvas == null)
            return;

        var hovered = InventorySlotUI.HoveredSlot;
        if (hovered != null && hovered.inventory != null && IsBackpackOpen)
        {
            GameObject item = null;
            switch (hovered.slotType)
            {
                case InventorySlotUI.SlotType.Backpack:
                    if (hovered.slotIndex >= 0 && hovered.slotIndex < hovered.inventory.backpack.Length)
                        item = hovered.inventory.backpack[hovered.slotIndex];
                    break;
                case InventorySlotUI.SlotType.RightHand:
                    item = hovered.inventory.rightHandItem;
                    break;
                case InventorySlotUI.SlotType.Accessory:
                    if (hovered.slotIndex >= 0 && hovered.slotIndex < hovered.inventory.accessories.Length)
                        item = hovered.inventory.accessories[hovered.slotIndex];
                    break;
            }

            if (item != null)
            {
                // gather display info from ScrollItem or ConsumableItem
                string title = item.name;
                Color titleColor = Color.white;
                string desc = "";
                Color descColor = Color.white;

                var scroll = item.GetComponent<ScrollItem>();
                if (scroll != null)
                {
                    title = !string.IsNullOrEmpty(scroll.title) ? scroll.title : item.name;
                    titleColor = scroll.titleColor;
                    desc = scroll.description;
                    descColor = scroll.descriptionColor;
                }
                else
                {
                    var cons = item.GetComponent<ConsumableItem>();
                    if (cons != null)
                    {
                        title = !string.IsNullOrEmpty(cons.title) ? cons.title : item.name;
                        titleColor = cons.titleColor;
                        desc = cons.description;
                        descColor = cons.descriptionColor;
                    }
                    else
                    {
                        // DecorationItem support for tooltip
                        var decor = item.GetComponent<DecorationItem>();
                        if (decor != null)
                        {
                            title = !string.IsNullOrEmpty(decor.title) ? decor.title : item.name;
                            titleColor = decor.titleColor;
                            desc = decor.description;
                            descColor = decor.descriptionColor;
                        }
                    }
                }

                tooltipController.SetData(title, titleColor, desc, descColor);

                // Position tooltip near mouse
                Vector2 screenPos = UnityEngine.InputSystem.Mouse.current != null
                    ? (Vector2)UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                    : (Vector2)Input.mousePosition;

                RectTransform canvasRect = uiCanvas.transform as RectTransform;
                RectTransform tooltipRect = tooltipGO.transform as RectTransform;
                Camera cam = uiCanvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCanvas.worldCamera : null;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out localPos);
                // Position tooltip with top-left at mouse cursor plus small offset.
                tooltipRect.localPosition = localPos + new Vector2(10f, -10f);

                tooltipGO.SetActive(true);
                return;
            }
        }

        tooltipGO.SetActive(false);
    }

    void UpdateBars()
    {
        if (stats == null)
            return;

        float h = stats.Health01;
        float m = stats.Mana01;
        float s = stats.Stamina01;

        if (healthBarHUD != null)
            healthBarHUD.value = h;
        if (manaBarHUD != null)
            manaBarHUD.value = m;
        if (sprintBarHUD != null)
            sprintBarHUD.value = s;

        if (healthBarBackpack != null)
            healthBarBackpack.value = h;
        if (manaBarBackpack != null)
            manaBarBackpack.value = m;
    }

    void UpdateStatusEffects()
    {
        if (statusEffectsRoot == null || statusEffectPrefab == null || statusEffects == null)
            return;

        foreach (Transform child in statusEffectsRoot)
            Destroy(child.gameObject);

        var entries = new Dictionary<object, float>();

        foreach (var e in statusEffects.activeEffects)
        {
            if (e.carrier != null)
            {
                float current = entries.ContainsKey(e.carrier) ? entries[e.carrier] : 0f;
                if (e.duration < 0f)
                {
                    entries[e.carrier] = -1f;
                }
                else
                {
                    float val = e.timer;
                    entries[e.carrier] = Mathf.Max(current, val);
                }
            }
            else
            {
                string key = e.id + "_" + Guid.NewGuid().ToString();
                entries[key] = e.duration < 0f ? -1f : e.timer;
            }
        }

        foreach (var kv in entries)
        {
            GameObject go = Instantiate(statusEffectPrefab, statusEffectsRoot);
            float timeVal = kv.Value;

            if (kv.Key is EffectCarrier carrierKey)
            {
                // Only Icon + Description for carrier-based entries
                Image iconImage = null;
                var iconTransform = go.transform.Find("Icon");
                if (iconTransform != null)
                    iconImage = iconTransform.GetComponent<Image>();
                if (iconImage == null)
                    iconImage = go.GetComponentInChildren<Image>();
                if (iconImage != null && carrierKey.icon != null)
                    iconImage.sprite = carrierKey.icon;

                // Set description to provided text
                var descTransform = go.transform.Find("Description");
                if (descTransform != null)
                {
                    var descText = descTransform.GetComponent<Text>();
                    if (descText != null)
                        descText.text = carrierKey.description;
                }
            }
            else
            {
                // Non-carrier entries: keep legacy title/time in first Text
                Text t = go.GetComponentInChildren<Text>();
                string titleText;
                if (timeVal < 0f)
                    titleText = $"{kv.Key} (ON)";
                else
                    titleText = $"{kv.Key} ({Mathf.CeilToInt(timeVal)}s)";
                if (t != null)
                    t.text = titleText;
            }
        }
    }

    void UpdateHands()
    {
        if (inventory == null)
            return;

        if (rightHandSlot != null)
            rightHandSlot.SetItem(inventory.rightHandItem, -1, inventory);
    }

    void UpdateBackpackSlots()
    {
        if (inventory == null || backpackSlots == null)
            return;

        for (int i = 0; i < backpackSlots.Length; i++)
        {
            var slot = backpackSlots[i];
            if (slot == null)
                continue;

            GameObject item = null;
            if (inventory.backpack != null && i < inventory.backpack.Length)
                item = inventory.backpack[i];

            slot.SetItem(item, i, inventory);
        }
    }

    void UpdateAccessories()
    {
        if (inventory == null || accessorySlots == null || inventory.accessories == null)
            return;

        int len = Mathf.Min(accessorySlots.Length, inventory.accessories.Length);

        for (int i = 0; i < len; i++)
        {
            var slot = accessorySlots[i];
            if (slot == null)
                continue;

            GameObject item = inventory.accessories[i];
            slot.SetItem(item, i, inventory);
        }
    }

    void UpdateCharacterSheet()
    {
        if (stats == null)
            return;

        if (healthText != null)
            healthText.text = $"{Mathf.RoundToInt(stats.health)} / {Mathf.RoundToInt(stats.maxHealth)}";

        if (manaText != null)
            manaText.text = $"{Mathf.RoundToInt(stats.mana)} / {Mathf.RoundToInt(stats.maxMana)}";

        if (staminaText != null)
            staminaText.text = $"Stamina: {Mathf.RoundToInt(stats.stamina)} / {Mathf.RoundToInt(stats.maxStamina)}";

        if (intelligenceText != null)
            intelligenceText.text = $"Intelligence: {Mathf.RoundToInt(stats.effectiveIntelligence)}";

        if (strengthText != null)
            strengthText.text = $"Strength: {Mathf.RoundToInt(stats.effectiveStrength)}";

        if (staminaAttrText != null)
            staminaAttrText.text = $"Stamina: {Mathf.RoundToInt(stats.effectiveStaminaAttr)}";

        if (agilityText != null)
            agilityText.text = $"Agility: {Mathf.RoundToInt(stats.effectiveAgility)}";
    }

    void HandleInventoryChanged()
    {
        UpdateBackpackSlots();
        UpdateHands();
        UpdateAccessories();
    }

    // ----- Drag handling -----

    public void StartDrag(GameObject item, Sprite icon, InventorySlotUI source, int sourceIndex = -1)
    {
        if (!IsBackpackOpen)
            return;

        if (item == null)
            return;

        currentlyDraggedItem = item;
        dragSourceSlot = source;
        dragSourceIndex = sourceIndex;

        Sprite s = icon ?? GetIconFromItem(item);
        CreateDragIcon(s);

        Vector2 pos = UnityEngine.InputSystem.Mouse.current != null
            ? (Vector2)UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        UpdateDrag(pos);
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragIconGO == null || uiCanvas == null)
            return;

        RectTransform canvasRect = uiCanvas.transform as RectTransform;
        Vector2 localPos;
        Camera cam = uiCanvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCanvas.worldCamera : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            cam,
            out localPos
        );

        (dragIconGO.transform as RectTransform).localPosition = localPos;
    }

    public void EndDrag()
    {
        currentlyDraggedItem = null;
        dragSourceSlot = null;
        dragSourceIndex = -1;

        if (dragIconGO != null)
        {
            Destroy(dragIconGO);
            dragIconGO = null;
            dragIconImage = null;
        }
    }

    private void CreateDragIcon(Sprite sprite)
    {
        if (uiCanvas == null)
            return;

        if (dragIconGO != null)
            Destroy(dragIconGO);

        dragIconGO = new GameObject("DragIcon");
        dragIconGO.transform.SetParent(uiCanvas.transform, false);
        dragIconImage = dragIconGO.AddComponent<Image>();
        dragIconImage.raycastTarget = false;
        dragIconImage.sprite = sprite;

        RectTransform rt = dragIconGO.GetComponent<RectTransform>();
        if (sprite != null)
            rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
        else
            rt.sizeDelta = new Vector2(40, 40);
    }

    private Sprite GetIconFromItem(GameObject item)
    {
        if (item == null)
            return null;

        var sr = item.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite;

        var uiImg = item.GetComponentInChildren<Image>();
        if (uiImg != null && uiImg.sprite != null)
            return uiImg.sprite;

        return null;
    }

    private bool IsSlotInBackpack(InventorySlotUI slot)
    {
        if (slot == null || backpackSlots == null)
            return false;

        for (int i = 0; i < backpackSlots.Length; i++)
            if (backpackSlots[i] == slot)
                return true;

        return false;
    }

    public void HandleDrop(InventorySlotUI targetSlot)
    {
        if (!IsBackpackOpen)
        {
            EndDrag();
            return;
        }

        if (inventory == null || currentlyDraggedItem == null || dragSourceSlot == null)
        {
            EndDrag();
            return;
        }

        if (targetSlot == dragSourceSlot)
        {
            EndDrag();
            return;
        }

        var srcType = dragSourceSlot.slotType;
        var dstType = targetSlot.slotType;
        int srcIndex = dragSourceIndex;
        int dstIndex = targetSlot.slotIndex;

        if (srcType == InventorySlotUI.SlotType.Backpack && dstType == InventorySlotUI.SlotType.Backpack)
        {
            inventory.SwapBackpackSlots(srcIndex, dstIndex);
        }
        else if (srcType == InventorySlotUI.SlotType.Backpack && dstType == InventorySlotUI.SlotType.RightHand)
        {
            inventory.SwapRightHandWithBackpack(srcIndex);
        }
        else if (srcType == InventorySlotUI.SlotType.RightHand && dstType == InventorySlotUI.SlotType.Backpack)
        {
            inventory.SwapRightHandWithBackpack(dstIndex);
        }
        else
        {
            Debug.Log("PlayerUI: Drop combination not handled.");
        }

        EndDrag();
    }

    // ----- NEW: helpers for PlayerController -----

    public bool TryConsumeHoveredBackpackItem(GameObject user)
    {
        if (!IsBackpackOpen || inventory == null)
            return false;

        var slot = InventorySlotUI.HoveredSlot;
        if (slot == null || slot.slotType != InventorySlotUI.SlotType.Backpack)
            return false;

        if (slot.slotIndex < 0)
            return false;

        return inventory.ConsumeFromBackpack(slot.slotIndex, user);
    }

    public bool TryDropHoveredBackpackItem(Transform dropOrigin)
    {
        if (!IsBackpackOpen || inventory == null)
            return false;

        var slot = InventorySlotUI.HoveredSlot;
        if (slot == null || slot.slotType != InventorySlotUI.SlotType.Backpack)
            return false;

        if (slot.slotIndex < 0)
            return false;

        return inventory.DropFromBackpack(slot.slotIndex, dropOrigin);
    }

    public void ToggleBackpack()
    {
        if (backpackRoot == null)
            return;

        bool newState = !backpackRoot.activeSelf;
        backpackRoot.SetActive(newState);

        if (hudRoot != null)
            hudRoot.SetActive(!newState);

        if (!newState)
            EndDrag();
    }
}

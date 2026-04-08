using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    public StatusEffects statusEffects;
    public PlayerInventory inventory;

    [Header("HUD Root (Always-on HUD)")]
    public GameObject hudRoot;

    [Header("Status Effects UI (Backpack)")]
    public Transform statusEffectsRoot;
    public GameObject statusEffectPrefab;

    [Header("Status Effects Styling")]
    public Sprite firstEffectBackground;

    [Header("Status Effects UI (HUD)")]
    public Transform statusEffectsHudRoot;
    public GameObject statusEffectHudPrefab;

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

    [Header("HUD Numbers")]
    public Text hudHealthText;
    public Text hudManaText;
    public Text hudEnergyText;

    [Header("Wand Slots Panel")]
    public WandSlotsPanel wandSlotsPanelPrefab;
    private WandSlotsPanel wandPanelInstance;
    private InventorySlotUI wandSourceSlot;

    [Header("Selection Visuals")]
    public GameObject selectedPrefabAccessory;
    public GameObject selectedPrefabBackpack;
    public GameObject selectedPrefabEquipped;
    // Assign an existing ItemDescriptionContainer element in the backpack UI (do not instantiate)
    public ItemDescriptionContainer descriptionContainerInstance; // existing UI element

    public GameObject highlightedPrefabAccessory;
    public GameObject highlightedPrefabBackpack;
    public GameObject highlightedPrefabEquipped;

    private InventorySlotUI selectedSlot;
    private GameObject selectedMarkerInstance;
    private ItemDescriptionContainer descriptionPanelInstance;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource backpackOpenSound;
    [SerializeField] private AudioSource backpackCloseSound;
    [SerializeField] private AudioSource clickSound;
    [SerializeField] private AudioSource dropSuccessSound;
    [SerializeField] private AudioSource dropInvalidSound;

    public bool IsBackpackOpen => backpackRoot != null && backpackRoot.activeSelf;

    private Canvas uiCanvas;
    private GameObject dragIconGO;
    private Image dragIconImage;
    private GameObject currentlyDraggedItem;
    private InventorySlotUI dragSourceSlot;
    private int dragSourceIndex = -1;
    private GameObject tooltipGO;
    private Component tooltipController; // kept for compatibility if reused

    void Start()
    {
        if (backpackRoot != null)
            backpackRoot.SetActive(false);

        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;

        uiCanvas = GetComponentInParent<Canvas>();
        if (uiCanvas == null)
            uiCanvas = FindFirstObjectByType<Canvas>();

        // Use the existing description container instance if assigned
        descriptionPanelInstance = descriptionContainerInstance;
        if (descriptionPanelInstance != null)
            descriptionPanelInstance.gameObject.SetActive(false);
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
        UpdateStatusEffectsHUD();
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


        if (IsBackpackOpen && wandPanelInstance != null && wandPanelInstance.IsShowing)
        {
            wandPanelInstance.UpdateLayout();

            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool clickReleased = mouse != null && mouse.leftButton.wasReleasedThisFrame;
            var hovered = InventorySlotUI.HoveredSlot;
            if (clickReleased && currentlyDraggedItem == null)
            {
                bool overWandSource = hovered == wandSourceSlot;
                bool overWandInternal = hovered != null && hovered.slotType == InventorySlotUI.SlotType.WandInternal;
                if (!overWandSource && !overWandInternal)
                    HideWandPanel();
            }
        }
    }

    // Tooltip system removed: no per-item tooltip rendering

    private GameObject GetItemFromSlot(InventorySlotUI slot)
    {
        if (slot == null || slot.inventory == null)
            return null;

        switch (slot.slotType)
        {
            case InventorySlotUI.SlotType.Backpack:
                if (slot.slotIndex >= 0 && slot.slotIndex < slot.inventory.backpack.Length)
                    return slot.inventory.backpack[slot.slotIndex];
                break;

            case InventorySlotUI.SlotType.RightHand:
                return slot.inventory.rightHandItem;

            case InventorySlotUI.SlotType.Accessory:
                if (slot.slotIndex >= 0 && slot.slotIndex < slot.inventory.accessories.Length)
                    return slot.inventory.accessories[slot.slotIndex];
                break;

            case InventorySlotUI.SlotType.WandInternal:
                if (slot.wandOwner != null && slot.wandSlotIndex >= 0)
                    return slot.wandOwner.GetSlotItem(slot.wandSlotIndex);
                break;
        }

        return null;
    }

    void UpdateBars()
    {
        if (hudHealthText != null)
            hudHealthText.text = $"{Mathf.RoundToInt(stats.health)} / {Mathf.RoundToInt(stats.maxHealth)}";
        if (hudManaText != null)
            hudManaText.text = $"{Mathf.RoundToInt(stats.mana)} / {Mathf.RoundToInt(stats.maxMana)}";
        if (hudEnergyText != null)
            hudEnergyText.text = $"{Mathf.RoundToInt(stats.stamina)} / {Mathf.RoundToInt(stats.maxStamina)}";
    }

    void UpdateStatusEffects()
    {
        if (statusEffectsRoot == null || statusEffectPrefab == null || statusEffects == null)
            return;

        foreach (Transform child in statusEffectsRoot)
            Destroy(child.gameObject);

        // Build a display dictionary from the new ActiveEffects list.
        // Key = carrier ScriptableObject (groups stacks) or a unique string for carrier-less effects.
        // Value = longest remaining timer (-1 = infinite).
        var entries = new Dictionary<object, float>();

        foreach (StatusEffectData e in statusEffects.ActiveEffects)
        {
            if (e.hideInUI)
                continue;

            if (e.carrier != null)
            {
                float current = entries.ContainsKey(e.carrier) ? entries[e.carrier] : 0f;
                entries[e.carrier] = e.IsInfinite ? -1f : Mathf.Max(current, e.timer);
            }
            else
            {
                // No carrier – use a unique key so each instance gets its own row.
                string key = e.id + "_" + Guid.NewGuid();
                entries[key] = e.IsInfinite ? -1f : e.timer;
            }
        }

        int idx = 0;
        foreach (var kv in entries)
        {
            GameObject go = Instantiate(statusEffectPrefab, statusEffectsRoot);
            float timeVal = kv.Value;

            // Special background for the first (top) effect if available
            if (idx == 0 && firstEffectBackground != null)
            {
                var bg = go.transform.Find("BG");
                if (bg != null)
                {
                    var bgImg = bg.GetComponent<Image>();
                    if (bgImg != null) bgImg.sprite = firstEffectBackground;
                }
            }

            if (kv.Key is EffectCarrier carrier)
            {
                // Icon
                Image iconImage = null;
                var iconTransform = go.transform.Find("Icon");
                if (iconTransform != null)
                    iconImage = iconTransform.GetComponent<Image>();
                if (iconImage == null)
                    iconImage = go.GetComponentInChildren<Image>();
                if (iconImage != null && carrier.icon != null)
                    iconImage.sprite = carrier.icon;

                // Title
                var titleTransform = go.transform.Find("Title");
                if (titleTransform != null)
                {
                    var titleText = titleTransform.GetComponent<Text>();
                    if (titleText != null)
                        titleText.text = carrier.title ?? carrier.name;
                }

                // Time
                var timeTransform = go.transform.Find("Time");
                if (timeTransform != null)
                {
                    var timeText = timeTransform.GetComponent<Text>();
                    if (timeText != null)
                        timeText.text = timeVal > 0f ? $"{Mathf.CeilToInt(timeVal)}s" : "";
                }
            }
            else
            {
                var titleTransform = go.transform.Find("Title");
                if (titleTransform != null)
                {
                    var titleText = titleTransform.GetComponent<Text>();
                    if (titleText != null)
                        titleText.text = kv.Key.ToString();
                }

                var timeTransform = go.transform.Find("Time");
                if (timeTransform != null)
                {
                    var timeText = timeTransform.GetComponent<Text>();
                    if (timeText != null)
                        timeText.text = timeVal < 0f ? "ON" : $"{Mathf.CeilToInt(timeVal)}s";
                }
            }

            idx++;
        }
    }

    void UpdateStatusEffectsHUD()
    {
        if (statusEffectsHudRoot == null || statusEffectHudPrefab == null || statusEffects == null)
            return;

        foreach (Transform child in statusEffectsHudRoot)
            Destroy(child.gameObject);

        var entries = new Dictionary<object, float>();

        foreach (StatusEffectData e in statusEffects.ActiveEffects)
        {
            if (e.hideInUI)
                continue;

            if (e.carrier != null)
            {
                float current = entries.ContainsKey(e.carrier) ? entries[e.carrier] : 0f;
                entries[e.carrier] = e.IsInfinite ? -1f : Mathf.Max(current, e.timer);
            }
            else
            {
                string key = e.id + "_" + Guid.NewGuid();
                entries[key] = e.IsInfinite ? -1f : e.timer;
            }
        }

        int idxHud = 0;
        foreach (var kv in entries)
        {
            GameObject go = Instantiate(statusEffectHudPrefab, statusEffectsHudRoot);
            float timeVal = kv.Value;

            if (idxHud == 0 && firstEffectBackground != null)
            {
                var bg = go.transform.Find("BG");
                if (bg != null)
                {
                    var bgImg = bg.GetComponent<Image>();
                    if (bgImg != null) bgImg.sprite = firstEffectBackground;
                }
            }

            if (kv.Key is EffectCarrier carrier)
            {
                Image iconImage = null;
                var iconTransform = go.transform.Find("Icon");
                if (iconTransform != null)
                    iconImage = iconTransform.GetComponent<Image>();
                if (iconImage == null)
                    iconImage = go.GetComponentInChildren<Image>();
                if (iconImage != null && carrier.icon != null)
                    iconImage.sprite = carrier.icon;

                var titleTransform = go.transform.Find("Title");
                if (titleTransform != null)
                {
                    var titleText = titleTransform.GetComponent<Text>();
                    if (titleText != null)
                        titleText.text = carrier.title ?? carrier.name;
                }

                var timeTransform = go.transform.Find("Time");
                if (timeTransform != null)
                {
                    var timeText = timeTransform.GetComponent<Text>();
                    if (timeText != null)
                        timeText.text = timeVal > 0f ? $"{Mathf.CeilToInt(timeVal)}s" : "";
                }
            }
            else
            {
                var titleTransform = go.transform.Find("Title");
                if (titleTransform != null)
                {
                    var titleText = titleTransform.GetComponent<Text>();
                    if (titleText != null)
                        titleText.text = kv.Key.ToString();
                }

                var timeTransform = go.transform.Find("Time");
                if (timeTransform != null)
                {
                    var timeText = timeTransform.GetComponent<Text>();
                    if (timeText != null)
                        timeText.text = timeVal < 0f ? "ON" : $"{Mathf.CeilToInt(timeVal)}s";
                }
            }

            idxHud++;
        }
    }

    void UpdateHands()
    {
        if (inventory == null)
            return;

        if (rightHandSlot != null)
        {
            rightHandSlot.SetItem(inventory.rightHandItem, -1, inventory);
            EnsurePersistentSelectedMarker(rightHandSlot, inventory.rightHandItem != null);
        }
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

            GameObject item = (inventory.backpack != null && i < inventory.backpack.Length)
                ? inventory.backpack[i]
                : null;

            slot.SetItem(item, i, inventory);
            EnsurePersistentSelectedMarker(slot, item != null);
        }
    }

    private void EnsurePersistentSelectedMarker(InventorySlotUI slot, bool hasItem)
    {
        if (slot == null) return;

        Transform existing = slot.transform.Find("PersistentSelectedMarker");
        if (!hasItem)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        GameObject prefab = null;
        switch (slot.slotType)
        {
            case InventorySlotUI.SlotType.Accessory: prefab = selectedPrefabAccessory; break;
            case InventorySlotUI.SlotType.RightHand: prefab = selectedPrefabEquipped; break;
            default: prefab = selectedPrefabBackpack; break;
        }

        if (prefab != null)
        {
            var inst = Instantiate(prefab, slot.transform);
            inst.name = "PersistentSelectedMarker";
            // place marker under the icon so it doesn't overlay the sprite
            Transform iconT = (slot != null && slot.icon != null) ? slot.icon.transform : null;
            if (iconT != null)
            {
                int idx = iconT.GetSiblingIndex();
                inst.transform.SetSiblingIndex(idx);
            }
            inst.transform.localPosition = Vector3.zero;
            // Disable raycast targets so marker does not block pointer events
            foreach (var img in inst.GetComponentsInChildren<Image>(true))
            {
                img.raycastTarget = false;
            }
            var cg = inst.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null) cg.blocksRaycasts = false;
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
            if (slot == null) continue;
            slot.SetItem(inventory.accessories[i], i, inventory);
            EnsurePersistentSelectedMarker(slot, inventory.accessories[i] != null);
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
            staminaText.text = $"{Mathf.RoundToInt(stats.stamina)} / {Mathf.RoundToInt(stats.maxStamina)}";

        if (intelligenceText != null)
            intelligenceText.text = $"{Mathf.RoundToInt(stats.effectiveIntelligence)}";

        if (strengthText != null)
            strengthText.text = $"{Mathf.RoundToInt(stats.effectiveStrength)}";

        if (staminaAttrText != null)
            staminaAttrText.text = $"{Mathf.RoundToInt(stats.effectiveStaminaAttr)}";

        if (agilityText != null)
            agilityText.text = $"{Mathf.RoundToInt(stats.effectiveAgility)}";
    }

    void HandleInventoryChanged()
    {
        UpdateBackpackSlots();
        UpdateHands();
        UpdateAccessories();
    }

    public void StartDrag(GameObject item, Sprite icon, InventorySlotUI source, int sourceIndex = -1)
    {
        if (!IsBackpackOpen || item == null)
            return;

        currentlyDraggedItem = item;
        dragSourceSlot = source;
        dragSourceIndex = sourceIndex;

        CreateDragIcon(icon ?? GetIconFromItem(item));
        PlaySound(clickSound);

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
        Camera cam = uiCanvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCanvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out Vector2 localPos);
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
        rt.sizeDelta = sprite != null ? new Vector2(sprite.rect.width, sprite.rect.height) : new Vector2(72, 72);
    }

    private Sprite GetIconFromItem(GameObject item) => ItemTooltipDataUtility.GetInventoryIcon(item);

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
            PlaySound(dropSuccessSound);
        }
        else if (srcType == InventorySlotUI.SlotType.Backpack && dstType == InventorySlotUI.SlotType.RightHand)
        {
            inventory.SwapRightHandWithBackpack(srcIndex);
            PlaySound(dropSuccessSound);
        }
        else if (srcType == InventorySlotUI.SlotType.RightHand && dstType == InventorySlotUI.SlotType.Backpack)
        {
            inventory.SwapRightHandWithBackpack(dstIndex);
            PlaySound(dropSuccessSound);
        }
        else if (dstType == InventorySlotUI.SlotType.WandInternal)
        {
            var scroll = currentlyDraggedItem.GetComponent<ScrollItem>();
            if (scroll == null)
            {
                Debug.Log("Only spell ScrollItems can be placed into wand slots.");
                PlaySound(dropInvalidSound);
                EndDrag();
                return;
            }

            var wandOwner = targetSlot.wandOwner;
            int wandIndex = targetSlot.wandSlotIndex;
            if (wandOwner == null || wandIndex < 0)
            {
                PlaySound(dropInvalidSound);
                EndDrag();
                return;
            }

            if (srcType == InventorySlotUI.SlotType.Backpack)
            {
                if (srcIndex >= 0 && srcIndex < inventory.backpack.Length)
                {
                    GameObject prev = wandOwner.RemoveSlotItem(wandIndex);
                    bool ok = wandOwner.SetSlotItem(wandIndex, currentlyDraggedItem);
                    if (!ok)
                    {
                        if (prev != null)
                            wandOwner.SetSlotItem(wandIndex, prev);
                        PlaySound(dropInvalidSound);
                    }
                    else
                    {
                        inventory.backpack[srcIndex] = prev;
                        if (prev != null) prev.SetActive(false);

                        targetSlot.SetItem(wandOwner.GetSlotItem(wandIndex), wandIndex, inventory);
                        if (srcIndex < backpackSlots.Length && backpackSlots[srcIndex] != null)
                            backpackSlots[srcIndex].SetItem(inventory.backpack[srcIndex], srcIndex, inventory);

                        PlaySound(dropSuccessSound);
                    }
                }
            }
            else if (srcType == InventorySlotUI.SlotType.RightHand)
            {
                inventory.rightHandItem = null;
                wandOwner.SetSlotItem(wandIndex, currentlyDraggedItem);
                targetSlot.SetItem(wandOwner.GetSlotItem(wandIndex), wandIndex, inventory);
                if (rightHandSlot != null)
                    rightHandSlot.SetItem(inventory.rightHandItem, -1, inventory);
                PlaySound(dropSuccessSound);
            }
            else if (srcType == InventorySlotUI.SlotType.WandInternal)
            {
                var srcWand = dragSourceSlot.wandOwner;
                int srcWandIndex = dragSourceSlot.wandSlotIndex;
                if (srcWand != null && srcWand == wandOwner)
                {
                    srcWand.SwapInternal(srcWandIndex, wandIndex);
                    targetSlot.SetItem(wandOwner.GetSlotItem(wandIndex), wandIndex, inventory);
                    dragSourceSlot.SetItem(srcWand.GetSlotItem(srcWandIndex), srcWandIndex, inventory);
                    PlaySound(dropSuccessSound);
                }
                else
                {
                    PlaySound(dropInvalidSound);
                }
            }
        }
        else if (srcType == InventorySlotUI.SlotType.WandInternal)
        {
            var srcWand = dragSourceSlot.wandOwner;
            int srcWandIndex = dragSourceSlot.wandSlotIndex;
            if (srcWand != null && srcWandIndex >= 0)
            {
                var item = srcWand.RemoveSlotItem(srcWandIndex);
                if (item != null)
                {
                    if (dstType == InventorySlotUI.SlotType.Backpack)
                    {
                        if (dstIndex >= 0 && dstIndex < inventory.backpack.Length)
                        {
                            inventory.backpack[dstIndex] = item;
                            item.SetActive(false);
                            if (dstIndex < backpackSlots.Length && backpackSlots[dstIndex] != null)
                                backpackSlots[dstIndex].SetItem(inventory.backpack[dstIndex], dstIndex, inventory);
                            dragSourceSlot.SetItem(srcWand.GetSlotItem(srcWandIndex), srcWandIndex, inventory);
                            PlaySound(dropSuccessSound);
                        }
                    }
                    else if (dstType == InventorySlotUI.SlotType.RightHand)
                    {
                        inventory.EquipRight(item);
                        if (rightHandSlot != null)
                            rightHandSlot.SetItem(inventory.rightHandItem, -1, inventory);
                        dragSourceSlot.SetItem(srcWand.GetSlotItem(srcWandIndex), srcWandIndex, inventory);
                        PlaySound(dropSuccessSound);
                    }
                    else
                    {
                        PlaySound(dropInvalidSound);
                    }
                }
            }
        }
        else if (dstType == InventorySlotUI.SlotType.RightHand && srcType == InventorySlotUI.SlotType.Accessory)
        {
            // Move accessory from accessory slot into right hand
            int srcAccIndex = dragSourceIndex;
            if (srcAccIndex >= 0 && srcAccIndex < inventory.accessories.Length)
            {
                GameObject moving = inventory.accessories[srcAccIndex];
                if (moving != null)
                {
                    var accComp = moving.GetComponent<Accessory>();
                    if (accComp != null)
                    {
                        // Unequip accessory and equip to hand
                        accComp.OnUnequipped();

                        inventory.accessories[srcAccIndex] = null;

                        // place in hand
                        inventory.rightHandItem = moving;
                        moving.transform.SetParent(inventory.rightHand, false);
                        moving.transform.localPosition = Vector3.zero;
                        moving.transform.localRotation = Quaternion.identity;

                        var rb = moving.GetComponent<Rigidbody>();
                        if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }
                        foreach (var col in moving.GetComponentsInChildren<Collider>()) col.enabled = false;
                        moving.SetActive(true);

                        if (srcAccIndex < accessorySlots.Length && accessorySlots[srcAccIndex] != null)
                            accessorySlots[srcAccIndex].SetItem(inventory.accessories[srcAccIndex], srcAccIndex, inventory);
                        if (rightHandSlot != null)
                            rightHandSlot.SetItem(inventory.rightHandItem, -1, inventory);

                        PlaySound(dropSuccessSound);
                    }
                    else
                    {
                        PlaySound(dropInvalidSound);
                    }
                }
            }
        }
        else if (dstType == InventorySlotUI.SlotType.Accessory)
        {
            var accComp = currentlyDraggedItem.GetComponent<Accessory>();
            if (accComp == null)
            {
                Debug.Log("Only Accessory items can be placed into accessory slots.");
                PlaySound(dropInvalidSound);
                EndDrag();
                return;
            }

            if (srcType == InventorySlotUI.SlotType.Backpack)
            {
                if (srcIndex >= 0 && srcIndex < inventory.backpack.Length && dstIndex >= 0 && dstIndex < inventory.accessories.Length)
                {
                    GameObject prevAcc = inventory.accessories[dstIndex];

                    inventory.accessories[dstIndex] = currentlyDraggedItem;
                    accComp.OnEquipped(inventory.gameObject);
                    currentlyDraggedItem.SetActive(false);

                    inventory.backpack[srcIndex] = prevAcc;
                    if (prevAcc != null)
                    {
                        var prevAccComp = prevAcc.GetComponent<Accessory>();
                        if (prevAccComp != null) prevAccComp.OnUnequipped();
                        prevAcc.SetActive(false);
                    }

                    if (dstIndex < accessorySlots.Length && accessorySlots[dstIndex] != null)
                        accessorySlots[dstIndex].SetItem(inventory.accessories[dstIndex], dstIndex, inventory);
                    if (srcIndex < backpackSlots.Length && backpackSlots[srcIndex] != null)
                        backpackSlots[srcIndex].SetItem(inventory.backpack[srcIndex], srcIndex, inventory);

                    PlaySound(dropSuccessSound);
                }
            }
            else if (srcType == InventorySlotUI.SlotType.Accessory)
            {
                int srcAccIndex = dragSourceIndex;
                if (srcAccIndex >= 0 && srcAccIndex < inventory.accessories.Length && dstIndex >= 0 && dstIndex < inventory.accessories.Length)
                {
                    GameObject from = inventory.accessories[srcAccIndex];
                    GameObject to = inventory.accessories[dstIndex];

                    var fromAcc = from != null ? from.GetComponent<Accessory>() : null;
                    var toAcc = to != null ? to.GetComponent<Accessory>() : null;

                    if (fromAcc != null) fromAcc.OnUnequipped();
                    if (toAcc != null) toAcc.OnUnequipped();

                    inventory.accessories[srcAccIndex] = to;
                    inventory.accessories[dstIndex] = from;

                    if (inventory.accessories[srcAccIndex] != null)
                    {
                        var comp = inventory.accessories[srcAccIndex].GetComponent<Accessory>();
                        if (comp != null) comp.OnEquipped(inventory.gameObject);
                        inventory.accessories[srcAccIndex].SetActive(false);
                    }
                    if (inventory.accessories[dstIndex] != null)
                    {
                        var comp = inventory.accessories[dstIndex].GetComponent<Accessory>();
                        if (comp != null) comp.OnEquipped(inventory.gameObject);
                        inventory.accessories[dstIndex].SetActive(false);
                    }

                    if (dstIndex < accessorySlots.Length && accessorySlots[dstIndex] != null)
                        accessorySlots[dstIndex].SetItem(inventory.accessories[dstIndex], dstIndex, inventory);
                    if (srcAccIndex < accessorySlots.Length && accessorySlots[srcAccIndex] != null)
                        accessorySlots[srcAccIndex].SetItem(inventory.accessories[srcAccIndex], srcAccIndex, inventory);

                    PlaySound(dropSuccessSound);
                }
            }
            else if (srcType == InventorySlotUI.SlotType.RightHand)
            {
                if (dstIndex >= 0 && dstIndex < inventory.accessories.Length)
                {
                    GameObject prevAcc = inventory.accessories[dstIndex];

                    inventory.accessories[dstIndex] = currentlyDraggedItem;
                    accComp.OnEquipped(inventory.gameObject);
                    inventory.accessories[dstIndex].SetActive(false);

                    inventory.rightHandItem = null;
                    if (rightHandSlot != null)
                        rightHandSlot.SetItem(inventory.rightHandItem, -1, inventory);

                    if (prevAcc != null)
                    {
                        var prevAccComp = prevAcc.GetComponent<Accessory>();
                        if (prevAccComp != null) prevAccComp.OnUnequipped();
                        prevAcc.SetActive(false);
                    }

                    if (dstIndex < accessorySlots.Length && accessorySlots[dstIndex] != null)
                        accessorySlots[dstIndex].SetItem(inventory.accessories[dstIndex], dstIndex, inventory);

                    PlaySound(dropSuccessSound);
                }
            }
            else
            {
                PlaySound(dropInvalidSound);
            }
        }
        else if (srcType == InventorySlotUI.SlotType.Accessory && dstType == InventorySlotUI.SlotType.Backpack)
        {
            int srcAccIndex = dragSourceIndex;
            if (srcAccIndex >= 0 && srcAccIndex < inventory.accessories.Length && dstIndex >= 0 && dstIndex < inventory.backpack.Length)
            {
                GameObject moving = inventory.accessories[srcAccIndex];
                GameObject prevBackpack = inventory.backpack[dstIndex];
                var movingAcc = moving != null ? moving.GetComponent<Accessory>() : null;
                if (movingAcc != null) movingAcc.OnUnequipped();

                // If the backpack item is an Accessory, swap into the accessory slot.
                var packAcc = prevBackpack != null ? prevBackpack.GetComponent<Accessory>() : null;
                if (packAcc != null)
                {
                    inventory.accessories[srcAccIndex] = prevBackpack;
                    inventory.backpack[dstIndex] = moving;

                    // Equip the new accessory in the slot
                    if (inventory.accessories[srcAccIndex] != null)
                    {
                        var comp = inventory.accessories[srcAccIndex].GetComponent<Accessory>();
                        if (comp != null) comp.OnEquipped(inventory.gameObject);
                        inventory.accessories[srcAccIndex].SetActive(false);
                    }

                    if (inventory.backpack[dstIndex] != null)
                        inventory.backpack[dstIndex].SetActive(false);

                    if (dstIndex < backpackSlots.Length && backpackSlots[dstIndex] != null)
                        backpackSlots[dstIndex].SetItem(inventory.backpack[dstIndex], dstIndex, inventory);
                    if (srcAccIndex < accessorySlots.Length && accessorySlots[srcAccIndex] != null)
                        accessorySlots[srcAccIndex].SetItem(inventory.accessories[srcAccIndex], srcAccIndex, inventory);

                    PlaySound(dropSuccessSound);
                }
                else
                {
                    // Backpack item is not an accessory: just move the accessory into the backpack slot and clear accessory slot.
                    inventory.backpack[dstIndex] = moving;
                    inventory.accessories[srcAccIndex] = null;

                    if (inventory.backpack[dstIndex] != null)
                        inventory.backpack[dstIndex].SetActive(false);

                    if (dstIndex < backpackSlots.Length && backpackSlots[dstIndex] != null)
                        backpackSlots[dstIndex].SetItem(inventory.backpack[dstIndex], dstIndex, inventory);
                    if (srcAccIndex < accessorySlots.Length && accessorySlots[srcAccIndex] != null)
                        accessorySlots[srcAccIndex].SetItem(inventory.accessories[srcAccIndex], srcAccIndex, inventory);

                    PlaySound(dropSuccessSound);
                }
            }
        }
        else
        {
            Debug.Log("PlayerUI: Drop combination not handled.");
            PlaySound(dropInvalidSound);
        }

        EndDrag();
    }

    public bool TryConsumeHoveredBackpackItem(GameObject user)
    {
        if (!IsBackpackOpen || inventory == null) return false;

        var slot = InventorySlotUI.HoveredSlot;
        if (slot == null || slot.slotType != InventorySlotUI.SlotType.Backpack || slot.slotIndex < 0)
            return false;

        return inventory.ConsumeFromBackpack(slot.slotIndex, user);
    }

    public bool TryDropHoveredBackpackItem(Transform dropOrigin)
    {
        if (!IsBackpackOpen || inventory == null) return false;

        var slot = InventorySlotUI.HoveredSlot;
        if (slot == null || slot.slotType != InventorySlotUI.SlotType.Backpack || slot.slotIndex < 0)
            return false;

        return inventory.DropFromBackpack(slot.slotIndex, dropOrigin);
    }

    public bool TryDropHoveredAccessoryItem(Transform dropOrigin)
    {
        if (!IsBackpackOpen || inventory == null) return false;

        var slot = InventorySlotUI.HoveredSlot;
        if (slot == null || slot.slotType != InventorySlotUI.SlotType.Accessory || slot.slotIndex < 0)
            return false;

        return inventory.DropFromAccessory(slot.slotIndex, dropOrigin);
    }

    public void ToggleBackpack()
    {
        if (backpackRoot == null)
            return;

        bool newState = !backpackRoot.activeSelf;
        backpackRoot.SetActive(newState);

        if (hudRoot != null)
            hudRoot.SetActive(!newState);

        if (!newState) EndDrag();
        if (!newState) HideWandPanel();

        PlaySound(newState ? backpackOpenSound : backpackCloseSound);
        // When opening the backpack, default the description to the held item.
        if (newState)
        {
            if (descriptionPanelInstance != null)
            {
                if (inventory != null && inventory.rightHandItem != null)
                {
                    descriptionPanelInstance.Populate(inventory.rightHandItem.GetComponent<ItemPickup>());
                    descriptionPanelInstance.gameObject.SetActive(true);
                }
                else
                {
                    // if no held item but inventory has items, try first backpack slot
                    GameObject first = null;
                    if (inventory != null && inventory.backpack != null && inventory.backpack.Length > 0)
                        first = inventory.backpack[0];

                    if (first != null)
                    {
                        descriptionPanelInstance.Populate(first.GetComponent<ItemPickup>());
                        descriptionPanelInstance.gameObject.SetActive(true);
                    }
                    else
                    {
                        // hide only if inventory empty
                        bool any = false;
                        if (inventory != null)
                        {
                            if (inventory.rightHandItem != null) any = true;
                            if (!any && inventory.backpack != null)
                            {
                                foreach (var it in inventory.backpack) if (it != null) { any = true; break; }
                            }
                            if (!any && inventory.accessories != null)
                            {
                                foreach (var it in inventory.accessories) if (it != null) { any = true; break; }
                            }
                        }

                        if (!any && descriptionPanelInstance != null)
                            descriptionPanelInstance.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public void NotifySlotClicked(InventorySlotUI slot)
    {
        if (!IsBackpackOpen)
            return;

        GameObject item = null;
        switch (slot.slotType)
        {
            case InventorySlotUI.SlotType.Backpack:
                if (slot.slotIndex >= 0 && slot.slotIndex < inventory.backpack.Length)
                    item = inventory.backpack[slot.slotIndex];
                break;
            case InventorySlotUI.SlotType.RightHand:
                item = inventory.rightHandItem;
                break;
            case InventorySlotUI.SlotType.Accessory:
                if (slot.slotIndex >= 0 && slot.slotIndex < inventory.accessories.Length)
                    item = inventory.accessories[slot.slotIndex];
                break;
            default:
                HideWandPanel();
                return;
        }

        var wand = item != null ? item.GetComponent<WandItem>() : null;

        // Update selection visuals for clicked slot
        UpdateSelectionForSlot(slot);

        if (wand == null)
        {
            if (wandPanelInstance != null && wandPanelInstance.IsForSource(slot))
                HideWandPanel();
            return;
        }

        if (wandPanelInstance != null && wandPanelInstance.IsForSource(slot))
            HideWandPanel();
        else
        {
            PlaySound(clickSound);
            ShowWandPanel(wand, slot);
        }
    }

    private void UpdateSelectionForSlot(InventorySlotUI slot)
    {

        // If there's no slot or the slot contains no item, do not change the current description.
        if (slot == null) return;

        GameObject item = GetItemFromSlot(slot);
        if (item == null) return;

        // We're going to select a new valid item: clear previous selection visuals
        if (selectedSlot != null && selectedSlot != slot && selectedSlot.icon != null)
        {
            selectedSlot.icon.rectTransform.localScale = Vector3.one;
        }

        if (selectedMarkerInstance != null && selectedSlot != slot)
        {
            // If this was a persistent marker, only remove its highlighted child
            if (selectedMarkerInstance.name == "PersistentSelectedMarker")
            {
                var prev = selectedMarkerInstance.transform.Find("HighlightedMarker");
                if (prev != null) Destroy(prev.gameObject);
            }
            else
            {
                Destroy(selectedMarkerInstance);
                selectedMarkerInstance = null;
            }
        }

        selectedSlot = slot;

        // Grow icon
        if (slot.icon != null)
            slot.icon.rectTransform.localScale = Vector3.one * 1.2f;

        // Highlight the persistent selected marker when description is shown
        Transform persistent = slot.transform.Find("PersistentSelectedMarker");
        // Determine icon sibling index so markers are placed under the icon
        int iconSibling = 0;
        Transform iconTransform = (slot.icon != null) ? slot.icon.transform : null;
        if (iconTransform != null)
            iconSibling = iconTransform.GetSiblingIndex();

        if (persistent == null)
        {
            // fallback: create a transient marker if none exists
            GameObject prefab = null;
            switch (slot.slotType)
            {
                case InventorySlotUI.SlotType.Accessory: prefab = selectedPrefabAccessory; break;
                case InventorySlotUI.SlotType.RightHand: prefab = selectedPrefabEquipped; break;
                default: prefab = selectedPrefabBackpack; break;
            }
            if (prefab != null)
            {
                selectedMarkerInstance = Instantiate(prefab, slot.transform);
                // place under the icon so it does not overlay the sprite
                if (iconTransform != null)
                    selectedMarkerInstance.transform.SetSiblingIndex(iconSibling);
                else
                    selectedMarkerInstance.transform.SetAsFirstSibling();
            }
        }
        else
        {
            selectedMarkerInstance = persistent.gameObject;
        }

        // Populate the existing description container instance (do not instantiate new)
        if (descriptionPanelInstance != null)
        {
            var pickup = item.GetComponent<ItemPickup>();
            descriptionPanelInstance.Populate(pickup);
            // Ensure description remains visible when inventory has items
            bool any = false;
            if (inventory != null)
            {
                if (inventory.rightHandItem != null) any = true;
                if (!any && inventory.backpack != null)
                {
                    foreach (var it in inventory.backpack) if (it != null) { any = true; break; }
                }
                if (!any && inventory.accessories != null)
                {
                    foreach (var it in inventory.accessories) if (it != null) { any = true; break; }
                }
            }
            descriptionPanelInstance.gameObject.SetActive(any);

            // Activate highlighted prefab under the persistent marker (keep it unscaled and under icon)
            if (selectedMarkerInstance != null)
            {
                var prev = selectedMarkerInstance.transform.Find("HighlightedMarker");
                if (prev != null) Destroy(prev.gameObject);

                GameObject hpf = null;
                switch (slot.slotType)
                {
                    case InventorySlotUI.SlotType.Accessory: hpf = highlightedPrefabAccessory; break;
                    case InventorySlotUI.SlotType.RightHand: hpf = highlightedPrefabEquipped; break;
                    default: hpf = highlightedPrefabBackpack; break;
                }

                if (hpf != null)
                {
                    var h = Instantiate(hpf, selectedMarkerInstance.transform);
                    h.name = "HighlightedMarker";
                    // keep prefab scale as authored
                    h.transform.localPosition = Vector3.zero;
                }
            }
        }
    }

    private void ShowWandPanel(WandItem wand, InventorySlotUI source)
    {
        HideWandPanel();
        if (wandSlotsPanelPrefab == null)
        {
            Debug.LogWarning("Assign wandSlotsPanelPrefab in PlayerUI to show wand slots.");
            return;
        }

        wandPanelInstance = Instantiate(wandSlotsPanelPrefab);
        wandPanelInstance.Show(wand, source, this);
        wandSourceSlot = source;
    }

    private void HideWandPanel()
    {
        if (wandPanelInstance != null)
        {
            wandPanelInstance.Hide();
            Destroy(wandPanelInstance.gameObject);
            wandPanelInstance = null;
        }

        wandSourceSlot = null;
    }

    private void PlaySound(AudioSource source)
    {
        if (source != null) source.Play();
    }
}
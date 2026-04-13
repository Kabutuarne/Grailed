using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WandSlotsPanel : MonoBehaviour
{
    [Header("References")]
    public InventorySlotUI slotTemplate;

    [Header("Selection Visuals")]

    private WandItem wand;
    private PlayerUI playerUI;
    private InventorySlotUI sourceSlot;

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

    private int lastSelectedIndex = -1;

    public bool IsShowing => wand != null && sourceSlot != null;
    public bool IsForSource(InventorySlotUI src) => IsShowing && sourceSlot == src;

    public void Show(WandItem wandItem, InventorySlotUI wandSourceSlot, PlayerUI ui)
    {
        // Ensure we have a template reference first (do not Clear template)
        if (slotTemplate == null)
            slotTemplate = GetComponentInChildren<InventorySlotUI>(true);

        if (slotTemplate == null)
        {
            Debug.LogWarning("WandSlotsPanel: slotTemplate is not assigned and no child InventorySlotUI was found.");
            return;
        }

        // If the template is a child of this panel, keep it hidden and treat it as a template only
        bool templateIsChild = slotTemplate.transform.IsChildOf(transform);
        if (templateIsChild)
            slotTemplate.gameObject.SetActive(false);

        // Clear any previous instantiated slots
        Clear();

        wand = wandItem;
        playerUI = ui;
        sourceSlot = wandSourceSlot;
        if (wand == null || playerUI == null) return;

        int n = Mathf.Max(1, wand.SlotCount);

        // Ensure panel is active so layout can be rebuilt while we add children
        gameObject.SetActive(true);

        // If the panel prefab contains a GridLayoutGroup (content container), instantiate slots under it.
        Transform container = transform;
        var grid = GetComponentInChildren<GridLayoutGroup>(true);
        if (grid != null) container = grid.transform;

        for (int i = 0; i < n; i++)
        {
            var uiSlot = Instantiate(slotTemplate, container);
            uiSlot.gameObject.SetActive(true);
            uiSlot.slotType = InventorySlotUI.SlotType.WandInternal;
            uiSlot.wandOwner = wand;
            uiSlot.wandSlotIndex = i;
            uiSlot.inventory = playerUI.inventory;
            // If an overlay child exists on the instantiated slot, ensure it's disabled before we populate
            // so that `SetItem` can trigger the entry animation when appropriate.
            var overlayComp = uiSlot.GetComponentInChildren<WandSlotOverlay>(true);
            if (overlayComp != null) overlayComp.ForceDisable();

            uiSlot.SetItem(wand.GetSlotItem(i), i, playerUI.inventory);

            if (uiSlot.previewRaw != null)
                uiSlot.previewRaw.rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);

            slotUIs.Add(uiSlot);
        }

        // Force layout rebuild so instantiated slots are arranged immediately
        var rect = container as RectTransform;
        if (rect != null)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        // Do not rely on equipment state for panel visibility — selection is handled by PlayerUI.
        EnsureSelectionIfNeeded();
        lastSelectedIndex = GetEffectiveSelectedIndex();
        UpdateSelectionVisuals();
    }

    public void Hide()
    {
        Clear();
        gameObject.SetActive(false);
    }

    private void Clear()
    {
        foreach (var s in slotUIs)
        {
            if (s == null) continue;
            // avoid accidentally destroying the scene template if it was referenced
            if (slotTemplate != null && s.gameObject == slotTemplate.gameObject)
                continue;
            Destroy(s.gameObject);
        }
        slotUIs.Clear();
        wand = null;
        sourceSlot = null;
        lastSelectedIndex = -1;
    }

    void Update()
    {
        if (!gameObject.activeSelf || wand == null) return;

        int effective = GetEffectiveSelectedIndex();
        if (effective != lastSelectedIndex)
        {
            lastSelectedIndex = effective;
            UpdateSelectionVisuals();
        }
    }

    private void UpdateSelectionVisuals()
    {
        int effective = GetEffectiveSelectedIndex();
        for (int i = 0; i < slotUIs.Count; i++)
        {
            var ui = slotUIs[i];
            if (ui == null) continue;

            bool isSelected = (ui.wandSlotIndex == effective);

            // Only toggle the selected indicator; do not change slot scale here.
            Transform indicatorT = ui.transform.Find("SelectedIndicator");
            if (indicatorT != null)
                indicatorT.gameObject.SetActive(isSelected);
        }
    }

    private int GetEffectiveSelectedIndex()
    {
        if (wand == null) return -1;
        int sel = wand.SelectedIndex;
        if (sel < 0 || sel >= wand.SlotCount) return -1;
        if (wand.GetSlotItem(sel) == null) return -1;
        return sel;
    }

    private bool IsEquipped() => playerUI?.inventory?.rightHandItem == wand?.gameObject;

    private void EnsureSelectionIfNeeded()
    {
        if (wand == null || !IsEquipped() || wand.SelectedIndex >= 0) return;

        for (int i = 0; i < wand.SlotCount; i++)
        {
            if (wand.GetSlotItem(i) != null)
            {
                wand.SelectedIndex = i;
                break;
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI panel that shows the wand's internal spell slots next to the wand's inventory slot.
public class WandSlotsPanel : MonoBehaviour
{
    [Header("Layout")]
    public InventorySlotUI slotTemplate;
    public float slotSpacingY = 12f;
    public float panelMarginLeft = 24f;
    public TextAnchor alignment = TextAnchor.UpperLeft;
    private VerticalLayoutGroup layoutGroup;
    private ContentSizeFitter sizeFitter;
    [Header("Selection Visuals")]
    public float selectedScale = 0.9f;
    public float normalScale = 1f;
    public Sprite selectedBGSprite;

    private WandItem wand;
    private PlayerUI playerUI;
    private InventorySlotUI sourceSlot;
    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private List<Sprite> originalBGSprites = new List<Sprite>();
    private List<Image> bgImages = new List<Image>();
    private List<RectTransform> slotRects = new List<RectTransform>();
    private int lastSelectedIndex = -1; // stores effective selection used for visuals
    private bool wasEquipped = false;

    public bool IsShowing => wand != null && sourceSlot != null;

    public bool IsForSource(InventorySlotUI src) => IsShowing && sourceSlot == src;

    public void Show(WandItem wandItem, InventorySlotUI wandSourceSlot, PlayerUI ui)
    {
        Clear();
        wand = wandItem;
        playerUI = ui;
        sourceSlot = wandSourceSlot;

        if (slotTemplate == null || wand == null || playerUI == null || sourceSlot == null)
        {
            Debug.LogWarning("WandSlotsPanel: Missing template or references.");
            return;
        }

        // Parent under the same canvas as PlayerUI
        var canvas = playerUI.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        transform.SetParent(canvas != null ? canvas.transform : playerUI.transform, false);

        // Ensure we have a layout group to manage child slot positions
        var myRect = GetComponent<RectTransform>();
        layoutGroup = GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
            layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = slotSpacingY;
        layoutGroup.childAlignment = alignment;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        sizeFitter = GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
            sizeFitter = gameObject.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int n = Mathf.Max(1, wand.SlotCount);
        for (int i = 0; i < n; i++)
        {
            var uiSlot = Instantiate(slotTemplate, transform);
            uiSlot.slotType = InventorySlotUI.SlotType.WandInternal;
            uiSlot.wandOwner = wand;
            uiSlot.wandSlotIndex = i;
            uiSlot.inventory = playerUI.inventory;
            uiSlot.SetItem(wand.GetSlotItem(i), i, playerUI.inventory);

            var rt = uiSlot.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Let layout group manage positioning; normalize anchors for consistency
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
            slotUIs.Add(uiSlot);
            slotRects.Add(rt);

            // cache original BG sprite if present
            Sprite orig = null;
            var bgT = uiSlot.transform.Find("BG");
            Image bgImg = null;
            if (bgT != null)
            {
                var img = bgT.GetComponent<Image>();
                if (img != null) { orig = img.sprite; bgImg = img; }
            }
            originalBGSprites.Add(orig);
            bgImages.Add(bgImg);

            // Make the spell preview smaller than the slot background
            if (uiSlot != null && uiSlot.previewRaw != null)
            {
                uiSlot.previewRaw.rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);
            }
        }

        UpdateLayout();
        wasEquipped = IsEquipped();
        EnsureSelectionIfNeeded();
        lastSelectedIndex = GetEffectiveSelectedIndex();
        UpdateSelectionVisuals();
        gameObject.SetActive(true);
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
            if (s != null) Destroy(s.gameObject);
        }
        slotUIs.Clear();
        originalBGSprites.Clear();
        bgImages.Clear();
        slotRects.Clear();
        wand = null;
        playerUI = null;
        sourceSlot = null;
        lastSelectedIndex = -1;
    }

    public void UpdateLayout()
    {
        if (sourceSlot == null) return;
        var srcRect = sourceSlot.GetComponent<RectTransform>();
        var myRect = GetComponent<RectTransform>();
        if (srcRect == null || myRect == null) return;

        // Align panel to the left side of the wand slot
        var canvas = myRect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        Vector3[] corners = new Vector3[4];
        srcRect.GetWorldCorners(corners);
        Vector3 leftCenter = (corners[0] + corners[3]) * 0.5f; // left side midpoint

        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(cam, leftCenter), cam, out localPos);
        myRect.localPosition = localPos + new Vector2(-panelMarginLeft, 0f);

        // Layout handled by VerticalLayoutGroup
    }

    void Update()
    {
        if (wand == null) return;
        bool equipped = IsEquipped();
        if (equipped != wasEquipped)
        {
            // If the wand just became equipped, anchor panel to right-hand slot
            if (equipped && playerUI != null && playerUI.rightHandSlot != null)
            {
                sourceSlot = playerUI.rightHandSlot;
                UpdateLayout();
            }
            if (equipped)
                EnsureSelectionIfNeeded();
            wasEquipped = equipped;
            UpdateSelectionVisuals();
        }

        int effective = GetEffectiveSelectedIndex();
        if (effective != lastSelectedIndex)
        {
            lastSelectedIndex = effective;
            UpdateSelectionVisuals();
        }
    }

    private void UpdateSelectionVisuals()
    {
        if (slotUIs == null || slotUIs.Count == 0) return;
        int effective = GetEffectiveSelectedIndex();
        for (int i = 0; i < slotUIs.Count; i++)
        {
            var uiSlot = slotUIs[i];
            if (uiSlot == null) continue;
            var rt = (i < slotRects.Count) ? slotRects[i] : uiSlot.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = (i == effective) ? Vector3.one * selectedScale : Vector3.one * normalScale;
            }
            var img = (i < bgImages.Count) ? bgImages[i] : null;
            if (img != null)
            {
                if (i == effective && selectedBGSprite != null)
                    img.sprite = selectedBGSprite;
                else
                    img.sprite = (i < originalBGSprites.Count) ? originalBGSprites[i] : img.sprite;
            }
        }
    }

    private int GetEffectiveSelectedIndex()
    {
        if (wand == null) return -1;
        // Only show selection when the wand is actually equipped in hand
        bool equipped = IsEquipped();
        if (!equipped) return -1;

        // If the wand has no spells, do not show selection
        bool anySpell = false;
        int n = wand.SlotCount;
        for (int i = 0; i < n; i++)
        {
            if (wand.GetSlotItem(i) != null) { anySpell = true; break; }
        }
        if (!anySpell) return -1;

        return wand.SelectedIndex;
    }

    private bool IsEquipped()
    {
        if (playerUI == null || playerUI.inventory == null || wand == null) return false;
        return playerUI.inventory.rightHandItem == wand.gameObject;
    }

    private void EnsureSelectionIfNeeded()
    {
        if (wand == null) return;
        if (!IsEquipped()) return;
        if (wand.SelectedIndex >= 0) return;
        int n = wand.SlotCount;
        for (int i = 0; i < n; i++)
        {
            if (wand.GetSlotItem(i) != null)
            {
                wand.SelectedIndex = i;
                break;
            }
        }
    }
}

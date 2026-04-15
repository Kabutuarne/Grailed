using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Component that represents a description container prefab in the UI.
// It stores tag color settings and exposes a Populate method to fill
// Title, Description and up to 5 lines.
public class ItemDescriptionContainer : MonoBehaviour
{
    [Header("Tag Colors")]
    public Color colorDescription = new Color(0xC4 / 255f, 0xB4 / 255f, 0x8A / 255f);
    public Color colorIntelligence = new Color(0x7c / 255f, 0x35 / 255f, 0xc0 / 255f);
    public Color colorStrength = new Color(0xc0 / 255f, 0x24 / 255f, 0x24 / 255f);
    public Color colorStamina = new Color(0xca / 255f, 0x6f / 255f, 0x1e / 255f);
    public Color colorAgility = new Color(0x23 / 255f, 0x9b / 255f, 0x56 / 255f);

    [Header("UI Refs")]
    public Text titleText;
    public Text descriptionText;
    public List<Text> lineTexts = new List<Text>(5);

    [Header("Wand Slots")]
    public WandSlotsPanel wandSlotsPanelPrefab;
    private WandSlotsPanel wandPanelInstance;
    private InventorySlotUI wandSourceSlot;
    private WandItem currentWand;
    private PlayerUI ownerUI;

    // Sets title, description and lines using ItemPickup data. Expects up to 5 lines.
    public void Populate(ItemPickup pickup)
    {
        if (pickup == null) return;

        currentPickup = pickup;

        if (titleText != null)
        {
            titleText.text = pickup.TooltipTitle;
            titleText.color = new Color(0xF2 / 255f, 0xE6 / 255f, 0xC8 / 255f);
        }

        if (descriptionText != null) descriptionText.text = string.Empty;
        foreach (var t in lineTexts) { if (t != null) t.text = string.Empty; }

        var allLines = pickup.GetItemLines();
        if (allLines == null) return;

        int uiLineIndex = 0;
        foreach (var data in allLines)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.text)) continue;

            if (data.tag == ItemLineData.LineTag.Description)
            {
                if (descriptionText != null)
                {
                    descriptionText.text = data.text;
                    descriptionText.color = colorDescription;
                }
            }
            else if (uiLineIndex < lineTexts.Count)
            {
                var t = lineTexts[uiLineIndex++];
                if (t != null)
                {
                    t.text = data.text;
                    t.color = GetColorForTag(data.tag);
                }
            }
        }
    }

    private ItemPickup currentPickup;

    public ItemPickup CurrentPickup => currentPickup;

    // Clear and hide the description container (used when the described item is removed)
    public void Clear()
    {
        currentPickup = null;
        if (titleText != null) titleText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        foreach (var t in lineTexts) { if (t != null) t.text = string.Empty; }
        HideWandSlots();
        gameObject.SetActive(false);
    }
    // Show wand slots associated with this description. If prefab is set it will be instantiated
    // as a child under this container. Passing null wand will hide any existing wand panel.
    public void ShowWandSlots(WandItem wand, InventorySlotUI sourceSlot, PlayerUI ui)
    {
        if (wand == null)
        {
            HideWandSlots();
            return;
        }

        if (wandPanelInstance == null)
        {
            if (wandSlotsPanelPrefab != null)
            {
                var go = Instantiate(wandSlotsPanelPrefab.gameObject, transform);
                wandPanelInstance = go.GetComponent<WandSlotsPanel>();
                if (wandPanelInstance == null)
                {
                    Debug.LogWarning("ItemDescriptionContainer: wandSlotsPanelPrefab missing WandSlotsPanel component.");
                    return;
                }
                // Ensure the instantiated panel is parented correctly and active so child coroutines/layouts can run.
                wandPanelInstance.transform.SetParent(transform, false);
                wandPanelInstance.gameObject.SetActive(true);
            }
            else
            {
                wandPanelInstance = GetComponentInChildren<WandSlotsPanel>(true);
                if (wandPanelInstance == null)
                {
                    Debug.LogWarning("ItemDescriptionContainer: no wandSlotsPanelPrefab and no child WandSlotsPanel found.");
                    return;
                }
                wandPanelInstance.transform.SetParent(transform, false);
                wandPanelInstance.gameObject.SetActive(true);
            }
        }

        wandSourceSlot = sourceSlot;
        currentWand = wand;
        ownerUI = ui;
        wandPanelInstance.Show(wand, sourceSlot, ui);
    }

    public void HideWandSlots()
    {
        if (wandPanelInstance != null)
        {
            wandPanelInstance.Hide();
            wandSourceSlot = null;
            currentWand = null;
            ownerUI = null;
        }
    }

    // Refresh the currently shown wand slots (re-populate UI) if a wand is active in this container.
    public void RefreshWandSlots()
    {
        if (currentWand == null || wandPanelInstance == null) return;
        wandPanelInstance.Show(currentWand, wandSourceSlot, ownerUI);
        var rect = transform as RectTransform;
        if (rect != null)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    public bool IsWandPanelForSource(InventorySlotUI src)
    {
        return wandPanelInstance != null && src != null && wandSourceSlot == src;
    }
    private Color GetColorForTag(ItemLineData.LineTag tag)
    {
        switch (tag)
        {
            case ItemLineData.LineTag.Description: return colorDescription;
            case ItemLineData.LineTag.Intelligence: return colorIntelligence;
            case ItemLineData.LineTag.Strength: return colorStrength;
            case ItemLineData.LineTag.Stamina: return colorStamina;
            case ItemLineData.LineTag.Agility: return colorAgility;
            default: return Color.white;
        }
    }
}

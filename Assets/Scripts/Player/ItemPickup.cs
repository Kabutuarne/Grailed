using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IItemDisplayName
{
    [Header("Presentation")]
    // Canonical title used across UI. Prefer this over legacy `itemName`.
    public string title;
    public Color titleColor = Color.white;
    public GameObject itemPrefab;

    [Header("Item Info")]
    // Canonical description lines used by the tooltip UI.
    public List<ItemLineData> descriptionRows = new List<ItemLineData>();

    // DisplayName implements IItemDisplayName. Uses `title` when set,
    // otherwise falls back to GameObject name.
    public virtual string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title;

            return gameObject.name;
        }
    }

    // Tooltip title used by tooltip UI (kept for compatibility).
    public virtual string TooltipTitle => DisplayName;

    // Tooltip color for the title.
    public virtual Color TooltipTitleColor => titleColor;

    // Return the item lines (may be empty)
    public virtual IReadOnlyList<ItemLineData> GetItemLines()
    {
        return descriptionRows;
    }

    public virtual void OnPickedUp()
    {
        gameObject.SetActive(false);
    }
}
using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IItemDisplayName
{
    [Header("Presentation")]
    // Canonical title used across UI
    public string title;
    public Color titleColor = Color.white;
    public GameObject itemPrefab;

    [Header("Item Info")]
    // Canonical description lines used by the tooltip UI.
    public List<ItemLineData> descriptionRows = new List<ItemLineData>();

    [Header("Hold Settings")]
    [Tooltip("Local position offset to apply when this item is parented to a hand transform.")]
    public Vector3 holdLocalPosition = Vector3.zero;
    [Tooltip("Local Euler rotation (degrees) to apply when held.")]
    public Vector3 holdLocalEulerAngles = Vector3.zero;

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

    // Apply the configured hold transform when this item is parented to a hand.
    public virtual void ApplyHeldTransform(Transform hand)
    {
        if (hand == null)
            return;

        transform.SetParent(hand, false);
        transform.localPosition = holdLocalPosition;
        transform.localEulerAngles = holdLocalEulerAngles;

    }

    public virtual void OnPickedUp()
    {
        gameObject.SetActive(false);
    }
}
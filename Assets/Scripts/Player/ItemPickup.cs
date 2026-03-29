using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IItemDisplayName, IItemTooltipData
{
    public string itemName;
    public GameObject itemPrefab;

    [Header("Tooltip")]
    public string tooltipTitleOverride;
    public Color tooltipTitleColor = Color.white;
    public List<ItemTooltipRowData> tooltipRows = new List<ItemTooltipRowData>();

    public virtual string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(itemName))
                return itemName;

            return gameObject.name;
        }
    }

    public virtual string TooltipTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(tooltipTitleOverride))
                return tooltipTitleOverride;

            return DisplayName;
        }
    }

    public virtual Color TooltipTitleColor => tooltipTitleColor;

    public virtual IReadOnlyList<ItemTooltipRowData> GetTooltipRows()
    {
        return tooltipRows;
    }

    public virtual void OnPickedUp()
    {
        gameObject.SetActive(false);
    }
}
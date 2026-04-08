using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IItemDisplayName
{
    public string itemName;
    public GameObject itemPrefab;

    [Header("Item Info")]
    // Up to 5 lines, each tagged (Description, Agility, Stamina, Strength, Intelligence)
    public List<ItemLineData> itemLines = new List<ItemLineData>();

    public virtual string DisplayName
    {
        get
        {
            // if (!string.IsNullOrWhiteSpace(itemName))
            //     return itemName;

            return gameObject.name;
        }
    }

    public virtual string TooltipTitle
    {
        get
        {
            return DisplayName;
        }
    }

    public virtual Color TooltipTitleColor => Color.white;

    // Return the item lines (may be empty)
    public virtual IReadOnlyList<ItemLineData> GetItemLines()
    {
        return itemLines;
    }

    public virtual void OnPickedUp()
    {
        gameObject.SetActive(false);
    }
}
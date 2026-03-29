using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class ItemTooltipDataUtility
{
    public static string GetDisplayName(GameObject item)
    {
        if (item == null)
            return "Item";

        MonoBehaviour[] behaviours = item.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IItemDisplayName named &&
                !string.IsNullOrWhiteSpace(named.DisplayName))
            {
                return named.DisplayName;
            }
        }

        ItemPickup pickup = item.GetComponent<ItemPickup>();
        if (pickup != null && !string.IsNullOrWhiteSpace(pickup.itemName))
            return pickup.itemName;

        return item.name;
    }

    public static bool TryGetTooltipData(GameObject item, out string title, out Color titleColor, out List<ItemTooltipRowData> rows)
    {
        title = GetDisplayName(item);
        titleColor = Color.white;
        rows = new List<ItemTooltipRowData>();

        if (item == null)
            return false;

        MonoBehaviour[] behaviours = item.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IItemTooltipData tooltipData)
            {
                title = string.IsNullOrWhiteSpace(tooltipData.TooltipTitle) ? title : tooltipData.TooltipTitle;
                titleColor = tooltipData.TooltipTitleColor;

                IReadOnlyList<ItemTooltipRowData> srcRows = tooltipData.GetTooltipRows();
                if (srcRows != null)
                {
                    for (int r = 0; r < srcRows.Count; r++)
                    {
                        if (srcRows[r] != null)
                            rows.Add(new ItemTooltipRowData(srcRows[r].text, srcRows[r].color));
                    }
                }

                return true;
            }
        }

        return false;
    }

    public static Sprite GetInventoryIcon(GameObject item)
    {
        if (item == null)
            return null;

        MonoBehaviour[] behaviours = item.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInventoryIconProvider iconProvider)
            {
                Sprite provided = iconProvider.InventoryIcon;
                if (provided != null)
                    return provided;
            }
        }

        SpriteRenderer sr = item.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite;

        Image uiImg = item.GetComponentInChildren<Image>();
        if (uiImg != null && uiImg.sprite != null)
            return uiImg.sprite;

        return null;
    }
}
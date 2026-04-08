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
        if (pickup != null && !string.IsNullOrWhiteSpace(pickup.DisplayName))
            return pickup.DisplayName;

        return item.name;
    }

    // get item lines (tagged) if present. Falls back to item data on the GameObject.
    public static bool TryGetItemInfo(GameObject item, out string title, out Color titleColor, out List<ItemLineData> lines)
    {
        title = GetDisplayName(item);
        titleColor = Color.white;
        lines = new List<ItemLineData>();

        if (item == null)
            return false;

        // If the ItemPickup component exists, use its stored lines
        var pickup = item.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            title = string.IsNullOrWhiteSpace(pickup.TooltipTitle) ? title : pickup.TooltipTitle;
            titleColor = pickup.TooltipTitleColor;
            var src = pickup.GetItemLines();
            if (src != null)
            {
                for (int i = 0; i < src.Count && i < 5; i++)
                {
                    var s = src[i];
                    if (s != null)
                        lines.Add(new ItemLineData(s.text, s.tag));
                }
            }

            return true;
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
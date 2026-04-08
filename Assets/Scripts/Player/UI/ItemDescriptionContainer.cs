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

    // Sets title, description and lines using ItemPickup data. Expects up to 5 lines.
    public void Populate(ItemPickup pickup)
    {
        if (pickup == null) return;

        if (titleText != null)
        {
            titleText.text = pickup.TooltipTitle;
            titleText.color = new Color(0xF2 / 255f, 0xE6 / 255f, 0xC8 / 255f);
        }

        var allLines = pickup.GetItemLines();

        // Reset all UI elements
        if (descriptionText != null) descriptionText.text = string.Empty;
        foreach (var t in lineTexts) { if (t != null) t.text = string.Empty; }

        if (allLines == null) return;

        // Track which UI line slot we are currently filling
        int uiLineIndex = 0;

        foreach (var data in allLines)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.text)) continue;

            // Handle the special Description tag
            if (data.tag == ItemLineData.LineTag.Description)
            {
                if (descriptionText != null)
                {
                    descriptionText.text = data.text;
                    descriptionText.color = colorDescription;
                }
                continue;
            }

            // Handle all other tags in order
            if (uiLineIndex < lineTexts.Count)
            {
                var t = lineTexts[uiLineIndex];
                if (t != null)
                {
                    t.text = data.text;
                    t.color = GetColorForTag(data.tag);
                    uiLineIndex++; // Only move to the next UI slot if we actually filled one
                }
            }
        }
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

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Controller for a modular tooltip prefab with top/center/bottom images and title/description texts.
public class ItemTooltip : MonoBehaviour
{
    [Header("Background Parts")]
    public Image topImage;
    public Image centerImage; // will be stretched to fit text
    public Image bottomImage;

    [Header("Texts")]
    public Text titleText;
    public Text descriptionText; // legacy single description

    [Header("Multi-row Description")]
    public Transform rowsRoot; // optional: if set, will render per-line descriptions here
    public Text rowPrefab;     // optional: prefab Text used for rows

    [Header("Layout")]
    public float paddingVertical = 8f;
    public float paddingHorizontal = 8f;
    [Tooltip("Maximum tooltip width in pixels. Prevents horizontal stretching.")]
    public float maxWidth = 400f;
    [Tooltip("Minimum tooltip width in pixels.")]
    public float minWidth = 80f;

    RectTransform rectT;

    void Awake()
    {
        rectT = GetComponent<RectTransform>();
        if (rectT != null)
            rectT.pivot = new Vector2(0f, 1f); // top-left pivot so positioning is relative to cursor
    }

    // Set content and colors (single description)
    public void SetData(string title, Color titleColor, string description, Color descriptionColor)
    {
        if (titleText != null)
        {
            titleText.text = title ?? "";
            titleText.color = titleColor;
        }

        // If multi-row root is defined, clear it and render no rows for single description
        if (rowsRoot != null)
        {
            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(rowsRoot.GetChild(i).gameObject);
            }
        }

        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.text = description ?? "";
            descriptionText.color = descriptionColor;
        }

        LayoutRebuild();
    }

    // New: Set multiple colored description rows. If rowsRoot/rowPrefab are set, uses them.
    // Falls back to concatenated text if row rendering isn't configured.
    [System.Serializable]
    public class TooltipLine
    {
        public string text;
        public Color color = Color.white;
    }

    public void SetLines(string title, Color titleColor, IList<TooltipLine> lines)
    {
        if (titleText != null)
        {
            titleText.text = title ?? "";
            titleText.color = titleColor;
        }

        if (rowsRoot != null && rowPrefab != null && lines != null)
        {
            // Clear previous rows
            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
                Destroy(rowsRoot.GetChild(i).gameObject);

            // Hide legacy single description
            if (descriptionText != null)
                descriptionText.gameObject.SetActive(false);

            foreach (var line in lines)
            {
                if (line == null) continue;
                var row = Instantiate(rowPrefab, rowsRoot);
                row.text = line.text ?? "";
                row.color = line.color;
            }
        }
        else
        {
            // Fallback: show concatenated single description
            string combined = "";
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var l = lines[i];
                    if (l == null) continue;
                    if (i > 0) combined += "\n";
                    combined += l.text ?? "";
                }
            }
            SetData(title, titleColor, combined, Color.white);
            return;
        }

        LayoutRebuild();
    }

    void LayoutRebuild()
    {
        if (rectT == null) rectT = GetComponent<RectTransform>();
        // Only rebuild layout (update text wrapping) but do NOT change tooltip RectTransform size.
        Canvas.ForceUpdateCanvases();

        // Ensure text components wrap based on their current widths (do not modify their rect sizes here).
        // This keeps designer-defined tooltip width/height intact while updating text content.

        // Position texts inside center area if centerImage has a RectTransform
        // No complex slicing logic here; assume prefab arranged with layout components.
    }
}

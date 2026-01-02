using UnityEngine;
using UnityEngine.UI;

// Controller for a modular tooltip prefab with top/center/bottom images and title/description texts.
public class ItemTooltip : MonoBehaviour
{
    [Header("Background Parts")]
    public Image topImage;
    public Image centerImage; // will be stretched to fit text
    public Image bottomImage;

    [Header("Texts")]
    public Text titleText;
    public Text descriptionText;

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

    // Set content and colors
    public void SetData(string title, Color titleColor, string description, Color descriptionColor)
    {
        if (titleText != null)
        {
            titleText.text = title ?? "";
            titleText.color = titleColor;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description ?? "";
            descriptionText.color = descriptionColor;
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

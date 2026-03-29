using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Controller for a modular tooltip prefab with top/center/bottom images and dynamic colored rows.
public class ItemTooltip : MonoBehaviour
{
    [Header("Background Parts")]
    public Image topImage;
    public Image centerImage;
    public Image bottomImage;

    [Header("Title")]
    public Text titleText;

    [Header("Rows")]
    public Transform rowsRoot;
    public TooltipTextRow rowPrefab;

    [Header("Optional Legacy")]
    public Text descriptionText;

    [Header("Layout")]
    public float cursorOffsetX = 10f;
    public float cursorOffsetY = -10f;

    private RectTransform rectT;
    private readonly List<TooltipTextRow> spawnedRows = new List<TooltipTextRow>();

    void Awake()
    {
        rectT = GetComponent<RectTransform>();
        if (rectT != null)
            rectT.pivot = new Vector2(0f, 1f);
    }

    public void SetData(string title, Color titleColor, IList<ItemTooltipRowData> rows)
    {
        if (titleText != null)
        {
            titleText.text = title ?? string.Empty;
            titleText.color = titleColor;
        }

        ClearRows();

        if (descriptionText != null)
            descriptionText.gameObject.SetActive(false);

        if (rowsRoot != null && rowPrefab != null && rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ItemTooltipRowData rowData = rows[i];
                if (rowData == null)
                    continue;

                TooltipTextRow row = Instantiate(rowPrefab, rowsRoot);
                row.SetData(rowData);
                spawnedRows.Add(row);
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void SetScreenPosition(Canvas canvas, Vector2 screenPosition)
    {
        if (canvas == null || rectT == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out localPos);
        rectT.localPosition = localPos + new Vector2(cursorOffsetX, cursorOffsetY);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    private void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i].gameObject);
        }

        spawnedRows.Clear();

        if (rowsRoot != null)
        {
            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(rowsRoot.GetChild(i).gameObject);
            }
        }
    }
}
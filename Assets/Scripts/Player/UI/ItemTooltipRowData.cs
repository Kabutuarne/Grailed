using UnityEngine;

[System.Serializable]
public class ItemTooltipRowData
{
    [TextArea]
    public string text;
    public Color color = Color.white;

    public ItemTooltipRowData() { }

    public ItemTooltipRowData(string text, Color color)
    {
        this.text = text;
        this.color = color;
    }
}
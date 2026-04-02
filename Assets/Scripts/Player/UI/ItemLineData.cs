using System;
using UnityEngine;

[Serializable]
public class ItemLineData
{
    public enum LineTag
    {
        Description,
        Agility,
        Stamina,
        Strength,
        Intelligence
    }

    [TextArea]
    public string text;
    public LineTag tag = LineTag.Description;
    public Color color = Color.white;

    public ItemLineData() { }
    public ItemLineData(string t, LineTag tag, Color c)
    {
        text = t; this.tag = tag; color = c;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class TooltipTextRow : MonoBehaviour
{
    public Text textLabel;

    public void SetData(ItemTooltipRowData data)
    {
        if (textLabel == null)
            return;

        if (data == null)
        {
            textLabel.text = string.Empty;
            textLabel.color = Color.white;
            return;
        }

        textLabel.text = data.text ?? string.Empty;
        textLabel.color = data.color;
    }
}
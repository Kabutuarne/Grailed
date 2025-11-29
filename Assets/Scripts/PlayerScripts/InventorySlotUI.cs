using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    public Text label;

    public void SetItem(GameObject item)
    {
        if (label == null)
            return;

        label.text = item != null ? item.name : "";
    }
}

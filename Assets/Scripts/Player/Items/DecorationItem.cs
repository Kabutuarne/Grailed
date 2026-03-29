using UnityEngine;

// Currently do not use
public class DecorationItem : ItemPickup
{
    [Header("Presentation")]
    public GameObject renderModel;
    public Rigidbody rb;

    [Header("Inventory UI")]
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;
    [TextArea]
    public string description;
    public Color descriptionColor = Color.white;

    [Header("Behavior")]
    public bool placeable = true;

}

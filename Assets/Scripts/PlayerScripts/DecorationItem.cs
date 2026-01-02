using UnityEngine;

// Represents a decorative world object that can be carried, placed, and shown in inventory.
public class DecorationItem : ItemPickup
{
    [Header("Presentation")]
    public GameObject renderModel; // 3D model to show when held / preview
    public Rigidbody rb;           // optional rigidbody on the world object

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

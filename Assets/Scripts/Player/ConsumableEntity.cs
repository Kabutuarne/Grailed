using UnityEngine;

// Attach to pickup/consumable world object. Holds presentation info and an EffectCarrier reference.
public class ConsumableItem : ItemPickup
{
    [Header("Data")]
    public EffectCarrier carrier;
    [Tooltip("Base time in seconds to consume this item (before agility speed).")]
    public float baseConsumeTime = 1.0f;

    [Header("Presentation")]
    public GameObject renderModel;       // 3D model to show in the world

    [Header("Inventory UI")]
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;
    public string description;
    public Color descriptionColor = Color.white;

    [Header("Behavior")]
    public bool destroyOnConsume = true;

    // Consume by a player GameObject (applies carrier effects)
    public void Consume(GameObject user)
    {
        if (carrier == null)
        {
            Debug.LogWarning($"ConsumableItem on {gameObject.name} missing EffectCarrier");
            return;
        }

        carrier.Apply(user);

        Debug.Log($"[ConsumableItem] Consumed carrier '{carrier.title}' on {user.name}");

        if (destroyOnConsume)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

}

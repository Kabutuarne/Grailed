using UnityEngine;

// Attach to pickup/consumable world object. Holds presentation info and an EffectCarrier reference.
public class ConsumableItem : ItemPickup
{
    [Header("Data")]
    public EffectCarrier carrier;

    [Header("Presentation")]
    public GameObject renderModel;       // 3D model to show in the world
    public Rigidbody rb;                 // rigidbody for physics

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

        // Apply the carrier effects to the user. PlayerEffects will record the carrier reference for UI.
        foreach (var e in carrier.effects)
        {
            if (e == null) continue;
            e.Apply(user, carrier);
            Debug.Log($"[ConsumableEntity] Applied effect '{e.displayName}' from carrier '{carrier.title}' to {user.name}");
        }

        if (destroyOnConsume)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}

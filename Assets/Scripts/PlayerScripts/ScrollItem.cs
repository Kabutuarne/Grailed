using UnityEngine;

// Represents a scroll item that holds a SpellEffect and inventory metadata.
public class ScrollItem : ItemPickup
{
    [Header("Scroll Data")]
    public SpellEffect spellEffect;

    [Header("Presentation")]
    public GameObject renderModel;       // model to show when held
    public Rigidbody rb;                 // optional rigidbody for physics on world object
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;
    [TextArea]
    public string description;
    public Color descriptionColor = Color.white;

    [Header("Behavior")]
    public bool destroyOnCast = false; // do not destroy scroll on use by default
    public GameObject projectilePrefab; // optional specific projectile prefab for this scroll
    public float fireballAOERadius = 0f; // if >0, fireball will deal AOE damage on impact
    public GameObject groundFirePrefab; // optional prefab spawned at impact that persists on ground

    // Helper: returns true if this scroll can be cast
    public bool CanCast()
    {
        return spellEffect != null && spellEffect.castTime > 0f;
    }
}

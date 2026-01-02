using UnityEngine;

// Represents a scroll item that holds spell configuration and inventory metadata.
public class ScrollItem : ItemPickup
{
    [Header("Scroll Data")]
    public AOESpell aoeSpell;              // optional AOE spell
    public ProjectileSpell projectileSpell; // optional projectile spell

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
    public GameObject projectilePrefab; // optional specific projectile model for this scroll
    public float fireballAOERadius = 0f; // retained legacy property (if used)
    public GameObject groundFirePrefab; // optional prefab spawned at impact that persists on ground

    // Helper: returns true if this scroll can be cast
    public bool CanCast()
    {
        if (aoeSpell != null && aoeSpell.castTime > 0f) return true;
        if (projectileSpell != null && projectileSpell.castTime > 0f) return true;
        return false;
    }
}

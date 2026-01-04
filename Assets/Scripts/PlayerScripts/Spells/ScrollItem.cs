using UnityEngine;
using UnityEngine.InputSystem;

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

    public void Cast(GameObject user, Transform origin)
    {
        if (user == null) return;

        var fireOrigin = origin != null ? origin : user.transform;

        if (projectileSpell != null && projectilePrefab != null)
        {
            var proj = Object.Instantiate(projectilePrefab, fireOrigin.position + fireOrigin.forward * 0.3f, Quaternion.LookRotation(fireOrigin.forward, Vector3.up));
            var rbp = proj.GetComponent<Rigidbody>();
            if (rbp != null)
            {
                rbp.isKinematic = false;
                rbp.detectCollisions = true;
                rbp.linearVelocity = fireOrigin.forward * 12f;
            }
        }
        else if (aoeSpell != null)
        {
            Vector3 pos = fireOrigin.position + fireOrigin.forward * 2f;
            if (groundFirePrefab != null)
            {
                Object.Instantiate(groundFirePrefab, pos, Quaternion.identity);
            }
        }
    }
}

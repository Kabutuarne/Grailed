using UnityEngine;

public class DecorationItem : ItemPickup, IInventoryIconProvider, IInventoryPreviewProvider
{
    [Header("Decoration Settings")]
    [Tooltip("If true the item will be anchored (kinematic, no gravity) until triggered.")]
    public bool anchored = true;

    [Tooltip("If true the item will become dynamic when impacted by sufficient force.")]
    public bool becomesDynamicOnImpact = true;

    [Tooltip("Minimum impulse magnitude required to unanchor the item.")]
    public float minImpactToUnanchor = 1.0f;

    [Tooltip("If true, any collision with the player will unanchor immediately.")]
    public bool unanchorOnPlayerContact = true;

    [Tooltip("If true the item can be picked up only once (will be removed after pickup).")]
    public bool pickableOnce = true;

    private Rigidbody rb;
    private bool isAnchoredActive;
    private bool wasPickedUp;

    [Header("Presentation")]
    public GameObject renderModel;

    [Header("Inventory UI")]
    public Sprite inventoryIcon;
    public Sprite InventoryIcon => inventoryIcon;

    [Header("UI Preview Tweaks")]
    public Vector3 previewRotation = new Vector3(0, 180, 0);
    public float previewScale = 1.0f;
    public GameObject PreviewPrefab => renderModel;
    public Vector3 PreviewRotation => previewRotation;
    public float PreviewScale => previewScale;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.detectCollisions = true;
            if (anchored)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        isAnchoredActive = anchored;
        wasPickedUp = false;
    }

    public override void OnPickedUp()
    {
        BecomeDynamic(Vector3.zero);
        wasPickedUp = true;
        base.OnPickedUp();
    }

    // Called by the player's OnControllerColliderHit
    public void OnPlayerContact(Vector3 impulse)
    {
        if (wasPickedUp || !isAnchoredActive)
            return;

        if (unanchorOnPlayerContact)
            BecomeDynamic(impulse);
    }

    // Handles dynamic Rigidbody objects hitting this (thrown items, debris, etc.)
    void OnCollisionEnter(Collision collision)
    {
        if (wasPickedUp || !isAnchoredActive)
            return;

        // CharacterControllers never reach here — use OnPlayerContact() instead
        if (unanchorOnPlayerContact && collision.gameObject.CompareTag("Player"))
        {
            BecomeDynamic(collision.impulse);
            return;
        }

        if (becomesDynamicOnImpact && collision.impulse.magnitude >= minImpactToUnanchor)
            BecomeDynamic(collision.impulse);
    }

    // Handles trigger-based overlap (e.g. a separate trigger collider, pick-up radius, etc.)
    void OnTriggerEnter(Collider other)
    {
        if (wasPickedUp || !isAnchoredActive)
            return;

        if (unanchorOnPlayerContact && other.CompareTag("Player"))
            BecomeDynamic(Vector3.zero);
    }

    // Call from explosions, wind, abilities, etc.
    public void ApplyKnockback(Vector3 force)
    {
        if (isAnchoredActive)
        {
            BecomeDynamic(force);
        }
        else if (rb != null)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    private void BecomeDynamic(Vector3 impulse)
    {
        if (!isAnchoredActive)
            return;

        isAnchoredActive = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;

            if (impulse != Vector3.zero)
                rb.AddForce(impulse, ForceMode.Impulse);
        }

        transform.SetParent(null, true);
    }
}
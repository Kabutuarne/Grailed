using UnityEngine;

// Simple decoration/basic item that can be anchored (static/in-air) and become dynamic when impacted
public class DecorationItem : ItemPickup
{
    [Header("Decoration Settings")]
    [Tooltip("If true the item will be anchored (kinematic, no gravity) until impacted or picked up.")]
    public bool anchored = true;

    [Tooltip("Optional transform to parent to while anchored (for wall hangings).")]
    public Transform anchoredParent;

    [Tooltip("If true the item will become dynamic when impacted by sufficient force.")]
    public bool becomesDynamicOnImpact = true;

    [Tooltip("Minimum impulse magnitude required to unanchor the item.")]
    public float minImpactToUnanchor = 1.0f;

    [Tooltip("If true collisions with player will unanchor immediately.")]
    public bool unanchorOnPlayerContact = true;

    [Tooltip("If true the item can be picked up only once (will be removed after pickup).")]
    public bool pickableOnce = true;

    private Rigidbody rb;
    private bool isAnchoredActive;
    private bool wasPickedUp;

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

        if (anchoredParent != null)
            transform.SetParent(anchoredParent, true);

        isAnchoredActive = anchored;
        wasPickedUp = false;
    }

    public override void OnPickedUp()
    {
        wasPickedUp = true;
        base.OnPickedUp();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (wasPickedUp || !isAnchoredActive)
            return;

        if (unanchorOnPlayerContact)
        {
            var player = collision.collider.GetComponentInParent<PlayerInventory>();
            if (player != null)
            {
                // unanchor on player contact
                BecomeDynamic(collision.impulse);
                return;
            }
        }

        if (becomesDynamicOnImpact)
        {
            float impact = collision.impulse.magnitude;
            if (impact >= minImpactToUnanchor)
            {
                BecomeDynamic(collision.impulse);
            }
        }
    }

    // External API to apply a force/knockback and make the item dynamic
    public void ApplyKnockback(Vector3 force)
    {
        BecomeDynamic(force);
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
            rb.AddForce(impulse, ForceMode.Impulse);
        }

        // Un-parent so physics behaves naturally
        transform.SetParent(null, true);
    }
}

using UnityEngine;

/// <summary>
/// Placed on hand/fist bones. Always-on physical collider; arming is logical only.
/// Reports contacts to SkeletonCombat during an armed swing via collision callbacks.
/// Uses kinematic rigidbody with contact detection to ensure physics interactions.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class SkeletonLimbHitbox : MonoBehaviour
{
    private SkeletonCombat combat;
    private bool armed;

    public void Initialize(SkeletonCombat skeletonCombat)
    {
        combat = skeletonCombat;

        // Ensure collider is NOT a trigger so it generates physics contacts
        Collider collider = GetComponent<Collider>();
        collider.isTrigger = false;
        collider.providesContacts = true;

        // Ensure rigidbody is configured correctly for contact detection
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;      // Don't let physics move the bone
        rb.detectCollisions = true; // Must detect collisions for contacts to work
        rb.useGravity = false;      // Bones are animated, not gravity-driven
    }

    public void Arm() => armed = true;
    public void Disarm() => armed = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (!armed || combat == null) return;
        combat.OnLimbHit(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!armed || combat == null) return;
        combat.OnLimbHit(collision.collider);
    }
}
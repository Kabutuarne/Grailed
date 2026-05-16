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
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.useGravity = false;
    }

    public void Arm() => armed = true;
    public void Disarm() => armed = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (!armed || combat == null) return;

        // Prevent hitting own parent (the skeleton itself)
        if (collision.collider.transform.IsChildOf(transform.parent) ||
            collision.collider.transform.parent == transform.parent)
            return;

        combat.OnLimbHit(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!armed || combat == null) return;

        // Prevent hitting own parent (the skeleton itself)
        if (collision.collider.transform.IsChildOf(transform.parent) ||
            collision.collider.transform.parent == transform.parent)
            return;

        combat.OnLimbHit(collision.collider);
    }
}
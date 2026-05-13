using UnityEngine;

/// <summary>
/// Placed on hand/fist bones. Always-on trigger collider; arming is logical only.
/// Reports contacts to SkeletonCombat during an armed swing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SkeletonLimbHitbox : MonoBehaviour
{
    private SkeletonCombat combat;
    private bool armed;

    public void Initialize(SkeletonCombat skeletonCombat)
    {
        combat = skeletonCombat;
        GetComponent<Collider>().isTrigger = true;
    }

    public void Arm() => armed = true;
    public void Disarm() => armed = false;
    private void OnTriggerEnter(Collider other)
    {
        if (!armed || combat == null) return;
        combat.OnLimbHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!armed || combat == null) return;
        combat.OnLimbHit(other);
    }
}
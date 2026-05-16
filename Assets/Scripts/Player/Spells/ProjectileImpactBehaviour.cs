using UnityEngine;

// Base class for modular impact behaviours that can be attached to spell assets.
public abstract class SpellImpactBehaviour : ScriptableObject
{
    // Apply custom behaviour on impact.
    // caster: the player who cast the spell
    // hitPos: impact position in world space
    // hits: colliders affected (within spell impact radius or the hit collider)
    // impactRadius: current spell's impact radius for reference
    public abstract void Apply(GameObject caster, Vector3 hitPos, Collider[] hits, float impactRadius);
}

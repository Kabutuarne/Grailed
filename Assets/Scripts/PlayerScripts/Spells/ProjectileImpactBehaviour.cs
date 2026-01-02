using UnityEngine;

// Base class for modular impact behaviours that can be attached to ProjectileSpell assets.
public abstract class ProjectileImpactBehaviour : ScriptableObject
{
    // Apply custom behaviour on impact.
    // caster: the player who cast the spell
    // hitPos: impact position in world space
    // hits: colliders affected (within projectile impact radius or the hit collider)
    // projectileRadius: current projectile's impactRadius for reference
    public abstract void Apply(GameObject caster, Vector3 hitPos, Collider[] hits, float projectileRadius);
}

using UnityEngine;

/// <summary>
/// Base class for modular hit behaviours that can be attached to enemy combat assets.
/// Similar to ProjectileImpactBehaviour but for melee attacks.
/// </summary>
public abstract class EnemyHitBehaviour : ScriptableObject
{
    /// <summary>
    /// Apply custom behaviour on enemy melee hit.
    /// </summary>
    /// <param name="attacker">The enemy performing the attack (e.g., SkeletonAI, ButlerAI)</param>
    /// <param name="hitPos">Impact position in world space</param>
    /// <param name="hits">Colliders affected by the attack</param>
    /// <param name="attackRadius">Current attack's effective radius for reference</param>
    public abstract void Apply(GameObject attacker, Vector3 hitPos, Collider[] hits, float attackRadius);
}

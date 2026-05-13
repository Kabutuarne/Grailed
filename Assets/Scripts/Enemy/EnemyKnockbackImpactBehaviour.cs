using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/EnemyBehaviours/EnemyKnockback", fileName = "NewEnemyKnockbackBehaviour")]
public class EnemyKnockbackImpactBehaviour : EnemyHitBehaviour
{
    [Header("Knockback Settings")]
    public float force = 5f;
    public float upwardsModifier = 0f;
    public ForceMode forceMode = ForceMode.Impulse;

    [Header("Targeting")]
    public bool useAttackRadius = true;
    public float radius = 2f;
    public LayerMask layerMask = ~0;

    [Header("Player Knockback")]
    public float playerKnockbackMultiplier = 1f;

    public override void Apply(GameObject attacker, Vector3 hitPos, Collider[] hits, float attackRadius)
    {
        float effectiveRadius = useAttackRadius ? Mathf.Max(0.5f, attackRadius) : radius;

        var processed = new System.Collections.Generic.HashSet<GameObject>();

        foreach (Collider c in hits)
        {
            if (c == null) continue;
            if (!processed.Add(c.gameObject)) continue;
            if ((layerMask.value & (1 << c.gameObject.layer)) == 0) continue;

            PlayerController player = c.gameObject.GetComponent<PlayerController>()
                                   ?? c.gameObject.GetComponentInParent<PlayerController>()
                                   ?? c.gameObject.GetComponentInChildren<PlayerController>();

            if (player != null)
                ApplyPlayerKnockback(player, hitPos, force, effectiveRadius);
        }
    }

    private void ApplyPlayerKnockback(PlayerController player, Vector3 hitPos, float baseForce, float explosionRadius)
    {
        if (player == null) return;

        float distance = Vector3.Distance(player.transform.position, hitPos);
        float attenuation = 1f - Mathf.Clamp01(distance / explosionRadius);
        float effective = baseForce * attenuation * playerKnockbackMultiplier;

        if (effective < 0.1f) return;

        player.ApplyExplosionKnockback(hitPos, effective, explosionRadius, upwardsModifier);
    }
}
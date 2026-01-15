using UnityEngine;

// Reusable attack: explodes once, damaging and knocking back nearby targets, then kills the owner.
public class SelfDestructAttack : EnemyAttack
{
    [Header("Explosion")]
    public float radius = 3f;
    public float force = 10f;
    public float upwardsModifier = 0.2f;
    public ForceMode forceMode = ForceMode.Impulse;
    public LayerMask damageLayers = ~0; // default: affect all
    public GameObject explosionVfx;

    public void TriggerExplosion()
    {
        // Damage and knockback all valid targets
        Vector3 center = transform.position;
        var hits = Physics.OverlapSphere(center, radius);

        foreach (var c in hits)
        {
            if (c == null) continue;
            var go = c.gameObject;
            if ((damageLayers.value & (1 << go.layer)) == 0)
                continue;

            // Damage player if present
            var ps = go.GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.TakeDamage(damagePerAttack);
                // Player uses CharacterController; apply explosion knockback via its helper if available
                var pc = go.GetComponent<PlayerController>();
                if (pc != null)
                {
                    try { pc.ApplyExplosionKnockback(center, force, radius, upwardsModifier); } catch { }
                }
            }

            // Knockback physics bodies
            var rb = c.attachedRigidbody != null ? c.attachedRigidbody : go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                try { rb.AddExplosionForce(force, center, radius, upwardsModifier, forceMode); } catch { }
            }
        }

        // VFX
        if (explosionVfx != null)
        {
            try { Instantiate(explosionVfx, center, Quaternion.identity); } catch { }
        }

        // Kill this enemy through its stats to ensure drops/death flow
        var es = GetComponent<EnemyStats>();
        if (es != null)
        {
            es.TakeDamage(Mathf.Max(1f, es.health));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected override void PerformAttack(GameObject target)
    {
        // For self-destruct, when asked to attack a target, just explode if in range.
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = target.transform.position; b.y = 0f;
        if (Vector3.Distance(a, b) <= attackRange)
        {
            TriggerExplosion();
        }
    }
}

using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/SpellBehaviours/Knockback", fileName = "NewKnockbackBehaviour")]
public class KnockbackImpactBehaviour : ProjectileImpactBehaviour
{
    [Header("Knockback Settings")]
    public float force = 10f;
    public float upwardsModifier = 0f;
    public ForceMode forceMode = ForceMode.Impulse;

    [Header("Targeting")]
    public bool useProjectileRadius = true;
    public float radius = 3f;
    public LayerMask layerMask = ~0; // affect all by default
    public bool affectKinematicBodies = false;

    public override void Apply(GameObject caster, Vector3 hitPos, Collider[] hits, float projectileRadius)
    {
        float r = useProjectileRadius ? Mathf.Max(0.01f, projectileRadius) : radius;
        foreach (var c in hits)
        {
            if (c == null) continue;
            var go = c.gameObject;
            if (caster != null && go == caster) continue;

            // Layer filter
            if ((layerMask.value & (1 << go.layer)) == 0)
                continue;

            // Prefer attachedRigidbody on collider
            Rigidbody rb = c.attachedRigidbody;
            if (rb == null)
                rb = go.GetComponent<Rigidbody>();

            if (rb == null) continue;
            if (!affectKinematicBodies && rb.isKinematic) continue;

            try
            {
                rb.AddExplosionForce(force, hitPos, r, upwardsModifier, forceMode);
            }
            catch { }
        }
    }
}

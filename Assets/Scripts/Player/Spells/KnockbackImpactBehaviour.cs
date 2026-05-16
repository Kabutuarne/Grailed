// KnockbackImpactBehaviour.cs
using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "DungeonBroker/SpellBehaviours/Knockback", fileName = "NewKnockbackBehaviour")]
public class KnockbackImpactBehaviour : SpellImpactBehaviour
{
    [Header("Knockback Settings")]
    [Tooltip("Base force strength applied to targets")]
    public float force = 10f;
    [Tooltip("Upward force modifier (adds vertical lift)")]
    public float upwardsModifier = 0f;
    [Tooltip("How the force is applied: Impulse = instant, Force = continuous")]
    public ForceMode forceMode = ForceMode.Impulse;

    [Header("Targeting")]
    [Tooltip("Use the projectile's impact radius instead of this radius")]
    public bool useProjectileRadius = true;
    [Tooltip("Override radius when not using projectile radius")]
    public float radius = 3f;
    [Tooltip("Which layers can receive knockback")]
    public LayerMask layerMask = ~0;

    [Header("Stagger (Butler AI)")]
    [Tooltip("Force multiplier when applying stagger to Butler AI")]
    public float butlerStaggerMultiplier = 1.5f;
    [Tooltip("Minimum force to trigger stagger on Butler")]
    public float minButlerStaggerForce = 1f;

    [Header("Player Knockback")]
    [Tooltip("Force multiplier for player knockback")]
    public float playerKnockbackMultiplier = 1f;

    [Header("Ragdoll Knockback")]
    [Tooltip("Whether to affect kinematic rigidbodies")]
    public bool affectKinematicBodies = false;
    [Tooltip("When true, also pushes already-ragdolled enemies")]
    public bool affectDeadRagdolls = true;

    // Helper coroutine runner
    private static MonoBehaviour coroutineRunner;

    public override void Apply(GameObject caster, Vector3 hitPos, Collider[] hits, float projectileRadius)
    {
        float effectiveRadius = useProjectileRadius ? Mathf.Max(0.01f, projectileRadius) : radius;

        var processedObjects = new System.Collections.Generic.HashSet<GameObject>();

        foreach (Collider c in hits)
        {
            if (c == null) continue;
            GameObject target = c.gameObject;

            if (!processedObjects.Add(target)) continue;

            if ((layerMask.value & (1 << target.layer)) == 0) continue;

            ButlerKnockbackReceiver butlerKnockback = target.GetComponentInParent<ButlerKnockbackReceiver>();
            if (butlerKnockback != null)
            {
                ApplyButlerKnockback(butlerKnockback, hitPos, force, effectiveRadius);
                continue;
            }

            EnemyKnockbackReceiver enemyKnockback = target.GetComponentInParent<EnemyKnockbackReceiver>();
            if (enemyKnockback != null)
            {
                ApplyEnemyKnockback(enemyKnockback, hitPos, force, effectiveRadius);
                continue;
            }

            PlayerController playerController = target.GetComponentInParent<PlayerController>();
            if (playerController != null)
            {
                ApplyPlayerKnockback(playerController, hitPos, force, effectiveRadius);
                continue;
            }

            Rigidbody rb = c.attachedRigidbody;
            if (rb == null)
                rb = target.GetComponent<Rigidbody>();

            if (rb != null)
            {
                if (!affectKinematicBodies && rb.isKinematic)
                {
                    if (affectDeadRagdolls && IsRagdollBone(rb))
                    {
                        rb.isKinematic = false;
                        rb.AddExplosionForce(force, hitPos, effectiveRadius, upwardsModifier, forceMode);

                        if (forceMode == ForceMode.Impulse && coroutineRunner != null)
                            coroutineRunner.StartCoroutine(ReenableKinematicAfterDelay(rb, 0.1f));
                    }
                    continue;
                }

                rb.AddExplosionForce(force, hitPos, effectiveRadius, upwardsModifier, forceMode);
            }
        }
    }

    /// <summary>
    /// Sets the coroutine runner for this behaviour. Call this from a MonoBehaviour.
    /// </summary>
    public static void SetCoroutineRunner(MonoBehaviour runner)
    {
        coroutineRunner = runner;
    }

    private void ApplyPlayerKnockback(PlayerController player, Vector3 hitPos, float baseForce, float explosionRadius)
    {
        if (player == null) return;

        float distance = Vector3.Distance(player.transform.position, hitPos);
        float attenuation = 1f - Mathf.Clamp01(distance / explosionRadius);
        float effectiveForce = baseForce * attenuation * playerKnockbackMultiplier;

        if (effectiveForce < 0.1f) return;

        Vector3 direction = (player.transform.position - hitPos).normalized;
        direction.y += upwardsModifier * 0.1f;
        direction.Normalize();

        player.ApplyExplosionKnockback(hitPos, effectiveForce, explosionRadius, upwardsModifier);
    }

    private void ApplyButlerKnockback(ButlerKnockbackReceiver receiver, Vector3 hitPos, float baseForce, float explosionRadius)
    {
        if (receiver == null) return;

        float distance = Vector3.Distance(receiver.transform.position, hitPos);
        float attenuation = 1f - Mathf.Clamp01(distance / explosionRadius);
        float attenuationSqr = attenuation * attenuation;

        float effectiveForce = baseForce * attenuationSqr * butlerStaggerMultiplier;

        if (effectiveForce < minButlerStaggerForce) return;

        Vector3 direction = (receiver.transform.position - hitPos).normalized;
        direction.y += upwardsModifier * 0.2f;
        direction.Normalize();

        receiver.ReceiveKnockback(hitPos, direction * effectiveForce, forceMode);
    }

    private void ApplyEnemyKnockback(EnemyKnockbackReceiver receiver, Vector3 hitPos, float baseForce, float explosionRadius)
    {
        if (receiver == null) return;

        float distance = Vector3.Distance(receiver.transform.position, hitPos);
        float attenuation = 1f - Mathf.Clamp01(distance / explosionRadius);
        float effectiveForce = baseForce * attenuation;

        Vector3 direction = (receiver.transform.position - hitPos).normalized;
        direction.y += upwardsModifier * 0.1f;
        direction.Normalize();

        receiver.ReceiveKnockback(direction * effectiveForce, forceMode);
    }

    private bool IsRagdollBone(Rigidbody rb)
    {
        if (rb == null) return false;

        CharacterJoint joint = rb.GetComponent<CharacterJoint>();
        if (joint != null) return true;

        ButlerAI butler = rb.GetComponentInParent<ButlerAI>();
        if (butler != null && butler.isDead) return true;

        return false;
    }

    private IEnumerator ReenableKinematicAfterDelay(Rigidbody rb, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (rb != null && !rb.isKinematic)
        {
            if (rb.linearVelocity.magnitude < 0.5f)
                rb.isKinematic = true;
            else
            {
                yield return new WaitForSeconds(0.2f);
                if (rb != null && rb.linearVelocity.magnitude < 0.5f)
                    rb.isKinematic = true;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(Vector3.zero, radius);
    }
}
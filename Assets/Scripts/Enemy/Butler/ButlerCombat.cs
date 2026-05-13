using UnityEngine;

/// <summary>
/// Handles attack timing, VFX, and effect application.
/// Uses the unified IResourceHandler system via EffectCarrier.Apply().
/// </summary>
[DisallowMultipleComponent]
public class ButlerCombat : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("Distance to start attacking")]
    public float attackRange = 2f;
    [Tooltip("Base time between attacks before agility scaling")]
    public float baseAttackInterval = 1.5f;
    [Tooltip("Effects applied on successful attack")]
    public EffectCarrier[] attackEffects;
    public EnemyHitBehaviour[] hitBehaviours;
    public GameObject attackVFX;
    public Vector3 attackVFXOffset = new Vector3(0f, 1f, 0.5f);
    [Tooltip("Radius for hit detection and behaviour application")]
    public float hitRadius = 1.5f;

    private ButlerAI ai;
    private float attackTimer;
    private int attackCount;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        attackTimer = 0f;
    }

    public void TickCooldown()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Attempt to attack the target. Called from ButlerAI when in range.
    /// </summary>
    public void TryAttack(Transform target)
    {
        if (attackTimer > 0f || target == null) return;

        attackTimer = ai.AttackInterval;
        PerformAttack(target);
    }

    private void PerformAttack(Transform target)
    {
        bool mirror = (attackCount % 2) == 1;
        attackCount++;

        // Trigger animation
        ai.animationController.TriggerAttack(mirror);

        // Play attack audio
        ai.audioController.PlayAttackSound();

        // Spawn VFX
        if (attackVFX != null)
        {
            Vector3 spawnPos = transform.position + transform.TransformDirection(attackVFXOffset);
            Instantiate(attackVFX, spawnPos, transform.rotation);
        }

        Vector3 hitPos = transform.position + transform.TransformDirection(attackVFXOffset);

        // Apply EffectCarrier effects
        if (attackEffects != null)
        {
            for (int i = 0; i < attackEffects.Length; i++)
            {
                EffectCarrier carrier = attackEffects[i];
                if (carrier != null)
                    carrier.Apply(target.gameObject);
            }
        }

        // Apply hit behaviours
        if (hitBehaviours != null && hitBehaviours.Length > 0)
        {
            Collider[] hits = Physics.OverlapSphere(hitPos, hitRadius);

            foreach (EnemyHitBehaviour behaviour in hitBehaviours)
            {
                if (behaviour != null)
                    behaviour.Apply(gameObject, hitPos, hits, hitRadius);
            }
        }
    }
}
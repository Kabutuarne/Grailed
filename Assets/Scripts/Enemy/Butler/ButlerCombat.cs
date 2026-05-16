using UnityEngine;
using System.Collections.Generic;

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
    [Tooltip("Radius for hit detection and behaviour application (only used if no hitboxes)")]
    public float hitRadius = 1.5f;

    private ButlerAI ai;
    private float attackTimer;
    private int attackCount;
    private bool isAttacking;
    private readonly HashSet<GameObject> hitThisSwing = new HashSet<GameObject>();

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
    public void TryAttack()
    {
        if (attackTimer > 0f || isAttacking) return;

        attackTimer = ai.AttackInterval;
        PerformAttack();
    }

    private void PerformAttack()
    {
        bool mirror = (attackCount % 2) == 1;
        attackCount++;

        isAttacking = true;
        hitThisSwing.Clear();

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

        // Arm all limb hitboxes from ButlerAI
        foreach (ButlerLimbHitbox hitbox in ai.limbHitboxes)
            hitbox?.Arm();
    }

    /// <summary>
    /// Called by ButlerLimbHitbox on trigger contact during an armed swing.
    /// </summary>
    public void OnLimbHit(Collider other)
    {
        if (!isAttacking) return;
        if (!hitThisSwing.Add(other.gameObject)) return;

        // Apply EffectCarrier effects
        if (attackEffects != null)
        {
            foreach (EffectCarrier carrier in attackEffects)
                carrier?.Apply(other.gameObject);
        }

        // Apply hit behaviours
        if (hitBehaviours != null && hitBehaviours.Length > 0)
        {
            Vector3 hitPos = other.ClosestPoint(transform.position);
            Collider[] hits = { other };

            foreach (EnemyHitBehaviour behaviour in hitBehaviours)
                behaviour?.Apply(gameObject, hitPos, hits, hitRadius);
        }
    }

    /// <summary>
    /// Called by ButlerAnimationController when the Attack-tagged state exits.
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        if (!isAttacking) return;

        isAttacking = false;
        hitThisSwing.Clear();

        // Disarm all limb hitboxes from ButlerAI
        foreach (ButlerLimbHitbox hitbox in ai.limbHitboxes)
            hitbox?.Disarm();
    }
}
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class SkeletonCombat : MonoBehaviour
{
    [Header("Combat")]
    public float attackRange = 2.5f;
    public float baseAttackInterval = 0.5f;

    [Header("Effects")]
    public EffectCarrier[] attackEffects;
    public EnemyHitBehaviour[] hitBehaviours;
    [Tooltip("Radius passed to hit behaviours (e.g. knockback falloff reference)")]
    public float hitBehaviourRadius = 2f;

    [Header("VFX")]
    public GameObject attackVFX;
    public Vector3 attackVFXOffset = new Vector3(0f, 1f, 0.5f);

    [Header("Hitboxes")]
    [Tooltip("SkeletonLimbHitbox components on the hand/fist bones.")]
    public SkeletonLimbHitbox[] limbHitboxes;

    private SkeletonAI ai;
    private float attackTimer;
    private bool isAttacking;
    private readonly HashSet<GameObject> hitThisSwing = new HashSet<GameObject>();

    public void Initialize(SkeletonAI skeletonAI)
    {
        ai = skeletonAI;
        attackTimer = 0f;

        foreach (SkeletonLimbHitbox hitbox in limbHitboxes)
            hitbox?.Initialize(this);
    }

    public void TickAttack(Transform target)
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist <= attackRange)
        {
            ai.animationController.upperBodyWeight = 1f;

            if (!isAttacking)
            {
                if (attackTimer > 0f)
                    attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f)
                    PerformAttack();
            }
        }
        else
        {
            ai.animationController.upperBodyWeight = 0f;
        }
    }

    private void PerformAttack()
    {
        if (ai == null) return;

        isAttacking = true;
        hitThisSwing.Clear();

        ai.animationController.TriggerAttack();

        if (attackVFX != null)
        {
            Vector3 spawnPos = transform.position + transform.TransformDirection(attackVFXOffset);
            Instantiate(attackVFX, spawnPos, transform.rotation);
        }

        foreach (SkeletonLimbHitbox hitbox in limbHitboxes)
            hitbox?.Arm();
    }

    /// <summary>Called by SkeletonLimbHitbox on trigger contact during an armed swing.</summary>
    public void OnLimbHit(Collider other)
    {
        if (!isAttacking) return;
        if (!hitThisSwing.Add(other.gameObject)) return;

        // Only apply effects to players
        PlayerController playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null) return;

        foreach (EffectCarrier carrier in attackEffects)
            carrier?.Apply(other.gameObject);

        // if (hitBehaviours != null && hitBehaviours.Length > 0)
        {
            Vector3 hitPos = other.ClosestPoint(transform.position);
            Collider[] hits = { other };
            Debug.Log("hello? ");
            foreach (EnemyHitBehaviour behaviour in hitBehaviours)
            {
                behaviour?.Apply(gameObject, hitPos, hits, hitBehaviourRadius);
                Debug.Log("SKELETON HIT THIS STUPID FUCKING PLAYER WITH " + behaviour);
            }
        }
    }

    /// <summary>Called by SkeletonAnimationController when the Attack-tagged state exits.</summary>
    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        hitThisSwing.Clear();

        foreach (SkeletonLimbHitbox hitbox in limbHitboxes)
            hitbox?.Disarm();

        attackTimer = ai != null ? ai.AttackInterval : baseAttackInterval;
    }
}
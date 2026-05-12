using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonCombat : MonoBehaviour
{
    [Header("Combat")]
    public float attackRange = 2.5f;
    public float baseAttackInterval = 0.5f;
    public EffectCarrier[] attackEffects;
    public GameObject attackVFX;
    public Vector3 attackVFXOffset = new Vector3(0f, 1f, 0.5f);

    private SkeletonAI ai;
    private float attackTimer;

    public void Initialize(SkeletonAI skeletonAI)
    {
        ai = skeletonAI;
        attackTimer = 0f;
    }

    public void TickAttack(Transform target)
    {
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        bool inRange = dist <= attackRange;

        ai.animationController.SetAttacking(inRange);

        if (inRange)
        {
            ai.animationController.upperBodyWeight = 1f;
            if (attackTimer > 0f)
                attackTimer -= Time.deltaTime;

            if (attackTimer <= 0f)
                PerformAttack(target);
        }
        else
        {
            ai.animationController.upperBodyWeight = 0f;
            attackTimer = 0f;   // reset so first hit after closing distance is instant
        }
    }

    private void PerformAttack(Transform target)
    {
        attackTimer = baseAttackInterval;
        ai.animationController.TriggerAttack();

        if (attackVFX != null)
        {
            Vector3 spawnPos = transform.position + transform.TransformDirection(attackVFXOffset);
            Instantiate(attackVFX, spawnPos, transform.rotation);
        }

        if (attackEffects != null)
        {
            foreach (EffectCarrier carrier in attackEffects)
            {
                if (carrier != null)
                    carrier.Apply(target.gameObject);
            }
        }
    }
}
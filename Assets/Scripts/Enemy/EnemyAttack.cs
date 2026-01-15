using UnityEngine;

public abstract class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float damagePerAttack = 10f;
    public float attackRate = 1f; // attacks per second
    public float attackRange = 1.8f;

    float nextAttackTime = 0f;

    protected bool CanAttack()
    {
        return Time.time >= nextAttackTime;
    }

    protected void MarkAttacked()
    {
        nextAttackTime = Time.time + (attackRate > 0f ? 1f / attackRate : 0f);
    }

    public void TryAttack(GameObject target)
    {
        if (target == null) return;
        if (!CanAttack()) return;

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = target.transform.position; b.y = 0f;
        if (Vector3.Distance(a, b) <= attackRange)
        {
            PerformAttack(target);
            MarkAttacked();
        }
    }

    protected abstract void PerformAttack(GameObject target);
}

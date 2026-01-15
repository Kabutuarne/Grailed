using UnityEngine;

public class MeleeAttack : EnemyAttack
{
    [Header("Melee")]
    public float knockbackForce = 5f;
    public float knockbackUpwards = 0.2f;

    protected override void PerformAttack(GameObject target)
    {
        // Face target
        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // Apply damage
        var pStats = target.GetComponent<PlayerStats>();
        if (pStats != null)
        {
            pStats.TakeDamage(damagePerAttack);
        }

        // Apply knockback if target has rigidbody; otherwise let PlayerController handle via existing explosion-based method
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 kbDir = dir.normalized;
            rb.AddForce(kbDir * knockbackForce + Vector3.up * knockbackUpwards, ForceMode.Impulse);
        }
        else
        {
            // For player using CharacterController, reuse its custom method to simulate knockback
            var pc = target.GetComponent<PlayerController>();
            if (pc != null)
            {
                Vector3 origin = transform.position;
                pc.ApplyExplosionKnockback(origin, knockbackForce, 2.0f, knockbackUpwards);
            }
        }
    }
}

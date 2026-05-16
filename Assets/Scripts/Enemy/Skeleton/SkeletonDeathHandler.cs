using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonDeathHandler : MonoBehaviour
{
    [Header("Death")]
    [Tooltip("How long after death before the skeleton is destroyed")]
    public float destroyDelay = 5f;

    private SkeletonAI ai;

    public void Initialize(SkeletonAI skeletonAI) => ai = skeletonAI;

    public void Die()
    {
        if (ai == null) return;

        if (ai.animator != null)
            ai.animator.enabled = false;

        if (ai.ragdollController != null)
        {
            ai.ragdollController.ActivateRagdoll();
        }
        else
        {
            Collider mainCol = GetComponent<Collider>();
            if (mainCol) mainCol.enabled = false;

            if (ai.rb != null)
            {
                ai.rb.isKinematic = true;
                ai.rb.detectCollisions = false;
                ai.rb.useGravity = false;
            }
        }

        if (ai.stats != null)
            ai.stats.SpawnDeathDrops();

        Destroy(gameObject, destroyDelay);
    }
}
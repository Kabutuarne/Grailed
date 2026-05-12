using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonKnockbackReceiver : MonoBehaviour
{
    [Header("Knockback")]
    public float knockbackForceMultiplier = 1f;

    // Minimal stagger used only for speed reduction (no visual rig)
    [Header("Movement Slow on hit")]
    public float staggerDuration = 0.3f;
    [Range(0f, 1f)] public float staggerSpeedMultiplier = 0.4f;
    public float minForceToStagger = 1f;

    private SkeletonAI ai;
    private Rigidbody rb;
    private bool isStaggered;
    private float staggerTimer;

    public bool IsStaggered => isStaggered;

    public void Initialize(SkeletonAI skeletonAI)
    {
        ai = skeletonAI;
        rb = ai.rb;
    }

    private void Update()
    {
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0f)
                EndStagger();
        }
    }

    public void ReceiveKnockback(Vector3 impactPoint, Vector3 force, ForceMode forceMode = ForceMode.Impulse)
    {
        if (ai == null || ai.isDead) return;

        if (rb != null && !rb.isKinematic)
            rb.AddForce(force, forceMode);

        if (force.magnitude >= minForceToStagger)
            StartStagger(force.magnitude);
    }

    private void StartStagger(float magnitude)
    {
        float dur = staggerDuration * Mathf.Clamp(magnitude / 10f, 0.5f, 2f);
        staggerTimer = Mathf.Max(staggerTimer, dur);
        isStaggered = true;
    }

    private void EndStagger()
    {
        isStaggered = false;
        staggerTimer = 0f;
    }

    public float GetStaggerSpeedMultiplier() => isStaggered ? staggerSpeedMultiplier : 1f;
}
using UnityEngine;

/// <summary>
/// Simple knockback receiver for enemies that don't have the Butler's BonePhysicsLayer system.
/// Directly applies force to the Rigidbody with optional stagger slowdown.
/// </summary>
[DisallowMultipleComponent]
public class EnemyKnockbackReceiver : MonoBehaviour
{
    [Header("Knockback Settings")]
    [Tooltip("Multiplier applied to incoming knockback force")]
    public float knockbackMultiplier = 1f;

    [Tooltip("Minimum force to trigger any reaction")]
    public float minForceToReact = 1f;

    [Header("Stagger")]
    [Tooltip("Duration of stagger slowdown after knockback")]
    public float staggerDuration = 0.3f;
    [Tooltip("Speed multiplier during stagger")]
    [Range(0f, 1f)] public float staggerSpeedMultiplier = 0.5f;

    private Rigidbody rb;
    private bool isStaggered;
    private float staggerTimer;

    public bool IsStaggered => isStaggered;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0f)
            {
                isStaggered = false;
            }
        }
    }

    /// <summary>
    /// Receive a knockback force.
    /// </summary>
    public void ReceiveKnockback(Vector3 force, ForceMode forceMode = ForceMode.Impulse)
    {
        if (force.magnitude < minForceToReact) return;

        Vector3 scaledForce = force * knockbackMultiplier;

        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(scaledForce, forceMode);
        }

        // Trigger stagger
        StartStagger(force.magnitude);
    }

    private void StartStagger(float forceMagnitude)
    {
        float duration = staggerDuration * Mathf.Clamp(forceMagnitude / 10f, 0.5f, 2f);
        staggerTimer = Mathf.Max(staggerTimer, duration);
        isStaggered = true;
    }

    /// <summary>
    /// Returns current movement speed multiplier (accounts for stagger).
    /// </summary>
    public float GetStaggerSpeedMultiplier()
    {
        return isStaggered ? staggerSpeedMultiplier : 1f;
    }
}
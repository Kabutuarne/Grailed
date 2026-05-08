// ButlerKnockbackReceiver.cs
using UnityEngine;

[DisallowMultipleComponent]
public class ButlerKnockbackReceiver : MonoBehaviour
{
    [Header("Knockback Settings")]
    public float knockbackForceMultiplier = 1f;

    [Header("Stagger")]
    public float staggerDuration = 0.5f;
    [Range(0f, 1f)] public float staggerSpeedMultiplier = 0.4f;
    public float minForceToStagger = 1f;

    [Header("Stagger Rig")]
    public ButlerStaggerRig staggerRig;

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    private ButlerAI ai;
    private ButlerMovement movement;
    private Rigidbody rb;
    private bool isStaggered;
    private float staggerTimer;

    // Cache for colliders to ensure they stay enabled
    private Collider[] allColliders;
    public bool IsStaggered => isStaggered;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        movement = ai.movement;
        rb = ai.rb;

        if (staggerRig == null)
            staggerRig = GetComponentInChildren<ButlerStaggerRig>();

        if (staggerRig != null)
        {
            // Pass animation controller reference
            if (staggerRig.animationController == null)
                staggerRig.animationController = ai.animationController;

            AutoAssignBones();
            staggerRig.InitializeSources();

            Debug.Log($"[ButlerKnockbackReceiver] Stagger rig initialized on {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[ButlerKnockbackReceiver] No ButlerStaggerRig found on {gameObject.name}");
        }

        // Cache all colliders to ensure they stay enabled
        allColliders = GetComponentsInChildren<Collider>(true);
    }

    private void AutoAssignBones()
    {
        if (ai.animator == null || staggerRig == null) return;

        // Auto-assign bones from the Animator's avatar
        if (staggerRig.headBone == null)
            staggerRig.headBone = ai.animator.GetBoneTransform(HumanBodyBones.Head);
        if (staggerRig.spineBone == null)
            staggerRig.spineBone = ai.animator.GetBoneTransform(HumanBodyBones.Spine);
        if (staggerRig.leftArmBone == null)
            staggerRig.leftArmBone = ai.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (staggerRig.rightArmBone == null)
            staggerRig.rightArmBone = ai.animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        // Sources should be children of this rig, find them by name
        if (staggerRig.headSource == null)
            staggerRig.headSource = FindSourceInRig("HeadStaggerSource");
        if (staggerRig.spineSource == null)
            staggerRig.spineSource = FindSourceInRig("SpineStaggerSource");
        if (staggerRig.leftArmSource == null)
            staggerRig.leftArmSource = FindSourceInRig("LeftArmStaggerSource");
        if (staggerRig.rightArmSource == null)
            staggerRig.rightArmSource = FindSourceInRig("RightArmStaggerSource");
    }

    private Transform FindSourceInRig(string name)
    {
        if (staggerRig == null) return null;

        Transform[] children = staggerRig.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private void Update()
    {
        // Ensure all colliders stay enabled during gameplay
        EnsureCollidersEnabled();

        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0f)
                EndStagger();
        }
    }

    /// <summary>
    /// Makes sure no colliders are disabled by the stagger system
    /// </summary>
    private void EnsureCollidersEnabled()
    {
        if (allColliders == null) return;

        foreach (Collider col in allColliders)
        {
            if (col != null && !col.enabled)
            {
                // Only enable colliders that should be active (not trigger volumes, etc.)
                if (!col.isTrigger || col.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
                {
                    col.enabled = true;
                }
            }
        }
    }

    public void ReceiveKnockback(Vector3 impactPoint, Vector3 force, ForceMode forceMode = ForceMode.Impulse)
    {
        if (ai == null || ai.isDead) return;

        float magnitude = force.magnitude;
        if (magnitude < minForceToStagger) return;

        // Apply physical force to Rigidbody
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(force, forceMode);
        }

        if (staggerRig == null) return;

        // Direction from impact to Butler (push away from impact)
        Vector3 worldDirection = (transform.position - impactPoint).normalized;
        worldDirection.y += 0.3f;
        worldDirection.Normalize();

        // Convert to local space
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        float scaledForce = magnitude * knockbackForceMultiplier;

        // Distribute to all body parts (per-limb rotation limits handle the rest)
        staggerRig.ApplyStaggerForce(localDirection, scaledForce, ButlerStaggerRig.StaggerTarget.Spine);
        staggerRig.ApplyStaggerForce(localDirection, scaledForce * 0.6f, ButlerStaggerRig.StaggerTarget.Head);

        // Apply to appropriate arm based on hit direction
        if (localDirection.x < -0.2f)
            staggerRig.ApplyStaggerForce(localDirection, scaledForce * 0.5f, ButlerStaggerRig.StaggerTarget.RightArm);
        else if (localDirection.x > 0.2f)
            staggerRig.ApplyStaggerForce(localDirection, scaledForce * 0.5f, ButlerStaggerRig.StaggerTarget.LeftArm);
        else
        {
            staggerRig.ApplyStaggerForce(localDirection, scaledForce * 0.3f, ButlerStaggerRig.StaggerTarget.LeftArm);
            staggerRig.ApplyStaggerForce(localDirection, scaledForce * 0.3f, ButlerStaggerRig.StaggerTarget.RightArm);
        }

        StartStagger(magnitude);

        if (showDebug)
        {
            Debug.DrawRay(transform.position + Vector3.up, worldDirection * scaledForce * 0.1f, Color.red, 0.5f);
        }
    }

    public void ReceiveKnockbackFrom(Vector3 sourcePosition, float forceAmount)
    {
        Vector3 direction = (transform.position - sourcePosition).normalized;
        direction.y += 0.2f;
        direction.Normalize();
        ReceiveKnockback(sourcePosition, direction * forceAmount, ForceMode.Impulse);
    }

    private void StartStagger(float forceMagnitude)
    {
        float duration = staggerDuration * Mathf.Clamp(forceMagnitude / 10f, 0.5f, 2f);
        staggerTimer = Mathf.Max(staggerTimer, duration);
        isStaggered = true;
    }

    private void EndStagger()
    {
        isStaggered = false;
        staggerTimer = 0f;
    }

    public float GetStaggerSpeedMultiplier()
    {
        return isStaggered ? staggerSpeedMultiplier : 1f;
    }
}
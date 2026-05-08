using UnityEngine;

/// <summary>
/// Handles death sequence: ragdoll activation, loot dropping, cleanup, and destruction.
/// </summary>
[DisallowMultipleComponent]
public class ButlerDeathHandler : MonoBehaviour
{
    [Header("Ragdoll / Death")]
    [Tooltip("All rigidbodies that should become kinematic=false on death")]
    public Rigidbody[] ragdollBodies;
    [Tooltip("All colliders on ragdoll bones that should enable on death")]
    public Collider[] ragdollColliders;
    [Tooltip("Main collider used while alive (disabled on death)")]
    public Collider mainCollider;
    [Tooltip("How long after death before the corpse is destroyed")]
    public float destroyDelay = 8f;

    [Header("Ragdoll Force")]
    [Tooltip("Optional: Apply an explosion force to the ragdoll on death for dramatic effect")]
    public float deathExplosionForce = 2f;
    [Tooltip("Upward modifier for the explosion force")]
    public float deathExplosionUpward = 0.5f;
    [Tooltip("Radius of the explosion force")]
    public float deathExplosionRadius = 1f;

    private ButlerAI ai;
    private ButlerAudioController audioController;
    private ButlerMovement movement;
    private CapsuleCollider capsuleCollider;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        audioController = ai.audioController;
        movement = ai.movement;

        // Try to find capsule collider if mainCollider not assigned
        if (mainCollider == null)
        {
            mainCollider = GetComponent<CapsuleCollider>();
            if (mainCollider == null)
                mainCollider = GetComponent<CharacterController>();
        }

        // Auto-find ragdoll components if not assigned in inspector
        if (ragdollBodies == null || ragdollBodies.Length == 0)
            AutoFindRagdollComponents();

        // Ensure ragdoll starts disabled
        SetRagdollEnabled(false);
    }

    /// <summary>
    /// Execute full death sequence. Called by ButlerAI when stats hit zero.
    /// </summary>
    public void Die()
    {
        if (ai.isDead) return;

        ai.isDead = true;
        ai.currentState = ButlerAI.AIState.Dead;

        // Clear status effects
        if (ai.statusEffects != null)
        {
            ai.statusEffects.ClearAllEffects();
            ai.statusEffects.enabled = false;
        }

        // Stop movement and audio
        movement.SetDesiredVelocity(Vector3.zero);
        audioController.StopMovementAudio();

        // cleanup knockback offsets
        // ButlerKnockbackReceiver knockbackReceiver = GetComponent<ButlerKnockbackReceiver>();
        // if (knockbackReceiver != null)
        //     knockbackReceiver.ResetAllSprings();

        // Disable animation
        if (ai.animator != null)
            ai.animator.enabled = false;

        // Play death sound
        audioController.PlayDeathSound();

        // ── Ragdoll Activation ────────────────────────────────────────────────
        // Store last velocity before disabling main rigidbody
        Vector3 lastVelocity = ai.rb != null ? ai.rb.linearVelocity : Vector3.zero;

        // Disable main rigidbody completely
        if (ai.rb != null)
        {
            ai.rb.linearVelocity = Vector3.zero;
            ai.rb.angularVelocity = Vector3.zero;
            ai.rb.isKinematic = true;
            ai.rb.detectCollisions = false;
            ai.rb.useGravity = false;
        }

        // Disable main collider so it doesn't interfere with ragdoll
        if (mainCollider != null)
            mainCollider.enabled = false;

        // Also disable the CharacterController if present
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        // Disable any other non-ragdoll colliders on the root object
        Collider[] rootColliders = GetComponents<Collider>();
        foreach (Collider col in rootColliders)
        {
            if (col != mainCollider && !IsRagdollCollider(col))
                col.enabled = false;
        }

        // Enable ragdoll
        SetRagdollEnabled(true);

        // Transfer any remaining velocity to ragdoll
        if (lastVelocity.magnitude > 0.1f && ragdollBodies != null && ragdollBodies.Length > 0)
        {
            // Apply velocity to the hips/pelvis (usually the root ragdoll bone)
            Rigidbody rootRagdollBody = FindRagdollRoot();
            if (rootRagdollBody != null)
            {
                rootRagdollBody.linearVelocity = lastVelocity;
            }

            // Apply all velocities from animator to ragdoll bones
            CopyAnimatorVelocityToRagdoll();
        }

        // Optional: Apply death explosion force
        if (deathExplosionForce > 0f)
        {
            ApplyDeathForce(ai.currentTarget != null ? ai.currentTarget.position : transform.position);
        }

        // Drop loot via the unified stats system
        if (ai.stats != null)
            ai.stats.SpawnDeathDrops();

        // Schedule destruction
        Destroy(gameObject, destroyDelay);

        // Disable ButlerAI (stops Update loop)
        ai.enabled = false;
    }

    // ── Ragdoll Setup ─────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-finds all rigidbodies and colliders that should be part of the ragdoll.
    /// Excludes the main GameObject's rigidbody and collider.
    /// </summary>
    private void AutoFindRagdollComponents()
    {
        Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        Collider[] allColliders = GetComponentsInChildren<Collider>(true);

        // Filter out the main rigidbody (on root GameObject)
        var bodyList = new System.Collections.Generic.List<Rigidbody>();
        var colliderList = new System.Collections.Generic.List<Collider>();

        foreach (Rigidbody rb in allRigidbodies)
        {
            if (rb.gameObject == gameObject) continue; // Skip main rigidbody
            bodyList.Add(rb);
        }

        foreach (Collider col in allColliders)
        {
            if (col.gameObject == gameObject && col == mainCollider) continue; // Skip main collider
            if (col.isTrigger) continue; // Skip triggers
            colliderList.Add(col);
        }

        ragdollBodies = bodyList.ToArray();
        ragdollColliders = colliderList.ToArray();

        Debug.Log($"[ButlerDeathHandler] Auto-found {ragdollBodies.Length} ragdoll bodies and {ragdollColliders.Length} ragdoll colliders.");
    }

    private void SetRagdollEnabled(bool enabled)
    {
        // Enable/disable ragdoll rigidbodies
        if (ragdollBodies != null)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null) continue;
                body.isKinematic = !enabled;
                body.detectCollisions = enabled;
                body.useGravity = enabled;

                if (enabled)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
            }
        }

        // Enable/disable ragdoll colliders
        if (ragdollColliders != null)
        {
            foreach (Collider col in ragdollColliders)
            {
                if (col != null)
                    col.enabled = enabled;
            }
        }
    }

    private Rigidbody FindRagdollRoot()
    {
        if (ragdollBodies == null || ragdollBodies.Length == 0) return null;

        // Try to find the hips/pelvis by name
        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null) continue;
            string name = body.name.ToLower();
            if (name.Contains("hip") || name.Contains("pelvis") || name.Contains("spine"))
                return body;
        }

        // Fallback: return first ragdoll body
        return ragdollBodies[0];
    }

    /// <summary>
    /// Copies velocity from Animator to ragdoll bones for smooth transition.
    /// </summary>
    private void CopyAnimatorVelocityToRagdoll()
    {
        if (ai.animator == null || ragdollBodies == null) return;

        // Get bone transforms from animator
        Transform hips = ai.animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform spine = ai.animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform head = ai.animator.GetBoneTransform(HumanBodyBones.Head);
        Transform leftUpperArm = ai.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rightUpperArm = ai.animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftUpperLeg = ai.animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform rightUpperLeg = ai.animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

        // Match each ragdoll body to its animator bone and copy velocity
        foreach (Rigidbody ragdollBody in ragdollBodies)
        {
            if (ragdollBody == null) continue;

            Transform matchingBone = FindMatchingBone(ragdollBody.transform);
            if (matchingBone != null)
            {
                // The animator moves bones - we approximate velocity from last frame position
                // For simplicity, we just add a small downward velocity for gravity feel
                ragdollBody.linearVelocity = Vector3.down * 0.5f;
            }
        }
    }

    private Transform FindMatchingBone(Transform ragdollTransform)
    {
        if (ai.animator == null) return null;

        // Try to find bone by name
        HumanBodyBones[] bones = (HumanBodyBones[])System.Enum.GetValues(typeof(HumanBodyBones));
        foreach (HumanBodyBones bone in bones)
        {
            if (bone == HumanBodyBones.LastBone) continue;

            Transform boneTransform = ai.animator.GetBoneTransform(bone);
            if (boneTransform != null && boneTransform.name == ragdollTransform.name)
                return boneTransform;
        }

        return null;
    }

    private void ApplyDeathForce(Vector3 attackerPosition)
    {
        if (ragdollBodies == null) return;

        Vector3 explosionOrigin = transform.position;

        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null) continue;

            Vector3 direction = (body.worldCenterOfMass - explosionOrigin).normalized;

            // Add some randomness
            direction += Random.insideUnitSphere * 0.3f;
            direction.Normalize();

            // Direction away from attacker
            Vector3 awayFromAttacker = (body.worldCenterOfMass - attackerPosition).normalized;
            direction = Vector3.Lerp(direction, awayFromAttacker, 0.5f).normalized;

            body.AddForce(direction * deathExplosionForce + Vector3.up * deathExplosionUpward, ForceMode.Impulse);
        }
    }

    private bool IsRagdollCollider(Collider col)
    {
        if (ragdollColliders == null) return false;
        foreach (Collider ragdollCol in ragdollColliders)
        {
            if (ragdollCol == col) return true;
        }
        return false;
    }
}
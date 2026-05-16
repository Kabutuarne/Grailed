using UnityEngine;

/// <summary>
/// Handles death sequence: ragdoll activation, loot dropping, cleanup, and destruction.
/// </summary>
[DisallowMultipleComponent]
public class ButlerDeathHandler : MonoBehaviour
{
    [Header("Ragdoll / Death")]
    [Tooltip("All rigidbodies that should become non-kinematic on death")]
    public Rigidbody[] ragdollBodies;
    [Tooltip("All colliders on ragdoll bones that should enable on death")]
    public Collider[] ragdollColliders;
    [Tooltip("Main collider used while alive (disabled on death)")]
    public Collider mainCollider;
    [Tooltip("How long after death before the corpse is destroyed")]
    public float destroyDelay = 8f;

    private ButlerAI ai;
    private ButlerAudioController audioController;
    private ButlerMovement movement;
    private bool isDead;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        audioController = ai.audioController;
        movement = ai.movement;

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        if (ragdollBodies == null || ragdollBodies.Length == 0)
            AutoFindRagdollComponents();

        SetRagdollEnabled(false);
    }

    /// <summary>
    /// Execute full death sequence. Called by ButlerAI when stats hit zero.
    /// </summary>
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        if (ai != null)
        {
            ai.isDead = true;
            ai.currentState = ButlerAI.AIState.Dead;
        }

        if (ai?.statusEffects != null)
        {
            ai.statusEffects.ClearAllEffects();
            ai.statusEffects.enabled = false;
        }

        movement?.SetDesiredVelocity(Vector3.zero);
        audioController?.StopMovementAudio();

        if (ai?.animator != null)
            ai.animator.enabled = false;

        if (ai?.rb != null)
        {
            ai.rb.isKinematic = true;
            ai.rb.detectCollisions = false;
            ai.rb.useGravity = false;
        }

        if (mainCollider != null)
            mainCollider.enabled = false;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        DisableRootColliders();
        SetRagdollEnabled(true);

        if (ai?.stats != null)
            ai.stats.SpawnDeathDrops();

        Destroy(gameObject, destroyDelay);
        if (ai != null)
            ai.enabled = false;
    }

    private void DisableRootColliders()
    {
        Collider[] rootColliders = GetComponents<Collider>();
        foreach (Collider col in rootColliders)
        {
            if (col != mainCollider && !IsRagdollCollider(col))
                col.enabled = false;
        }
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

        // Ensure ragdoll colliders are enabled. Keep child colliders active
        // (main/root collider is handled separately during death).
        if (ragdollColliders != null)
        {
            foreach (Collider col in ragdollColliders)
            {
                if (col != null)
                    col.enabled = true;
            }
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
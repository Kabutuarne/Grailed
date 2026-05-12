using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonRagdollController : MonoBehaviour
{
    [Header("Ragdoll Setup")]
    [Tooltip("All rigidbodies that should be active while the skeleton is ragdolled.")]
    public Rigidbody[] ragdollBodies;
    [Tooltip("All colliders on ragdoll bones that should be active while the skeleton is ragdolled.")]
    public Collider[] ragdollColliders;
    [Tooltip("Main root collider used while the skeleton is standing and chasing.")]
    public Collider mainCollider;

    private SkeletonAI ai;
    private Animator animator;
    private List<(Transform ragdollTransform, Transform animatorBone)> bonePairs;
    private bool isRagdollActive;

    public void Initialize(SkeletonAI skeletonAI)
    {
        ai = skeletonAI;
        animator = ai.animator;

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        if (ragdollBodies == null || ragdollBodies.Length == 0 ||
            ragdollColliders == null || ragdollColliders.Length == 0)
        {
            AutoFindRagdollComponents();
        }

        BuildBonePairs();
        ActivateRagdoll();
    }

    public void ActivateRagdoll()
    {
        if (isRagdollActive) return;
        isRagdollActive = true;

        SetMainColliderEnabled(false);
        SetRootRigidbodyForRagdoll();
        SetRagdollEnabled(true);

        if (animator != null)
            animator.enabled = false;

        Debug.Log($"[{gameObject.name}] Ragdoll activated with {ragdollBodies?.Length ?? 0} bodies");
    }

    public void RecoverFromRagdoll()
    {
        if (!isRagdollActive) return;

        Vector3 ragdollRootPosition = GetRagdollRootPosition();
        Quaternion ragdollRootRotation = GetRagdollRootRotation();

        SetRagdollEnabled(false);
        SetRootRigidbodyForAnimation();
        SetMainColliderEnabled(true);

        if (animator != null)
        {
            animator.enabled = true;
            animator.Update(0f);
            ApplyRagdollPoseToAnimator();
        }

        transform.position = ragdollRootPosition;
        transform.rotation = ragdollRootRotation;

        isRagdollActive = false;
        Debug.Log($"[{gameObject.name}] Recovered from ragdoll, starting get up");
    }

    public bool IsRagdollActive => isRagdollActive;

    private void SetRagdollEnabled(bool enabled)
    {
        if (ragdollBodies != null)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null) continue;
                body.isKinematic = !enabled;
                body.detectCollisions = enabled;
                body.useGravity = enabled;
                body.interpolation = enabled ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                body.collisionDetectionMode = enabled ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;

                if (enabled)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        if (ragdollColliders != null)
        {
            foreach (Collider col in ragdollColliders)
            {
                if (col == null) continue;
                col.enabled = enabled;
            }
        }
    }

    private void SetMainColliderEnabled(bool enabled)
    {
        if (mainCollider != null)
            mainCollider.enabled = enabled;
    }

    private void SetRootRigidbodyForRagdoll()
    {
        if (ai?.rb == null) return;
        ai.rb.isKinematic = true;
        ai.rb.detectCollisions = false;
        ai.rb.useGravity = false;
    }

    private void SetRootRigidbodyForAnimation()
    {
        if (ai?.rb == null) return;
        ai.rb.isKinematic = false;
        ai.rb.detectCollisions = true;
        ai.rb.useGravity = true;
    }

    private Vector3 GetRagdollRootPosition()
    {
        if (animator == null)
            return transform.position;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        return hips != null ? hips.position : transform.position;
    }

    private Quaternion GetRagdollRootRotation()
    {
        if (animator == null)
            return transform.rotation;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        return hips != null ? Quaternion.LookRotation(hips.forward, Vector3.up) : transform.rotation;
    }

    private void ApplyRagdollPoseToAnimator()
    {
        if (bonePairs == null || bonePairs.Count == 0)
            return;

        foreach (var pair in bonePairs)
        {
            if (pair.ragdollTransform == null || pair.animatorBone == null)
                continue;

            pair.animatorBone.position = pair.ragdollTransform.position;
            pair.animatorBone.rotation = pair.ragdollTransform.rotation;
        }

        Physics.SyncTransforms();
    }

    private void BuildBonePairs()
    {
        bonePairs = new List<(Transform ragdollTransform, Transform animatorBone)>();

        if (ragdollBodies == null || animator == null)
            return;

        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null) continue;
            Transform animatorBone = FindMatchingBone(body.transform);
            if (animatorBone != null)
                bonePairs.Add((body.transform, animatorBone));
        }
    }

    private void AutoFindRagdollComponents()
    {
        Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        Collider[] allColliders = GetComponentsInChildren<Collider>(true);

        var bodyList = new List<Rigidbody>();
        var colliderList = new List<Collider>();

        foreach (Rigidbody body in allRigidbodies)
        {
            if (body == null || body.gameObject == gameObject) continue;
            bodyList.Add(body);
        }

        foreach (Collider col in allColliders)
        {
            if (col == null) continue;
            if (col.gameObject == gameObject && col == mainCollider) continue;
            if (col.isTrigger) continue;
            colliderList.Add(col);
        }

        ragdollBodies = bodyList.ToArray();
        ragdollColliders = colliderList.ToArray();
        Debug.Log($"[{gameObject.name}] Auto-found {ragdollBodies.Length} ragdoll bodies and {ragdollColliders.Length} ragdoll colliders");
    }

    private Transform FindMatchingBone(Transform ragdollTransform)
    {
        if (animator == null || ragdollTransform == null)
            return null;

        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone == HumanBodyBones.LastBone)
                continue;

            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform != null && boneTransform.name == ragdollTransform.name)
                return boneTransform;
        }

        return null;
    }
}

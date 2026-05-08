// ButlerStaggerRig.cs
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Drives DampedTransform sources to create stagger feedback on the Butler.
/// Sources are positioned at bone locations and pushed away on impact.
/// DampedTransform constraints smoothly pull bones toward displaced sources.
/// </summary>
public class ButlerStaggerRig : MonoBehaviour
{
    [Header("Rig Reference")]
    public Rig staggerRig;
    [Tooltip("How quickly the rig weight blends in/out")]
    public float rigWeightBlendSpeed = 12f;

    [Header("Bone References")]
    public Transform headBone;
    public Transform spineBone;
    public Transform leftArmBone;
    public Transform rightArmBone;

    [Header("Position Stagger Settings")]
    [Tooltip("Maximum displacement from bone position")]
    public float maxDisplacement = 0.15f;
    [Tooltip("How fast sources return to bones (higher = faster)")]
    public float recoverySpeed = 8f;
    [Tooltip("Multiplier for incoming force to displacement")]
    [Range(0.01f, 0.3f)]
    public float forceToDisplacement = 0.06f;

    [Header("Per-Limb Rotation Settings")]
    public float headMaxRotation = 14f;
    public float spineMaxRotation = 18f;
    public float leftArmMaxRotation = 22f;
    public float rightArmMaxRotation = 22f;
    public float headRotationRecovery = 140f;
    public float spineRotationRecovery = 110f;
    public float armRotationRecovery = 130f;

    [Header("Rotation Force Scaling")]
    [Tooltip("Multiplier for incoming force to rotation angle")]
    [Range(0.05f, 3f)]
    public float forceToRotation = 0.6f;

    [Header("IK Control")]
    public ButlerAnimationController animationController;

    // ── Source Transforms (for DampedTransform constraints) ──────────────────
    [Header("Source Objects (Assign in Inspector)")]
    public Transform headSource;
    public Transform spineSource;
    public Transform leftArmSource;
    public Transform rightArmSource;

    // ── Runtime State ─────────────────────────────────────────────────────────
    private struct StaggerState
    {
        public Vector3 offset;
        public Quaternion rotation;
        public Transform bone;
        public Transform source;

        public bool IsPositionZero()
        {
            return offset.sqrMagnitude < 0.0001f;
        }

        public bool IsRotationZero()
        {
            return Quaternion.Angle(rotation, Quaternion.identity) < 0.5f;
        }

        public bool IsZero()
        {
            return IsPositionZero() && IsRotationZero();
        }

        public void Reset()
        {
            offset = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }

    private StaggerState headState;
    private StaggerState spineState;
    private StaggerState leftArmState;
    private StaggerState rightArmState;

    private float targetRigWeight;
    private float currentRigWeight;
    private bool isStaggering;
    private float staggerTimer;
    private bool ikWasEnabled;

    private void Start()
    {
        // Cache bone/source pairs
        headState = new StaggerState { bone = headBone, source = headSource };
        spineState = new StaggerState { bone = spineBone, source = spineSource };
        leftArmState = new StaggerState { bone = leftArmBone, source = leftArmSource };
        rightArmState = new StaggerState { bone = rightArmBone, source = rightArmSource };

        InitializeSources();

        // Find animation controller if not assigned
        if (animationController == null)
            animationController = GetComponentInParent<ButlerAnimationController>();
    }

    /// <summary>
    /// Position all source objects at their corresponding bone positions.
    /// </summary>
    public void InitializeSources()
    {
        ResetState(ref headState);
        ResetState(ref spineState);
        ResetState(ref leftArmState);
        ResetState(ref rightArmState);

        // Start with rig weight at 0
        targetRigWeight = 0f;
        currentRigWeight = 0f;
        if (staggerRig != null)
            staggerRig.weight = 0f;
    }

    private void ResetState(ref StaggerState state)
    {
        state.Reset();
        if (state.bone != null && state.source != null)
        {
            state.source.position = state.bone.position;
            state.source.rotation = state.bone.rotation;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (isStaggering)
        {
            staggerTimer -= dt;

            // Recover all offsets toward zero with per-limb speeds
            RecoverState(ref headState, StaggerTarget.Head, dt);
            RecoverState(ref spineState, StaggerTarget.Spine, dt);
            RecoverState(ref leftArmState, StaggerTarget.LeftArm, dt);
            RecoverState(ref rightArmState, StaggerTarget.RightArm, dt);

            // Update source transforms
            UpdateSourceTransform(ref headState);
            UpdateSourceTransform(ref spineState);
            UpdateSourceTransform(ref leftArmState);
            UpdateSourceTransform(ref rightArmState);

            // Check if fully recovered
            if (staggerTimer <= 0f && AllStatesZero())
            {
                isStaggering = false;
                targetRigWeight = 0f;

                // Re-enable IK
                if (animationController != null)
                    animationController.enableLookIK = ikWasEnabled;
            }
        }

        // Smoothly blend rig weight
        currentRigWeight = Mathf.MoveTowards(currentRigWeight, targetRigWeight, rigWeightBlendSpeed * dt);
        if (staggerRig != null)
            staggerRig.weight = currentRigWeight;
    }

    private void RecoverState(ref StaggerState state, StaggerTarget target, float dt)
    {
        state.offset = Vector3.MoveTowards(state.offset, Vector3.zero, recoverySpeed * dt);

        float rotRecovery = GetRotationRecoverySpeed(target);
        state.rotation = Quaternion.RotateTowards(state.rotation, Quaternion.identity, rotRecovery * dt);
    }

    private void UpdateSourceTransform(ref StaggerState state)
    {
        if (state.bone != null && state.source != null)
        {
            state.source.position = state.bone.position + state.offset;
            state.source.rotation = state.bone.rotation * state.rotation;
        }
    }

    private bool AllStatesZero()
    {
        return headState.IsZero() && spineState.IsZero() && leftArmState.IsZero() && rightArmState.IsZero();
    }

    /// <summary>
    /// Apply stagger force to a body part. Called by ButlerKnockbackReceiver.
    /// </summary>
    public void ApplyStaggerForce(Vector3 localDirection, float force, StaggerTarget target)
    {
        if (headBone == null || spineBone == null) return;

        // Calculate displacement
        float displacement = Mathf.Clamp(force * forceToDisplacement, 0.01f, maxDisplacement);
        Vector3 worldDirection = transform.TransformDirection(localDirection.normalized);
        Vector3 worldPush = worldDirection * displacement;

        // Calculate rotation angle, clamped to per-limb max
        float boneMaxRotation = GetMaxRotation(target);
        float rotationAngle = Mathf.Clamp(force * forceToRotation, 1f, boneMaxRotation);
        Quaternion deltaRotation = GetRotationForTarget(target, rotationAngle);

        // Activate staggering
        if (!isStaggering)
        {
            isStaggering = true;
            staggerTimer = 0f;

            // Disable head IK during stagger for more natural look
            if (animationController != null)
            {
                ikWasEnabled = animationController.enableLookIK;
                animationController.enableLookIK = false;
            }
        }

        // Reset stagger duration on each hit
        staggerTimer = 0.5f;
        targetRigWeight = 1f;

        // Apply to the targeted state
        float maxRot = GetMaxRotation(target);
        switch (target)
        {
            case StaggerTarget.Head:
                ApplyForceToState(ref headState, worldPush, deltaRotation, maxRot);
                break;
            case StaggerTarget.Spine:
                ApplyForceToState(ref spineState, worldPush, deltaRotation, maxRot);
                break;
            case StaggerTarget.LeftArm:
                ApplyForceToState(ref leftArmState, worldPush, deltaRotation, maxRot);
                break;
            case StaggerTarget.RightArm:
                ApplyForceToState(ref rightArmState, worldPush, deltaRotation, maxRot);
                break;
        }

        UpdateSourceTransform(ref headState);
        UpdateSourceTransform(ref spineState);
        UpdateSourceTransform(ref leftArmState);
        UpdateSourceTransform(ref rightArmState);
    }

    private void ApplyForceToState(ref StaggerState state, Vector3 worldPush, Quaternion deltaRotation, float maxRotation)
    {
        state.offset += worldPush;
        state.offset = Vector3.ClampMagnitude(state.offset, maxDisplacement);
        state.rotation = ClampRotation(state.rotation * deltaRotation, maxRotation);
    }

    private float GetMaxRotation(StaggerTarget target)
    {
        return target switch
        {
            StaggerTarget.Head => headMaxRotation,
            StaggerTarget.Spine => spineMaxRotation,
            StaggerTarget.LeftArm => leftArmMaxRotation,
            StaggerTarget.RightArm => rightArmMaxRotation,
            _ => 18f
        };
    }

    private float GetRotationRecoverySpeed(StaggerTarget target)
    {
        return target switch
        {
            StaggerTarget.Head => headRotationRecovery,
            StaggerTarget.Spine => spineRotationRecovery,
            StaggerTarget.LeftArm => armRotationRecovery,
            StaggerTarget.RightArm => armRotationRecovery,
            _ => 120f
        };
    }

    private Quaternion GetRotationForTarget(StaggerTarget target, float angle)
    {
        // Bone-local axes:
        // Head: rotate on -bone.right (local -X, tilts backward)
        // Spine: rotate on -bone.right (local -X, bends backward)
        // Arms: rotate on -bone.forward (local -Z, swings backward)
        Vector3 localAxis = target switch
        {
            StaggerTarget.Head when headBone != null => -headBone.right,
            StaggerTarget.Spine when spineBone != null => -spineBone.right,
            StaggerTarget.LeftArm when leftArmBone != null => -leftArmBone.forward,
            StaggerTarget.RightArm when rightArmBone != null => -rightArmBone.forward,
            _ => transform.up
        };

        return Quaternion.AngleAxis(angle, localAxis);
    }

    private Quaternion ClampRotation(Quaternion rotation, float maxAngle)
    {
        float currentAngle = Quaternion.Angle(Quaternion.identity, rotation);
        return currentAngle <= maxAngle ? rotation : Quaternion.RotateTowards(Quaternion.identity, rotation, maxAngle);
    }

    /// <summary>
    /// Immediately reset all sources to bone positions.
    /// </summary>
    public void ResetAllSources()
    {
        InitializeSources();
        isStaggering = false;
        targetRigWeight = 0f;
        currentRigWeight = 0f;
        if (staggerRig != null)
            staggerRig.weight = 0f;

        if (animationController != null)
            animationController.enableLookIK = ikWasEnabled;
    }

    public enum StaggerTarget
    {
        Head,
        Spine,
        LeftArm,
        RightArm
    }
}
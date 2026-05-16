using UnityEngine;

/// <summary>
/// Handles Animator parameter updates and IK look-at behavior.
/// Supports single animation playing forward (positive speed) or reversed (negative speed).
/// WalkSpeed acts as a multiplier: 0 = standing still, 1 = normal animation speed, >1 = faster animation.
/// </summary>
[DisallowMultipleComponent]
public class ButlerAnimationController : MonoBehaviour
{
    [Header("Animation")]
    public string animWalkSpeed = "WalkSpeed";
    public string animWalkDirection = "WalkDirection";
    public string animAttackTrig = "Attack";
    public string animMirrorBool = "MirrorAttack";
    [Tooltip("How fast the animation speed blend responds")]
    public float animationBlendSpeed = 4f;
    [Tooltip("How fast the direction blend responds")]
    public float directionBlendSpeed = 6f;
    [Tooltip("Maximum animation speed multiplier when moving faster than sprint speed")]
    public float maxAnimationMultiplier = 2f;

    [Header("Animation Layers")]
    [Tooltip("Base layer containing the attack state")]
    public int attackLayerIndex = 0;

    [Header("IK Settings")]
    [Tooltip("Enable head look-at IK to track target")]
    public bool enableLookIK = true;
    [Tooltip("Butler starts looking at the player when closer than this distance while chasing")]
    public float headLookRange = 6f;
    [Tooltip("To adjust the height of the look target so it looks right into the camera")]
    public float headLookYOffset = 1.7f;
    [Tooltip("How quickly the head IK weight blends in and out")]
    public float headLookSpeed = 3f;
    [Tooltip("Master weight of head look-at")]
    [Range(0f, 1f)] public float lookIKWeight = 1f;
    [Tooltip("How much the head turns toward the target")]
    [Range(0f, 1f)] public float headWeight = 0.85f;
    [Tooltip("How much the body follows the head turn")]
    [Range(0f, 1f)] public float bodyWeight = 0.15f;

    [Header("Attack Hand IK")]
    [Tooltip("Enable IK to pull the punching hand toward the player during the strike phase.")]
    public bool attackHandIKEnabled = true;
    [Tooltip("Normalized time range within the Attack state where IK is active. " +
            "0.2–0.6 covers the strike, skipping windup and follow-through.")]
    public float attackIKStartNorm = 0.2f;
    public float attackIKPeakNorm = 0.4f;
    public float attackIKEndNorm = 0.65f;
    [Tooltip("Max IK weight at peak. Keep below 0.7 to avoid rubber-arm look.")]
    [Range(0f, 1f)] public float attackIKMaxWeight = 0.55f;
    [Tooltip("Height offset above the player's root to target (chest level).")]
    public float attackIKTargetHeight = 0.9f;

    private ButlerAI ai;
    private Animator animator;
    private float currentLookWeight;
    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    private bool wasInAttackState;
    private AvatarIKGoal activePunchHand = AvatarIKGoal.RightHand;
    private bool mirrorAttack;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
        animator = ai.animator;
        currentLookWeight = 0f;
        lastPosition = transform.position;
    }

    /// <summary>
    /// Called each frame from ButlerAI.Update().
    /// Calculates actual movement direction and speed from real world movement.
    /// Supports backwards movement (negative WalkDirection) for single animation.
    /// WalkSpeed is a multiplier: 0 = stopped, 1 = normal speed, >1 = faster animation.
    /// </summary>
    public void Tick()
    {
        if (animator == null) return;

        // Calculate actual velocity from position changes (handles knockback, stagger, physics forces)
        Vector3 newPosition = transform.position;
        currentVelocity = (newPosition - lastPosition) / Time.deltaTime;
        lastPosition = newPosition;

        // Get horizontal movement speed (ignore vertical for ground movement)
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float actualSpeed = horizontalVelocity.magnitude;

        // Calculate speed multiplier based on actual movement speed
        // 0 = standing still, 1 = walking speed, >1 = faster than walk speed
        float speedMultiplier = 0f;
        if (ai != null && ai.WalkSpeed > 0.001f)
        {
            float rawRatio = actualSpeed / ai.WalkSpeed;
            speedMultiplier = Mathf.Clamp(rawRatio, 0f, maxAnimationMultiplier);
        }

        // Calculate movement direction relative to the enemy's forward direction
        float walkDirection = 0f;
        if (actualSpeed > 0.05f)
        {
            Vector3 moveDirection = horizontalVelocity.normalized;
            Vector3 enemyForward = transform.forward;
            enemyForward.y = 0f;
            enemyForward.Normalize();

            float dot = Vector3.Dot(moveDirection, enemyForward);
            if (dot > 0.4f)
                walkDirection = 1f;
            else if (dot < -0.4f)
                walkDirection = -1f;
            else
                walkDirection = 0f;
        }

        // Smoothly blend the speed multiplier parameter (0 to maxAnimationMultiplier)
        if (!string.IsNullOrEmpty(animWalkSpeed))
        {
            float currentSpeed = animator.GetFloat(animWalkSpeed);
            float targetSpeed = speedMultiplier;
            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * animationBlendSpeed);
            animator.SetFloat(animWalkSpeed, nextSpeed);
        }

        // Smoothly blend the direction parameter (-1, 0, 1)
        if (!string.IsNullOrEmpty(animWalkDirection))
        {
            float currentDirection = animator.GetFloat(animWalkDirection);
            float targetDirection = (actualSpeed > 0.05f) ? walkDirection : 0f;
            float nextDirection = Mathf.MoveTowards(currentDirection, targetDirection, Time.deltaTime * directionBlendSpeed);
            animator.SetFloat(animWalkDirection, nextDirection);
        }

        // Track attack state for end notification
        TrackAttackState();
    }

    /// <summary>
    /// Detects when the animator leaves an "Attack"-tagged state on the attack layer
    /// and notifies ButlerCombat so it can disarm hitboxes and reset attack state.
    /// The attack animation state must have its Tag set to "Attack" in the Animator.
    /// </summary>
    private void TrackAttackState()
    {
        if (ai?.combat == null) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        bool inAttackState = info.IsTag("Attack") && !animator.IsInTransition(attackLayerIndex);

        if (wasInAttackState && !inAttackState)
            ai.combat.OnAttackAnimationEnd();

        wasInAttackState = inAttackState;
    }

    /// <summary>
    /// Trigger an attack animation with optional mirroring.
    /// </summary>
    public void TriggerAttack(bool mirror)
    {
        if (animator == null) return;

        // Capture which hand punches THIS swing before flipping
        activePunchHand = mirror ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
        mirrorAttack = mirror;

        if (!string.IsNullOrEmpty(animMirrorBool))
            animator.SetBool(animMirrorBool, mirror);

        if (!string.IsNullOrEmpty(animAttackTrig))
            animator.SetTrigger(animAttackTrig);
    }

    /// <summary>
    /// Called by ButlerAI via OnAnimatorIK.
    /// </summary>
    public void OnAnimatorIK(int layerIndex)
    {
        if (ai == null || ai.isDead || animator == null) return;

        // ── Head look IK ─────────────────────────────────────────────────────
        Transform lookTarget = null;

        if (enableLookIK && ai.currentState == ButlerAI.AIState.Chasing && ai.currentTarget != null)
        {
            float dist = ButlerTargeting.FlatDistance(transform.position, ai.currentTarget.position);
            if (dist <= headLookRange)
                lookTarget = ai.currentTarget;
        }

        float desiredWeight = lookTarget != null ? lookIKWeight : 0f;
        currentLookWeight = Mathf.MoveTowards(currentLookWeight, desiredWeight, Time.deltaTime * headLookSpeed);

        if (currentLookWeight > 0.001f && lookTarget != null)
        {
            Vector3 lookPoint = lookTarget.position + Vector3.up * headLookYOffset;
            animator.SetLookAtPosition(lookPoint);
            animator.SetLookAtWeight(currentLookWeight, bodyWeight, headWeight, 0f, 0.5f);
        }
        else
        {
            animator.SetLookAtWeight(0f);
        }

        // ── Attack hand IK ────────────────────────────────────────────────────
        if (attackHandIKEnabled && ai.currentTarget != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);

            if (info.IsTag("Attack"))
            {
                float t = info.normalizedTime % 1f; // loop-safe

                // Triangle ramp: 0 at start → 1 at peak → 0 at end
                float ikWeight = 0f;
                if (t >= attackIKStartNorm && t <= attackIKEndNorm)
                {
                    if (t <= attackIKPeakNorm)
                        ikWeight = Mathf.InverseLerp(attackIKStartNorm, attackIKPeakNorm, t);
                    else
                        ikWeight = Mathf.InverseLerp(attackIKEndNorm, attackIKPeakNorm, t);

                    ikWeight *= attackIKMaxWeight;
                }

                // Get the closest point on the player's collider surface to avoid phasing through
                Vector3 targetPos = ai.currentTarget.position + Vector3.up * attackIKTargetHeight;
                Collider targetCollider = ai.currentTarget.GetComponent<Collider>();
                if (targetCollider != null)
                {
                    Vector3 closestPoint = targetCollider.ClosestPoint(transform.position);
                    // Offset slightly outward from the surface to hit properly
                    Vector3 dirFromTarget = (transform.position - ai.currentTarget.position).normalized;
                    targetPos = closestPoint + dirFromTarget * 0.05f;
                }

                animator.SetIKPositionWeight(activePunchHand, ikWeight);
                animator.SetIKPosition(activePunchHand, targetPos);
            }
            else
            {
                // Outside attack state — zero out both hand IK goals
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            }
        }
    }
}
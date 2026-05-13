using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonAnimationController : MonoBehaviour
{
    [Header("Animation Parameters")]
    public string animGetUpTrigger = "GetUp";
    public string animAttackTrigger = "Attack";
    public string animWalkSpeed = "WalkSpeed";
    public string animWalkDirection = "WalkDirection";
    public string animMirrorBool = "MirrorAttack";
    private bool mirrorAttack;

    [Header("Animation Layers")]
    [Tooltip("Base locomotion layer")]
    public int locomotionLayerIndex = 0;
    [Tooltip("Upper body layer (avatar-masked to upper body)")]
    public int upperBodyLayerIndex = 1;
    [Range(0f, 1f)] public float upperBodyWeight = 1f;

    [Header("Animation Speed Mapping")]
    public float animationBlendSpeed = 4f;
    public float directionBlendSpeed = 6f;
    public float maxAnimationMultiplier = 2f;

    [Header("IK Settings")]
    public bool headLookEnabled = true;
    public float headLookRange = 8f;
    public float headLookYOffset = 1.8f;
    [Range(0f, 1f)] public float lookIKWeight = 1f;
    [Range(0f, 1f)] public float bodyWeight = 0.2f;
    [Range(0f, 1f)] public float headWeight = 0.9f;
    public float headLookBlendSpeed = 3f;
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
    [Header("Get-Up Timing")]
    [Tooltip("Fallback timeout if the GetUp state tag is never detected.")]
    public float getUpTimeout = 3f;

    private SkeletonAI ai;
    private Animator animator;
    private bool isFrozen;
    private bool headIKEnabled = true;

    private float currentLookWeight;
    private float currentUpperBodyWeight;
    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    private float getUpTimer;

    // Attack-state tracking
    private bool wasInAttackState;

    public void Initialize(SkeletonAI skeletonAI)
    {
        ai = skeletonAI;
        animator = ai.animator;
        lastPosition = transform.position;

        if (animator != null)
            animator.SetLayerWeight(upperBodyLayerIndex, 1f);
    }

    private void Start()
    {
        StartCoroutine(CheckGetUpCompletion());
    }

    private IEnumerator CheckGetUpCompletion()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            if (animator == null || ai == null) continue;
            if (ai.currentState != SkeletonAI.AIState.GettingUp) continue;
            if (isFrozen) continue;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(locomotionLayerIndex);
            getUpTimer += 0.1f;

            if (info.IsTag("GetUp") && info.normalizedTime >= 0.95f && !animator.IsInTransition(locomotionLayerIndex))
            {
                OnGetUpAnimationFinished();
                continue;
            }

            if (getUpTimer >= getUpTimeout)
            {
                Debug.LogWarning($"[{gameObject.name}] GetUp timeout — forcing completion.");
                OnGetUpAnimationFinished();
            }
        }
    }

    public void Tick()
    {
        if (animator == null || !animator.enabled || isFrozen) return;

        UpdateMovementAnimation();
        SmoothUpperBodyWeight();
        TrackAttackState();
    }

    // ── Attack-state tracking ─────────────────────────────────────────────────

    /// <summary>
    /// Detects when the animator leaves an "Attack"-tagged state on the upper-body layer
    /// and notifies SkeletonCombat so it can disarm hitboxes and start the cooldown.
    /// The attack animation state must have its Tag set to "Attack" in the Animator.
    /// </summary>
    private void TrackAttackState()
    {
        if (ai?.combat == null) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex);
        bool inAttackState = info.IsTag("Attack") && !animator.IsInTransition(upperBodyLayerIndex);

        if (wasInAttackState && !inAttackState)
            ai.combat.OnAttackAnimationEnd();

        wasInAttackState = inAttackState;
    }

    // ── Movement animation ────────────────────────────────────────────────────

    private void UpdateMovementAnimation()
    {
        Vector3 newPosition = transform.position;
        currentVelocity = (newPosition - lastPosition) / Time.deltaTime;
        lastPosition = newPosition;

        Vector3 horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float actualSpeed = horizontal.magnitude;

        float speedMultiplier = 0f;
        if (ai != null && ai.WalkSpeed > 0.001f)
            speedMultiplier = Mathf.Clamp(actualSpeed / ai.WalkSpeed, 0f, maxAnimationMultiplier);

        float walkDirection = 0f;
        if (actualSpeed > 0.05f)
        {
            float dot = Vector3.Dot(horizontal.normalized, transform.forward.normalized);
            walkDirection = dot > 0.4f ? 1f : dot < -0.4f ? -1f : 0f;
        }

        if (!string.IsNullOrEmpty(animWalkSpeed))
        {
            float next = Mathf.MoveTowards(animator.GetFloat(animWalkSpeed), speedMultiplier, Time.deltaTime * animationBlendSpeed);
            animator.SetFloat(animWalkSpeed, next);
        }

        if (!string.IsNullOrEmpty(animWalkDirection))
        {
            float target = actualSpeed > 0.05f ? walkDirection : 0f;
            float next = Mathf.MoveTowards(animator.GetFloat(animWalkDirection), target, Time.deltaTime * directionBlendSpeed);
            animator.SetFloat(animWalkDirection, next);
        }
    }

    private void SmoothUpperBodyWeight()
    {
        currentUpperBodyWeight = Mathf.Lerp(currentUpperBodyWeight, upperBodyWeight, Time.deltaTime * 5f);
        animator.SetLayerWeight(upperBodyLayerIndex, currentUpperBodyWeight);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void FreezeAnimation(bool freeze)
    {
        isFrozen = freeze;
        if (animator == null) return;

        if (freeze)
        {
            animator.speed = 0f;
            headIKEnabled = false;
            animator.SetFloat(animWalkSpeed, 0f);
            animator.SetFloat(animWalkDirection, 0f);
        }
        else
        {
            animator.speed = 1f;
            headIKEnabled = true;
        }
    }

    public void TriggerGetUp()
    {
        if (animator == null) return;
        getUpTimer = 0f;
        animator.SetTrigger(animGetUpTrigger);
        animator.SetFloat(animWalkSpeed, 0f);
    }

    public void OnGetUpAnimationFinished()
    {
        if (animator != null)
            animator.speed = 1f;
        ai?.OnGetUpFinished();
    }

    private AvatarIKGoal activePunchHand = AvatarIKGoal.RightHand;

    public void TriggerAttack()
    {
        if (animator == null) return;

        // Capture which hand punches THIS swing before flipping
        activePunchHand = mirrorAttack ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;

        animator.SetTrigger(animAttackTrigger);
        animator.SetBool(animMirrorBool, mirrorAttack);
        mirrorAttack = !mirrorAttack;
    }

    // ── IK ────────────────────────────────────────────────────────────────────

    public void OnAnimatorIK(int layerIndex)
    {
        if (ai == null || ai.isDead || animator == null || !animator.enabled || !headIKEnabled) return;

        // ── Head look (existing) ──────────────────────────────────────────────────
        Transform lookTarget = null;
        if (headLookEnabled && ai.currentTarget != null &&
            ai.currentState == SkeletonAI.AIState.Chasing)
        {
            float dist = SkeletonTargeting.FlatDistance(transform.position, ai.currentTarget.position);
            if (dist <= headLookRange)
                lookTarget = ai.currentTarget;
        }

        float desiredWeight = lookTarget != null ? lookIKWeight : 0f;
        currentLookWeight = Mathf.MoveTowards(currentLookWeight, desiredWeight,
                                              Time.deltaTime * headLookBlendSpeed);

        if (currentLookWeight > 0.001f && lookTarget != null)
        {
            animator.SetLookAtPosition(lookTarget.position + Vector3.up * headLookYOffset);
            animator.SetLookAtWeight(currentLookWeight, bodyWeight, headWeight);
        }
        else
        {
            animator.SetLookAtWeight(0f);
        }

        // ── Attack hand IK ────────────────────────────────────────────────────────
        if (attackHandIKEnabled && ai.currentTarget != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex);

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

                // mirrorAttack toggles each swing; false = right hand, true = left hand
                AvatarIKGoal punchHand = activePunchHand;
                Vector3 targetPos = ai.currentTarget.position + Vector3.up * attackIKTargetHeight;

                animator.SetIKPositionWeight(punchHand, ikWeight);
                animator.SetIKPosition(punchHand, targetPos);
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
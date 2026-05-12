using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class SkeletonAnimationController : MonoBehaviour
{
    [Header("Animation Parameters")]
    public string animGetUpTrigger = "GetUp";
    public string animAttackingBool = "Attacking";
    public string animAttackTrigger = "Attack";
    public string animWalkSpeed = "WalkSpeed";
    public string animWalkDirection = "WalkDirection";

    [Header("Animation Layers")]
    [Tooltip("Base locomotion layer (whole body, but upper body masked later)")]
    public int locomotionLayerIndex = 0;
    [Tooltip("Upper body layer for attacks (avatar mask restricts to upper body)")]
    public int upperBodyLayerIndex = 1;
    [Range(0f, 1f)] public float upperBodyWeight = 1f;

    [Header("Animation Speed Mapping")]
    [Tooltip("How fast the animation speed blend responds")]
    public float animationBlendSpeed = 4f;
    [Tooltip("How fast the direction blend responds")]
    public float directionBlendSpeed = 6f;
    [Tooltip("Maximum animation speed multiplier when moving faster than walk speed")]
    public float maxAnimationMultiplier = 2f;

    [Header("IK Settings")]
    public bool headLookEnabled = true;
    public float headLookRange = 8f;
    public float headLookYOffset = 1.8f;
    [Range(0f, 1f)] public float lookIKWeight = 1f;
    [Range(0f, 1f)] public float bodyWeight = 0.2f;
    [Range(0f, 1f)] public float headWeight = 0.9f;
    public float headLookBlendSpeed = 3f;

    private SkeletonAI ai;
    private Animator animator;
    private bool isFrozen;
    private bool headIKEnabled = true;
    private float currentLookWeight;
    private float currentUpperBodyWeight;
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

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

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("GetUp") &&
                stateInfo.normalizedTime >= 0.95f &&
                !animator.IsInTransition(0))
            {
                OnGetUpAnimationFinished();
            }
        }
    }

    public void Tick()
    {
        if (animator == null || !animator.enabled || isFrozen) return;

        UpdateMovementAnimation();
        SmoothUpperBodyWeight();
    }

    /// <summary>
    /// Calculates actual velocity from position changes (handles knockback, stagger, physics forces).
    /// WalkSpeed is a multiplier: 0 = stopped, 1 = normal walk speed, >1 = faster.
    /// WalkDirection: 1 = forward, -1 = backward, 0 = sideways/stationary.
    /// </summary>
    private void UpdateMovementAnimation()
    {
        Vector3 newPosition = transform.position;
        currentVelocity = (newPosition - lastPosition) / Time.deltaTime;
        lastPosition = newPosition;

        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float actualSpeed = horizontalVelocity.magnitude;

        // Speed multiplier: 0 = still, 1 = walk speed, >1 = faster
        float speedMultiplier = 0f;
        if (ai != null && ai.WalkSpeed > 0.001f)
        {
            float rawRatio = actualSpeed / ai.WalkSpeed;
            speedMultiplier = Mathf.Clamp(rawRatio, 0f, maxAnimationMultiplier);
        }

        // Direction: dot against forward to detect backward movement
        float walkDirection = 0f;
        if (actualSpeed > 0.05f)
        {
            Vector3 moveDirection = horizontalVelocity.normalized;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            float dot = Vector3.Dot(moveDirection, forward);
            if (dot > 0.4f)
                walkDirection = 1f;
            else if (dot < -0.4f)
                walkDirection = -1f;
            else
                walkDirection = 0f;
        }

        // Blend speed
        if (!string.IsNullOrEmpty(animWalkSpeed))
        {
            float current = animator.GetFloat(animWalkSpeed);
            float next = Mathf.MoveTowards(current, speedMultiplier, Time.deltaTime * animationBlendSpeed);
            animator.SetFloat(animWalkSpeed, next);
        }

        // Blend direction
        if (!string.IsNullOrEmpty(animWalkDirection))
        {
            float current = animator.GetFloat(animWalkDirection);
            float target = actualSpeed > 0.05f ? walkDirection : 0f;
            float next = Mathf.MoveTowards(current, target, Time.deltaTime * directionBlendSpeed);
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
        animator.SetTrigger(animGetUpTrigger);
        animator.SetFloat(animWalkSpeed, 0f);
    }

    public void OnGetUpAnimationFinished()
    {
        if (animator != null)
            animator.speed = 1f;
        ai?.OnGetUpFinished();
    }

    public void SetAttacking(bool attacking)
    {
        if (animator != null)
            animator.SetBool(animAttackingBool, attacking);
    }

    public void TriggerAttack()
    {
        if (animator != null)
            animator.SetTrigger(animAttackTrigger);
    }

    // ── IK ────────────────────────────────────────────────────────────────────

    public void OnAnimatorIK(int layerIndex)
    {
        if (ai == null || ai.isDead || animator == null || !animator.enabled || !headIKEnabled) return;

        Transform lookTarget = null;
        if (headLookEnabled && ai.currentTarget != null &&
            ai.currentState == SkeletonAI.AIState.Chasing)
        {
            float dist = SkeletonTargeting.FlatDistance(transform.position, ai.currentTarget.position);
            if (dist <= headLookRange)
                lookTarget = ai.currentTarget;
        }

        float desiredWeight = lookTarget != null ? lookIKWeight : 0f;
        currentLookWeight = Mathf.MoveTowards(currentLookWeight, desiredWeight, Time.deltaTime * headLookBlendSpeed);

        if (currentLookWeight > 0.001f && lookTarget != null)
        {
            Vector3 lookPoint = lookTarget.position + Vector3.up * headLookYOffset;
            animator.SetLookAtPosition(lookPoint);
            animator.SetLookAtWeight(currentLookWeight, bodyWeight, headWeight);
        }
        else
        {
            animator.SetLookAtWeight(0f);
        }
    }
}
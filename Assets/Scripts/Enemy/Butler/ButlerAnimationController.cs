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
    public string animWalkDirection = "WalkDirection"; // NEW: Controls animation direction (-1 to 1)
    public string animAttackTrig = "Attack";
    public string animMirrorBool = "MirrorAttack";
    [Tooltip("How fast the animation speed blend responds")]
    public float animationBlendSpeed = 4f;
    [Tooltip("How fast the direction blend responds")]
    public float directionBlendSpeed = 6f;
    [Tooltip("Maximum animation speed multiplier when moving faster than sprint speed")]
    public float maxAnimationMultiplier = 2f;

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

    private ButlerAI ai;
    private Animator animator;
    private float currentLookWeight;
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

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
    }

    /// <summary>
    /// Trigger an attack animation with optional mirroring.
    /// </summary>
    public void TriggerAttack(bool mirror)
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(animMirrorBool))
            animator.SetBool(animMirrorBool, mirror);

        if (!string.IsNullOrEmpty(animAttackTrig))
            animator.SetTrigger(animAttackTrig);
    }

    /// <summary>
    /// Called by ButlerIKBridge or directly from OnAnimatorIK.
    /// </summary>
    public void OnAnimatorIK(int layerIndex)
    {
        if (ai == null || ai.isDead || animator == null) return;

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
    }
}
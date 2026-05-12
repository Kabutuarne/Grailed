using UnityEngine;

/// <summary>
/// Central hub for the Skeleton enemy.
/// The skeleton starts alive as a ragdoll, wakes when the player enters range,
/// transitions into the get-up animation, and then chases and attacks normally.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class SkeletonAI : MonoBehaviour
{
    public enum AIState { Ragdoll, GettingUp, Chasing, Dead }

    [Header("Sub-Components (auto-populated if left empty)")]
    public SkeletonMovement movement;
    public SkeletonTargeting targeting;
    public SkeletonCombat combat;
    public SkeletonAnimationController animationController;
    public SkeletonRagdollController ragdollController;
    public SkeletonDeathHandler deathHandler;
    public SkeletonKnockbackReceiver knockbackReceiver;

    [Header("AI Behaviour")]
    [Tooltip("Time (seconds) the player must remain in range before the skeleton starts getting up.")]
    public float activationDelay = 0f;
    [Tooltip("Chase speed multiplier used once the skeleton is standing.")]
    public float normalChaseSpeed = 0.7f;

    [HideInInspector] public AIState currentState = AIState.Ragdoll;
    [HideInInspector] public Transform currentTarget;
    [HideInInspector] public bool isDead;

    [HideInInspector] public EnemyStats stats;
    [HideInInspector] public StatusEffects statusEffects;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public Animator animator;

    private float activationTimer;

    // ── Calculated Properties ─────────────────────────────────────────────────

    /// <summary>
    /// The skeleton's effective chase speed. Used by SkeletonAnimationController
    /// as the reference "1x" speed for the WalkSpeed animator parameter.
    /// </summary>
    public float WalkSpeed =>
        stats != null
            ? Mathf.Max(0f, (stats.EffectiveStamina / 10f) *
                movement.baseSprintSpeed *
                GetSpeedMultiplier() *
                normalChaseSpeed)
            : 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        statusEffects = GetComponent<StatusEffects>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        movement = GetComponent<SkeletonMovement>() ?? gameObject.AddComponent<SkeletonMovement>();
        targeting = GetComponent<SkeletonTargeting>() ?? gameObject.AddComponent<SkeletonTargeting>();
        combat = GetComponent<SkeletonCombat>() ?? gameObject.AddComponent<SkeletonCombat>();
        animationController = GetComponent<SkeletonAnimationController>() ?? gameObject.AddComponent<SkeletonAnimationController>();
        ragdollController = GetComponent<SkeletonRagdollController>() ?? gameObject.AddComponent<SkeletonRagdollController>();
        deathHandler = GetComponent<SkeletonDeathHandler>() ?? gameObject.AddComponent<SkeletonDeathHandler>();
        knockbackReceiver = GetComponent<SkeletonKnockbackReceiver>() ?? gameObject.AddComponent<SkeletonKnockbackReceiver>();

        movement.Initialize(this);
        targeting.Initialize(this);
        combat.Initialize(this);
        animationController.Initialize(this);
        ragdollController.Initialize(this);
        deathHandler.Initialize(this);
        knockbackReceiver.Initialize(this);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        currentState = AIState.Ragdoll;
    }

    private void Update()
    {
        if (isDead) return;

        if (stats.IsDead)
        {
            Die();
            return;
        }

        currentTarget = targeting.AcquireTarget();

        switch (currentState)
        {
            case AIState.Ragdoll: UpdateRagdoll(); break;
            case AIState.GettingUp: UpdateGettingUp(); break;
            case AIState.Chasing: UpdateChasing(); break;
        }

        animationController.Tick();
    }

    // ── State Handlers ────────────────────────────────────────────────────────

    private void UpdateRagdoll()
    {
        movement.SetDesiredVelocity(Vector3.zero);
        if (currentTarget == null)
        {
            activationTimer = 0f;
            Debug.Log($"[{name}] Ragdoll: No target found!");
            return;
        }

        activationTimer += Time.deltaTime;
        Debug.Log($"[{name}] Ragdoll: Target found, timer: {activationTimer:F2}/{activationDelay:F2}");
        if (activationTimer >= activationDelay)
            StartGetUp();
    }

    private void StartGetUp()
    {
        currentState = AIState.GettingUp;
        activationTimer = 0f;
        ragdollController.RecoverFromRagdoll();
        animationController.TriggerGetUp();
        Debug.Log($"[{name}] Starting get up animation");
    }

    private void UpdateGettingUp()
    {
        movement.SetDesiredVelocity(Vector3.zero);
        // Completion is handled by OnGetUpFinished(), called from SkeletonAnimationController
    }

    private void UpdateChasing()
    {
        if (currentTarget == null)
        {
            movement.SetDesiredVelocity(Vector3.zero);
            return;
        }

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > 0.05f)
        {
            movement.SetDesiredFacing(toTarget.normalized);
            movement.SetDesiredVelocity(toTarget.normalized * WalkSpeed);
        }
        else
        {
            movement.SetDesiredVelocity(Vector3.zero);
        }

        combat.TickAttack(currentTarget);
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    /// <summary>Called by SkeletonAnimationController when the get-up animation finishes.</summary>
    public void OnGetUpFinished()
    {
        if (currentState == AIState.GettingUp)
        {
            currentState = AIState.Chasing;
            Debug.Log($"[{name}] Get up finished, starting chase");
            // No SetSprinting needed — WalkSpeed feeds the velocity which drives the animator
        }
    }


    // ── Damage / Healing / Effects ────────────────────────────────────────────

    public void TakeDamage(float amount)
    {
        if (isDead || stats == null || amount <= 0f) return;
        stats.ModifyHealth(-amount);
        if (stats.IsDead) Die();
    }

    public void ApplyEffectCarrier(EffectCarrier carrier)
    {
        if (carrier == null || isDead) return;
        carrier.Apply(gameObject);
    }

    public void Heal(float amount)
    {
        if (isDead || stats == null || amount <= 0f) return;
        stats.ModifyHealth(amount);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        currentState = AIState.Dead;
        deathHandler.Die();
        enabled = false;
    }

    public void OnAnimatorIK(int layerIndex)
    {
        animationController.OnAnimatorIK(layerIndex);
    }

    private float GetSpeedMultiplier()
    {
        return statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;
    }
}
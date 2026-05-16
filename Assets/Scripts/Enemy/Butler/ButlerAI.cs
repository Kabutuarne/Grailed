using UnityEngine;

/// <summary>
/// Central hub for the Butler enemy. Coordinates sub-components and exposes shared state.
/// All sub-components communicate through this hub rather than directly referencing each other.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class ButlerAI : MonoBehaviour
{
    // ── Public State ──────────────────────────────────────────────────────────
    public enum AIState { Idle, Chasing, Dead }

    [Header("Sub-Components (auto-populated if left empty)")]
    public ButlerMovement movement;
    public ButlerTargeting targeting;
    public ButlerCombat combat;
    public ButlerAnimationController animationController;
    public ButlerAudioController audioController;
    public ButlerDeathHandler deathHandler;
    public EnemyPathing pathing;
    public BasicRoaming roaming;

    [Header("Hitboxes")]
    [Tooltip("ButlerLimbHitbox components on the hand/fist bones.")]
    public ButlerLimbHitbox[] limbHitboxes;

    // ── Shared State (read by sub-components) ─────────────────────────────────
    [HideInInspector] public AIState currentState = AIState.Idle;
    [HideInInspector] public Transform currentTarget;
    [HideInInspector] public bool isDead;
    [HideInInspector] public float desiredMoveSpeed;      // 0-1 blend for animations

    // ── Shared References ─────────────────────────────────────────────────────
    [HideInInspector] public EnemyStats stats;
    [HideInInspector] public StatusEffects statusEffects;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public Animator animator;

    // ── Calculated Properties (cached for sub-components) ─────────────────────
    public float WalkSpeed =>
        stats != null
            ? Mathf.Max(0f, (stats.EffectiveStamina / 10f) * movement.baseWalkSpeed * GetSpeedMultiplier())
            : 0f;

    public float SprintSpeed =>
        stats != null
            ? Mathf.Max(0f, (stats.EffectiveStamina / 10f) * movement.baseSprintSpeed * GetSpeedMultiplier())
            : 0f;

    public float AttackInterval =>
        Mathf.Max(0.1f, combat.baseAttackInterval /
            (stats != null ? Mathf.Max(0.1f, stats.EffectiveAgility / 10f) : 1f));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        statusEffects = GetComponent<StatusEffects>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        // Auto-create missing sub-components
        movement = GetComponent<ButlerMovement>() ?? gameObject.AddComponent<ButlerMovement>();
        targeting = GetComponent<ButlerTargeting>() ?? gameObject.AddComponent<ButlerTargeting>();
        combat = GetComponent<ButlerCombat>() ?? gameObject.AddComponent<ButlerCombat>();
        animationController = GetComponent<ButlerAnimationController>() ?? gameObject.AddComponent<ButlerAnimationController>();
        audioController = GetComponent<ButlerAudioController>() ?? gameObject.AddComponent<ButlerAudioController>();
        deathHandler = GetComponent<ButlerDeathHandler>() ?? gameObject.AddComponent<ButlerDeathHandler>();
        pathing = GetComponent<EnemyPathing>() ?? gameObject.AddComponent<EnemyPathing>();
        roaming = GetComponent<BasicRoaming>() ?? gameObject.AddComponent<BasicRoaming>();

        // Initialize all sub-components
        movement.Initialize(this);
        targeting.Initialize(this);
        combat.Initialize(this);
        animationController.Initialize(this);
        audioController.Initialize(this);
        deathHandler.Initialize(this);
        pathing.Initialize(this);
        roaming.Initialize(this);

        // Initialize limb hitboxes
        foreach (ButlerLimbHitbox hitbox in limbHitboxes)
            hitbox?.Initialize(combat);

        // Initialize knockback receiver and ensure proper Rigidbody settings
        ButlerKnockbackReceiver knockbackReceiver = GetComponent<ButlerKnockbackReceiver>()
            ?? gameObject.AddComponent<ButlerKnockbackReceiver>();
        knockbackReceiver.Initialize(this);
        rb.freezeRotation = true;
    }

    private void Update()
    {
        // Check for death
        if (!isDead && stats != null && stats.IsDead)
        {
            deathHandler.Die();
            return;
        }

        if (isDead) return;

        combat.TickCooldown();
        currentTarget = targeting.AcquireTarget();

        if (currentTarget == null)
        {
            currentState = AIState.Idle;
            TickRoaming();
        }
        else
        {
            currentState = AIState.Chasing;
            TickChasing();
        }

        animationController.Tick();
        audioController.TickMovementAudio();
    }

    // ── State Ticks ───────────────────────────────────────────────────────────

    private void TickRoaming()
    {
        // While jumping, the pathing coroutine owns movement — don't interfere.
        if (pathing.IsJumping)
            return;

        Vector3 roamVelocity = roaming.GetRoamVelocity();
        movement.SetDesiredVelocity(roamVelocity);

        if (roamVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 facing = roamVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                movement.SetDesiredFacing(facing.normalized);

            desiredMoveSpeed = 0.5f; // walk blend weight for animation
        }
        else
        {
            desiredMoveSpeed = 0f;
        }
    }

    private void TickChasing()
    {
        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distanceToTarget = toTarget.magnitude;

        // In attack range — stop and swing
        if (distanceToTarget <= combat.attackRange)
        {
            movement.SetDesiredVelocity(Vector3.zero);
            desiredMoveSpeed = 0f;
            combat.TryAttack();
            return;
        }

        // While jumping the pathing coroutine owns movement — don't interfere.
        if (pathing.IsJumping)
            return;

        Vector3 desiredVelocity = pathing.GetDesiredVelocityTowards(currentTarget.position, SprintSpeed);

        if (desiredVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 facing = desiredVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                movement.SetDesiredFacing(facing.normalized);

            movement.SetDesiredVelocity(desiredVelocity);
            desiredMoveSpeed = 1f;
        }
        else
        {
            // Fallback: path not yet available, go direct
            Vector3 fallback = toTarget.normalized * SprintSpeed;
            movement.SetDesiredFacing(toTarget.normalized);
            movement.SetDesiredVelocity(fallback);
            desiredMoveSpeed = 1f;
        }
    }

    // ── Public API (called by external systems) ───────────────────────────────

    public void TakeDamage(float amount)
    {
        if (isDead || stats == null || amount <= 0f) return;
        stats.ModifyHealth(-amount);
        if (stats.IsDead) deathHandler.Die();
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

    public void AlertTo(Transform newTarget)
    {
        if (isDead || newTarget == null) return;
        currentTarget = newTarget;
        audioController.OnAlert();
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
using UnityEngine;

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
    public EnemyPathing pathing;

    [Header("AI Behaviour")]
    public float activationDelay = 0f;
    public float normalChaseSpeed = 0.7f;

    [HideInInspector] public AIState currentState = AIState.Ragdoll;
    [HideInInspector] public Transform currentTarget;
    [HideInInspector] public bool isDead;

    [HideInInspector] public EnemyStats stats;
    [HideInInspector] public StatusEffects statusEffects;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public Animator animator;

    private float activationTimer;

    public float WalkSpeed =>
        stats != null
            ? Mathf.Max(0f, (stats.EffectiveStamina / 10f) *
                movement.baseSprintSpeed *
                GetSpeedMultiplier() *
                normalChaseSpeed)
            : 0f;

    public float AttackInterval =>
        Mathf.Max(0.1f, combat != null
            ? combat.baseAttackInterval / Mathf.Max(0.1f, stats != null ? stats.EffectiveAgility / 10f : 1f)
            : 1f);

    public float AttackSpeedMultiplier =>
        stats != null ? Mathf.Max(0.1f, stats.EffectiveAgility / 10f) : 1f;

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
        pathing = GetComponent<EnemyPathing>() ?? gameObject.AddComponent<EnemyPathing>();

        movement.Initialize(this);
        targeting.Initialize(this);
        combat.Initialize(this);
        animationController.Initialize(this);
        ragdollController.Initialize(this);
        deathHandler.Initialize(this);
        knockbackReceiver.Initialize(this);
        pathing.Initialize(this);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Start() => currentState = AIState.Ragdoll;

    private void Update()
    {
        if (isDead) return;

        if (stats.IsDead) { Die(); return; }

        currentTarget = targeting.AcquireTarget();

        switch (currentState)
        {
            case AIState.Ragdoll: UpdateRagdoll(); break;
            case AIState.GettingUp: UpdateGettingUp(); break;
            case AIState.Chasing: UpdateChasing(); break;
        }

        animationController.Tick();
    }

    private void UpdateRagdoll()
    {
        movement.SetDesiredVelocity(Vector3.zero);
        if (currentTarget == null) { activationTimer = 0f; return; }

        activationTimer += Time.deltaTime;
        if (activationTimer >= activationDelay)
            StartGetUp();
    }

    private void StartGetUp()
    {
        currentState = AIState.GettingUp;
        activationTimer = 0f;
        ragdollController.RecoverFromRagdoll();
        animationController.TriggerGetUp();
    }

    private void UpdateGettingUp()
    {
        movement.SetDesiredVelocity(Vector3.zero);
    }

    private void UpdateChasing()
    {
        if (currentTarget == null) { movement.SetDesiredVelocity(Vector3.zero); return; }

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > 0.05f)
        {
            Vector3 desiredVelocity = pathing.GetDesiredVelocityTowards(currentTarget.position, WalkSpeed);
            if (desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 desiredFacing = desiredVelocity;
                desiredFacing.y = 0f;
                if (desiredFacing.sqrMagnitude > 0.0001f)
                    movement.SetDesiredFacing(desiredFacing.normalized);
                movement.SetDesiredVelocity(desiredVelocity);
            }
            else
            {
                movement.SetDesiredFacing(toTarget.normalized);
                movement.SetDesiredVelocity(toTarget.normalized * WalkSpeed);
            }
        }
        else
        {
            movement.SetDesiredVelocity(Vector3.zero);
        }

        combat.TickAttack(currentTarget);
    }

    public void OnGetUpFinished()
    {
        if (currentState == AIState.GettingUp)
            currentState = AIState.Chasing;
    }

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

    public void OnAnimatorIK(int layerIndex) => animationController.OnAnimatorIK(layerIndex);

    private float GetSpeedMultiplier() =>
        statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;
}
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyStatusEffects))]
[DisallowMultipleComponent]
public class ButlerAI : MonoBehaviour
{
    public enum AIState { Roaming, Chasing, Dead }

    // ── Targeting ────────────────────────────────────────────────────────────
    [Header("Targeting")]
    public Transform explicitTarget;
    public string playerTag = "Player";
    public float hearingRadius = 10f;
    public float visionRadius = 16f;
    public float visionAngle = 60f;
    public float loseTargetRadius = 22f;
    public float attackRange = 1.9f;
    public LayerMask losIgnoreLayers;

    // ── Movement ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    public float chaseSpeedMultiplier = 1.8f;
    [Tooltip("Higher = snappier turning. 10-15 is natural, 20+ is instant.")]
    public float rotationSpeed = 12f;

    // ── Roaming ──────────────────────────────────────────────────────────────
    [Header("Roaming")]
    public float roamRadius = 10f;
    public float roamWaitTime = 2.5f;
    public Vector2 groanInterval = new Vector2(6f, 14f);

    // ── Combat ───────────────────────────────────────────────────────────────
    [Header("Combat")]
    public float meleeDamageMultiplier = 1f;
    public EffectCarrier onHitEffect;
    public GameObject hitSpawnPrefab;
    public Vector3 hitSpawnOffset = new Vector3(0f, 1f, 0.5f);

    // ── Death / Drops ────────────────────────────────────────────────────────
    [Header("Death")]
    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;
    public Collider aliveCollider;
    public GameObject[] dropPrefabs;
    [Range(0f, 1f)]
    public float dropChance = 0.4f;

    // ── Audio ────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioSource footstepSource;
    public AudioSource oneShotSource;
    public AudioClip groanClip;
    public AudioClip alertClip;
    public AudioClip attackHitClip;
    public AudioClip deathClip;
    public float footstepBasePitch = 1f;

    // ── Animation ────────────────────────────────────────────────────────────
    [Header("Animation")]
    public Animator animator;
    public string animWalkSpeed = "WalkSpeed";
    public string animAttackTrig = "Attack";
    public string animMirrorBool = "MirrorAttack";

    // ── Head IK ──────────────────────────────────────────────────────────────
    [Header("Head IK")]
    [Tooltip("Butler starts looking at the player when closer than this distance while chasing.")]
    public float headLookRange = 6f;
    [Tooltip("How quickly the head IK weight blends in and out.")]
    public float headLookSpeed = 3f;
    [Tooltip("How much the head turns toward the target (0-1).")]
    [Range(0f, 1f)]
    public float headWeight = 0.85f;
    [Tooltip("How much the body follows the head turn (0-1). Keep low so the body doesn't fight the animation.")]
    [Range(0f, 1f)]
    public float bodyWeight = 0.15f;

    private float currentLookWeight; // smoothly lerped each frame

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────
    private EnemyStats stats;
    private EnemyStatusEffects statusEffects;
    private Rigidbody rb;

    private AIState state = AIState.Roaming;
    private Transform target;
    private float nextAttackTime;
    private int attackCount;

    // These are written in Update and consumed in FixedUpdate
    // so physics and logic stay on their correct loops
    private Vector3 desiredVelocity;   // world-space XZ movement this frame
    private Vector3 desiredFacing;     // world-space direction to rotate toward

    // Roaming
    private Vector3 roamDestination;
    private bool waitingAtWaypoint;
    private float roamWaitTimer;
    private float nextGroanTime;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        statusEffects = GetComponent<EnemyStatusEffects>();
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Safety — make sure the rigidbody never tips over
        rb.freezeRotation = true;

        SetRagdoll(false);
        ScheduleGroan();
        PickRoamDestination();

        desiredFacing = transform.forward;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Update — pure logic, no physics writes
    // ─────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (state == AIState.Dead) return;
        if (stats.IsDead) { EnterDead(); return; }

        // Reset each frame; TickX will fill them if movement is needed
        desiredVelocity = Vector3.zero;

        switch (state)
        {
            case AIState.Roaming: TickRoaming(); break;
            case AIState.Chasing: TickChasing(); break;
        }

        UpdateAnimator();
        TickFootsteps();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FixedUpdate — all rigidbody writes happen here
    // ─────────────────────────────────────────────────────────────────────────
    private void FixedUpdate()
    {
        if (state == AIState.Dead) return;

        // Position
        if (desiredVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 next = rb.position + desiredVelocity * Time.fixedDeltaTime;
            // Preserve current Y so gravity/ground contact isn't disrupted
            next.y = rb.position.y;
            rb.MovePosition(next);
        }

        // Rotation — always runs so the butler keeps facing the target while attacking
        if (desiredFacing.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(desiredFacing, Vector3.up);
            Quaternion current = rb.rotation;
            // Use RotateTowards for a degrees-per-second cap — never flips
            float maxDeg = rotationSpeed * 45f * Time.fixedDeltaTime;
            rb.MoveRotation(Quaternion.RotateTowards(current, target, maxDeg));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Roaming
    // ─────────────────────────────────────────────────────────────────────────
    private void TickRoaming()
    {
        if (TryAcquireTarget())
        {
            PlayOneShot(alertClip);
            state = AIState.Chasing;
            return;
        }

        if (Time.time >= nextGroanTime)
        {
            PlayOneShot(groanClip);
            ScheduleGroan();
        }

        if (waitingAtWaypoint)
        {
            roamWaitTimer -= Time.deltaTime;
            if (roamWaitTimer <= 0f) { waitingAtWaypoint = false; PickRoamDestination(); }
            return;
        }

        Vector3 toDestination = roamDestination - transform.position;
        toDestination.y = 0f;

        if (toDestination.magnitude < 0.3f)
        {
            waitingAtWaypoint = true;
            roamWaitTimer = roamWaitTime;
            return;
        }

        Vector3 dir = toDestination.normalized;
        desiredVelocity = dir * stats.walkSpeed;
        desiredFacing = dir;
    }

    private void PickRoamDestination()
    {
        Vector2 rand = Random.insideUnitCircle * roamRadius;
        roamDestination = transform.position + new Vector3(rand.x, 0f, rand.y);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Chasing
    // ─────────────────────────────────────────────────────────────────────────
    private void TickChasing()
    {
        if (target == null || FlatDistance(transform.position, target.position) > loseTargetRadius)
        {
            target = null;
            state = AIState.Roaming;
            PickRoamDestination();
            ScheduleGroan();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // Always face the target — even while standing still attacking
        if (dist > 0.05f)
            desiredFacing = toTarget.normalized;

        if (dist > attackRange)
        {
            desiredVelocity = toTarget.normalized * stats.walkSpeed * chaseSpeedMultiplier;
        }
        else
        {
            // In attack range — stop moving, keep facing
            desiredVelocity = Vector3.zero;

            if (Time.time >= nextAttackTime)
                PerformAttack();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Animator
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (animator == null) return;

        float blend = 0f;

        if (state == AIState.Chasing && desiredVelocity.sqrMagnitude > 0.001f)
            blend = chaseSpeedMultiplier;
        else if (state == AIState.Roaming && desiredVelocity.sqrMagnitude > 0.001f)
            blend = 1f;

        // Smooth the blend so the transition in/out of walk looks natural
        float current = animator.GetFloat(animWalkSpeed);
        animator.SetFloat(animWalkSpeed, Mathf.MoveTowards(current, blend, Time.deltaTime * 4f));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Attack
    // ─────────────────────────────────────────────────────────────────────────
    private void PerformAttack()
    {
        nextAttackTime = Time.time + Mathf.Max(0.01f, stats.attackCooldown);

        bool mirror = (attackCount % 2) == 1;
        attackCount++;

        if (animator != null)
        {
            animator.SetBool(animMirrorBool, mirror);
            animator.SetTrigger(animAttackTrig);
        }

        if (target == null) return;

        PlayerStats ps = target.GetComponent<PlayerStats>();
        if (ps != null) ps.TakeDamage(stats.contactDamage * meleeDamageMultiplier);

        PlayOneShot(attackHitClip);

        if (onHitEffect != null) onHitEffect.Apply(target.gameObject);

        if (hitSpawnPrefab != null)
            Instantiate(hitSpawnPrefab,
                transform.position + transform.TransformDirection(hitSpawnOffset),
                transform.rotation);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Death
    // ─────────────────────────────────────────────────────────────────────────
    private void EnterDead()
    {
        if (state == AIState.Dead) return;
        state = AIState.Dead;

        desiredVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;

        if (footstepSource != null) footstepSource.Stop();
        if (animator != null) animator.enabled = false;
        if (aliveCollider != null) aliveCollider.enabled = false;

        PlayOneShot(deathClip);
        SetRagdoll(true);
        TrySpawnDrop();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Target acquisition
    // ─────────────────────────────────────────────────────────────────────────
    private bool TryAcquireTarget()
    {
        if (explicitTarget != null) { target = explicitTarget; return true; }

        PlayerStats[] all = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        foreach (PlayerStats ps in all)
        {
            if (ps == null) continue;
            if ((ps.transform.position - transform.position).sqrMagnitude < hearingRadius * hearingRadius)
            { target = ps.transform; return true; }
        }

        foreach (PlayerStats ps in all)
        {
            if (ps == null) continue;
            Vector3 toPlayer = ps.transform.position - transform.position;
            if (toPlayer.sqrMagnitude > visionRadius * visionRadius) continue;
            if (Vector3.Angle(transform.forward, toPlayer) > visionAngle) continue;
            if (!HasLOS(ps.transform.position)) continue;
            target = ps.transform;
            return true;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
        if (tagged != null && (tagged.transform.position - transform.position).sqrMagnitude <= visionRadius * visionRadius)
        { target = tagged.transform; return true; }

        return false;
    }

    private bool HasLOS(Vector3 worldPos)
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 dir = (worldPos + Vector3.up) - origin;
        LayerMask mask = ~losIgnoreLayers;
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude, mask, QueryTriggerInteraction.Ignore))
            if (!hit.collider.CompareTag(playerTag)) return false;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void TickFootsteps()
    {
        if (footstepSource == null) return;

        bool moving = desiredVelocity.sqrMagnitude > 0.01f;

        if (moving)
        {
            float speed = desiredVelocity.magnitude;
            footstepSource.pitch = footstepBasePitch * (speed / Mathf.Max(0.01f, stats.walkSpeed));
            if (!footstepSource.isPlaying) footstepSource.Play();
        }
        else if (footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (oneShotSource != null && clip != null)
            oneShotSource.PlayOneShot(clip);
    }

    private void ScheduleGroan()
    {
        nextGroanTime = Time.time + Random.Range(groanInterval.x, groanInterval.y);
    }

    private void SetRagdoll(bool on)
    {
        if (ragdollBodies != null)
            foreach (var r in ragdollBodies)
                if (r != null) { r.isKinematic = !on; r.detectCollisions = on; }

        if (ragdollColliders != null)
            foreach (var col in ragdollColliders)
                if (col != null) col.enabled = on;
    }

    private void TrySpawnDrop()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0 || Random.value > dropChance) return;
        GameObject drop = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
        if (drop != null)
            Instantiate(drop, transform.position + Vector3.up * 0.2f, Quaternion.identity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────
    public void AlertTo(Transform newTarget)
    {
        if (state == AIState.Dead) return;
        target = newTarget;
        PlayOneShot(alertClip);
        state = AIState.Chasing;
    }

    // Head IK — called by ButlerIKBridge on the ModelRoot child
    public void OnAnimatorIK_Forward(int layerIndex)
    {
        if (animator == null)
        {
            Debug.LogWarning("[ButlerAI] OnAnimatorIK fired but animator is null.");
            return;
        }

        // Find the look target — prefer the chasing target, fall back to any Player tag
        Transform lookTarget = null;
        if (state == AIState.Chasing && target != null)
        {
            float dist = FlatDistance(transform.position, target.position);
            if (dist <= headLookRange)
                lookTarget = target;
        }

        float desiredWeight = lookTarget != null ? 1f : 0f;
        currentLookWeight = Mathf.MoveTowards(
            currentLookWeight, desiredWeight, Time.deltaTime * headLookSpeed);

        if (currentLookWeight <= 0.001f)
        {
            animator.SetLookAtWeight(0f);
            return;
        }

        // Aim at eye level of the look target
        Vector3 lookPoint = lookTarget.position + Vector3.up * 1.6f;
        animator.SetLookAtPosition(lookPoint);
        animator.SetLookAtWeight(
            currentLookWeight,  // master weight
            bodyWeight,         // body
            headWeight,         // head
            0f,                 // eyes
            0.5f                // clamp — stops neck snapping past ~90 deg
        );

        Debug.DrawLine(transform.position + Vector3.up * 1.6f, lookPoint, Color.cyan);
    }
}

// IK appended
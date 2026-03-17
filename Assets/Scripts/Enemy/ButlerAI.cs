using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class ButlerAI : MonoBehaviour
{
    public enum AIState
    {
        Idle,
        Chasing,
        Dead
    }

    [Header("Health & Resources")]
    [Tooltip("Base health before strength multiplier")]
    public float baseMaxHealth = 100f;
    [Tooltip("Health regeneration per second before strength multiplier")]
    public float baseHealthRegen = 0.5f;

    [Header("Targeting")]
    public string playerTag = "Player";
    public float detectionRadius = 15f;
    public float loseTargetRadius = 25f;
    [Tooltip("Optional explicit target that overrides automatic acquisition")]
    public Transform explicitTarget;

    [Header("Movement")]
    [Tooltip("Base walk speed before stamina multiplier")]
    public float baseWalkSpeed = 2.5f;
    [Tooltip("Base sprint speed before stamina multiplier")]
    public float baseSprintSpeed = 4f;
    [Tooltip("Higher = snappier turning. 10-15 is natural, 20+ is very fast.")]
    public float rotationSpeed = 12f;
    [Tooltip("Pitch multiplier base for movement audio")]
    public float movementAudioBasePitch = 1f;

    [Header("Combat")]
    [Tooltip("Distance to start attacking")]
    public float attackRange = 2f;
    [Tooltip("Base time between attacks before agility scaling")]
    public float baseAttackInterval = 1.5f;
    [Tooltip("Effects applied on successful attack (NO damage multipliers)")]
    public EffectCarrier[] attackEffects;
    public GameObject attackVFX;
    public Vector3 attackVFXOffset = new Vector3(0f, 1f, 0.5f);

    [Header("Audio Sources")]
    [Tooltip("Audio source for spotted/alert sound")]
    public AudioSource alertAudioSource;
    [Tooltip("Audio source for attack sounds")]
    public AudioSource attackAudioSource;
    [Tooltip("Audio source for movement sounds")]
    public AudioSource movementAudioSource;
    [Tooltip("Audio source for death sound")]
    public AudioSource deathAudioSource;

    [Header("Audio Clips")]
    public AudioClip alertSound;
    public AudioClip attackSound;
    public AudioClip deathSound;

    [Header("Animation")]
    public Animator animator;
    public string animWalkSpeed = "WalkSpeed";
    public string animAttackTrig = "Attack";
    public string animMirrorBool = "MirrorAttack";
    [Tooltip("How fast the animation speed blend responds")]
    public float animationBlendSpeed = 4f;

    [Header("IK Settings")]
    [Tooltip("Enable head look-at IK to track target")]
    public bool enableLookIK = true;
    [Tooltip("Butler starts looking at the player when closer than this distance while chasing")]
    public float headLookRange = 6f;
    [Tooltip("How quickly the head IK weight blends in and out")]
    public float headLookYOffset = 1.7f;
    [Tooltip("To adjust the height of the look target so it looks right into the camera")]
    public float headLookSpeed = 3f;
    [Tooltip("Master weight of head look-at")]
    [Range(0f, 1f)] public float lookIKWeight = 1f;
    [Tooltip("How much the head turns toward the target")]
    [Range(0f, 1f)] public float headWeight = 0.85f;
    [Tooltip("How much the body follows the head turn")]
    [Range(0f, 1f)] public float bodyWeight = 0.15f;

    [Header("Ragdoll / Death")]
    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;
    [Tooltip("Main collider used while alive")]
    public GameObject aliveBody;
    public float destroyDelay = 8f;

    private EnemyStats stats;
    private EnemyStatusEffects statusEffects;
    private Rigidbody rb;
    private Transform target;

    private AIState state = AIState.Idle;
    private float attackTimer;
    private bool isDead;
    private bool hasAlerted;
    private int attackCount;
    private float currentLookWeight;

    // Written in Update, applied in FixedUpdate
    private Vector3 desiredVelocity;
    private Vector3 desiredFacing;

    public bool IsDead => isDead;

    // Calculated properties based on stats
    public float WalkSpeed =>
        stats != null
            ? Mathf.Max(0f, (stats.EffectiveStamina / 10f) * baseWalkSpeed * GetSpeedMultiplier())
            : 0f;

    public float SprintSpeed =>
        stats != null
            ? Mathf.Max(0f, (stats.EffectiveStamina / 10f) * baseSprintSpeed * GetSpeedMultiplier())
            : 0f;

    public float AttackSpeed =>
        stats != null
            ? Mathf.Max(0.1f, (stats.EffectiveAgility / 10f))
            : 1f;

    public float AttackInterval => Mathf.Max(0.1f, baseAttackInterval / AttackSpeed);
    public float Health01 => stats != null ? stats.Health01 : 0f;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        statusEffects = GetComponent<EnemyStatusEffects>();
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        attackTimer = 0f;
        desiredFacing = transform.forward;

        SetupAudioSources();

        rb.freezeRotation = true;
        SetRagdoll(false);
    }

    private void Update()
    {
        if (isDead)
            return;

        if (stats != null && stats.health <= 0f)
        {
            Die();
            return;
        }

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        desiredVelocity = Vector3.zero;

        AcquireTarget();

        if (target == null)
        {
            TickIdle();
        }
        else
        {
            TickChase();
        }

        UpdateAnimator();
        TickMovementAudio();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        if (desiredVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 next = rb.position + desiredVelocity * Time.fixedDeltaTime;
            next.y = rb.position.y;
            rb.MovePosition(next);
        }

        if (desiredFacing.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredFacing, Vector3.up);
            float maxDegrees = rotationSpeed * 45f * Time.fixedDeltaTime;
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, maxDegrees));
        }
    }

    private void TickIdle()
    {
        state = AIState.Idle;
        hasAlerted = false;
        desiredVelocity = Vector3.zero;
    }

    private void TickChase()
    {
        state = AIState.Chasing;

        if (!hasAlerted)
        {
            PlayOneShot(alertAudioSource, alertSound);
            hasAlerted = true;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget > 0.05f)
            desiredFacing = toTarget.normalized;

        if (distanceToTarget > attackRange)
        {
            desiredVelocity = toTarget.normalized * SprintSpeed;
        }
        else
        {
            desiredVelocity = Vector3.zero;
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (attackTimer > 0f)
            return;

        attackTimer = AttackInterval;
        PerformAttack();
    }

    private void PerformAttack()
    {
        bool mirror = (attackCount % 2) == 1;
        attackCount++;

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(animMirrorBool))
                animator.SetBool(animMirrorBool, mirror);

            if (!string.IsNullOrEmpty(animAttackTrig))
                animator.SetTrigger(animAttackTrig);
        }

        PlayOneShot(attackAudioSource, attackSound);

        if (attackVFX != null)
        {
            Vector3 spawnPos = transform.position + transform.TransformDirection(attackVFXOffset);
            Instantiate(attackVFX, spawnPos, transform.rotation);
        }

        if (target != null && attackEffects != null)
        {
            foreach (EffectCarrier carrier in attackEffects)
            {
                if (carrier != null)
                    carrier.Apply(target.gameObject);
            }
        }
    }

    private void AcquireTarget()
    {
        if (explicitTarget != null)
        {
            target = explicitTarget;
            return;
        }

        if (target != null)
        {
            if (FlatDistance(transform.position, target.position) <= loseTargetRadius)
                return;

            target = null;
        }

        target = FindClosestPlayer();
    }

    private Transform FindClosestPlayer()
    {
        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        float bestSqrDistance = detectionRadius * detectionRadius;
        Transform bestTarget = null;

        foreach (PlayerStats player in players)
        {
            if (player == null)
                continue;

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            bestTarget = player.transform;
        }

        if (bestTarget != null || string.IsNullOrEmpty(playerTag))
            return bestTarget;

        GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
        if (tagged == null)
            return null;

        float taggedDistance = (tagged.transform.position - transform.position).sqrMagnitude;
        return taggedDistance <= bestSqrDistance ? tagged.transform : null;
    }

    private void UpdateAnimator()
    {
        if (animator == null || string.IsNullOrEmpty(animWalkSpeed))
            return;

        float blend = 0f;

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float speed = desiredVelocity.magnitude;
            float divisor = Mathf.Max(0.01f, WalkSpeed);
            blend = speed / divisor;
        }

        float current = animator.GetFloat(animWalkSpeed);
        float next = Mathf.MoveTowards(current, blend, Time.deltaTime * animationBlendSpeed);
        animator.SetFloat(animWalkSpeed, next);
    }

    private void TickMovementAudio()
    {
        if (movementAudioSource == null)
            return;

        bool moving = desiredVelocity.sqrMagnitude > 0.01f;

        if (moving)
        {
            float maxReferenceSpeed = Mathf.Max(0.01f, SprintSpeed);
            float normalized = desiredVelocity.magnitude / maxReferenceSpeed;
            movementAudioSource.pitch = movementAudioBasePitch * (0.85f + 0.3f * normalized);

            if (!movementAudioSource.isPlaying)
                movementAudioSource.Play();
        }
        else if (movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f || stats == null)
            return;

        stats.health = Mathf.Clamp(stats.health - amount, 0f, stats.MaxHealth);

        if (stats.health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f || stats == null)
            return;

        stats.Heal(amount);
    }

    public void AlertTo(Transform newTarget)
    {
        if (isDead || newTarget == null)
            return;

        target = newTarget;
        hasAlerted = false;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        state = AIState.Dead;
        desiredVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;

        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
            statusEffects.enabled = false;
        }

        if (movementAudioSource != null)
            movementAudioSource.Stop();

        if (animator != null)
            animator.enabled = false;

        PlayDetachedDeathSound();

        SetRagdoll(true);

        if (stats != null)
            stats.SpawnDeathDrops();

        //After enabling ragdoll, disables mostly everything leaving an empty entity
        if (aliveBody != null)
        {
            aliveBody.GetComponent<Collider>().enabled = false;
            Destroy(aliveBody.GetComponent<EnemyStats>());
            Destroy(aliveBody.GetComponent<ButlerAI>()); //must be removed in order to get rid of RB, which causes physics problems
            Destroy(aliveBody.GetComponent<Rigidbody>());
        }
    }

    private void SetupAudioSources()
    {
        if (alertAudioSource == null)
        {
            alertAudioSource = gameObject.AddComponent<AudioSource>();
            alertAudioSource.playOnAwake = false;
        }

        if (attackAudioSource == null)
        {
            attackAudioSource = gameObject.AddComponent<AudioSource>();
            attackAudioSource.playOnAwake = false;
        }

        if (movementAudioSource == null)
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
            movementAudioSource.playOnAwake = false;
            movementAudioSource.loop = true;
        }

        if (deathAudioSource == null)
        {
            deathAudioSource = gameObject.AddComponent<AudioSource>();
            deathAudioSource.playOnAwake = false;
        }
    }

    private void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip);
    }

    private void PlayDetachedDeathSound()
    {
        if (deathSound == null)
            return;

        GameObject soundObject = new GameObject("ButlerDeathSound");
        soundObject.transform.position = transform.position;

        AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
        tempAudio.playOnAwake = false;
        tempAudio.clip = deathSound;
        tempAudio.Play();

        Destroy(soundObject, deathSound.length + 0.1f);
    }

    private void SetRagdoll(bool on)
    {
        if (ragdollBodies != null)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                    continue;

                body.isKinematic = !on;
                body.detectCollisions = on;
            }
        }

        if (ragdollColliders != null)
        {
            foreach (Collider col in ragdollColliders)
            {
                if (col != null)
                    col.enabled = on;
            }
        }

        if (!on)
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }
    }

    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private float GetSpeedMultiplier()
    {
        return statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;
    }

    /// <summary>
    /// Called by ButlerIKBridge to handle IK positioning.
    /// </summary>
    public void OnAnimatorIK(int layerIndex)
    {
        if (isDead || animator == null)
            return;

        Transform lookTarget = null;

        if (enableLookIK && state == AIState.Chasing && target != null)
        {
            float dist = FlatDistance(transform.position, target.position);
            if (dist <= headLookRange)
                lookTarget = target;
        }

        float desiredWeight = lookTarget != null ? lookIKWeight : 0f;
        currentLookWeight = Mathf.MoveTowards(currentLookWeight, desiredWeight, Time.deltaTime * headLookSpeed);

        if (currentLookWeight > 0.001f && lookTarget != null)
        {
            Vector3 lookPoint = lookTarget.position + Vector3.up * headLookYOffset;
            animator.SetLookAtPosition(lookPoint);
            animator.SetLookAtWeight(
                currentLookWeight,
                bodyWeight,
                headWeight,
                0f,
                0.5f
            );
        }
        else
        {
            animator.SetLookAtWeight(0f);
        }
    }
}
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[DisallowMultipleComponent]
public class EyeballAI : MonoBehaviour
{
    [Header("Health & Resources")]
    [Tooltip("Base health before strength multiplier")]
    public float baseMaxHealth = 100f;
    [Tooltip("Health regeneration per second before strength multiplier")]
    public float baseHealthRegen = 0.5f;

    [Header("Targeting")]
    public string playerTag = "Player";
    public float detectionRadius = 22f;
    public float loseTargetRadius = 30f;

    [Header("Weeping Angel Behavior")]
    public bool freezeWhenSeen = true;
    public float seenDotThreshold = 0.6f;
    public LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    public float rotationSpeed = 12f;
    public float hoverHeight = 2f;
    public float hoverCorrectionSpeed = 4f;
    public float hoverMaxVerticalSpeed = 3f;
    [Tooltip("Base movement speed multiplier before stamina scaling")]
    public float baseChargeSpeedMultiplier = 1.1f;
    public float maxChargeSpeedMultiplier = 2.8f;
    public float accelerationPerSecond = 0.75f;

    [Header("Ground Reference")]
    public LayerMask groundMask = ~0;
    public float groundProbeHeight = 3f;
    public float groundProbeDistance = 12f;

    [Header("Explosion Attack")]
    [Tooltip("Distance required to start the explosion attack")]
    public float explodeRange = 1.5f;
    [Tooltip("Delay between starting the attack and applying explosion effects")]
    public float attackDelay = 0f;
    public KnockbackImpactBehaviour knockbackBehaviour;
    public EffectCarrier[] onExplodeEffects;
    public GameObject explosionVFX;

    [Header("Audio Sources")]
    [Tooltip("Audio source for spotted sound (one-shot)")]
    public AudioSource spottedAudioSource;
    [Tooltip("Audio source for approach loop")]
    public AudioSource approachAudioSource;
    [Tooltip("Audio source for death sound")]
    public AudioSource deathAudioSource;

    [Header("Audio Clips")]
    public AudioClip spottedSound;
    public AudioClip approachLoopSound;
    public AudioClip deathSound;

    [Header("Knockback Recovery")]
    public float knockbackRecoveryDelay = 0.6f;

    [Header("Death Settings")]
    public bool destroyOnDeath = true;
    public float destroyDelay = 0f;

    private EnemyStats stats;
    private StatusEffects statusEffects;
    private Rigidbody rb;
    private Transform target;
    private Collider[] ownColliders;

    private float currentSpeedMultiplier;
    private float knockbackRecoveryTimer;
    private float pendingExplosionTime = -1f;
    private bool hasPlayedSpottedSound;
    private bool isApproaching;
    private bool isSeen;
    private bool isExploding;
    private bool isDead;

    public bool IsDead => isDead;

    // Calculated properties based on stats
    public float Health01 => stats != null ? stats.Health01 : 0f;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        statusEffects = GetComponent<StatusEffects>();
        rb = GetComponent<Rigidbody>();
        ownColliders = GetComponentsInChildren<Collider>().Append(GetComponent<Collider>()).Where(c => c != null).ToArray();

        currentSpeedMultiplier = baseChargeSpeedMultiplier;

        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        SetupAudioSources();
    }

    private void Update()
    {
        if (isDead)
            return;

        // Check for death
        if (stats != null && stats.health <= 0f)
        {
            Die();
            return;
        }

        AcquireTarget();
        isSeen = freezeWhenSeen && target != null && IsSeenByTarget();

        if (knockbackRecoveryTimer > 0f)
            knockbackRecoveryTimer -= Time.deltaTime;

        if (pendingExplosionTime >= 0f && Time.time >= pendingExplosionTime)
        {
            pendingExplosionTime = -1f;
            ApplyExplosion();
        }

        HandleAudio();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        if (isSeen)
        {
            FreezeInPlace();
            return;
        }

        MaintainHover();

        if (target == null || isExploding)
            return;

        FaceTarget(target.position);
        SetApproaching(true);

        currentSpeedMultiplier = Mathf.MoveTowards(
            currentSpeedMultiplier,
            maxChargeSpeedMultiplier,
            accelerationPerSecond * Time.fixedDeltaTime);

        MoveTowardsTarget();

        if (Vector3.Distance(transform.position, target.position) <= explodeRange)
            StartExplosionAttack();
    }

    private void StartExplosionAttack()
    {
        if (isExploding || isDead)
            return;

        isExploding = true;
        SetApproaching(false);
        rb.linearVelocity = Vector3.zero;
        pendingExplosionTime = Time.time + Mathf.Max(0f, attackDelay);
    }

    private void ApplyExplosion()
    {
        if (isDead)
            return;

        // Apply knockback
        if (knockbackBehaviour != null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, knockbackBehaviour.radius);
            knockbackBehaviour.Apply(gameObject, transform.position, hits, knockbackBehaviour.radius);
        }

        // Apply effects to target (NO damage multipliers, only EffectCarrier)
        if (target != null && onExplodeEffects != null)
        {
            foreach (EffectCarrier carrier in onExplodeEffects)
            {
                if (carrier != null)
                    carrier.Apply(target.gameObject);
            }
        }

        PlayDeathEffects();
        Die();
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

    public void Kill()
    {
        if (!isDead)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
            statusEffects.enabled = false;
        }

        // Spawn death drops
        if (stats != null)
            stats.SpawnDeathDrops();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private void FreezeInPlace()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentSpeedMultiplier = baseChargeSpeedMultiplier;
        SetApproaching(false);

        if (target != null)
            FaceTarget(target.position);
    }

    private void MaintainHover()
    {
        if (rb == null)
            return;

        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                groundProbeHeight + groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
            return;
        }

        if (knockbackRecoveryTimer > 0f)
        {
            if (rb.linearVelocity.y > 0f)
            {
                Vector3 recoveryVelocity = rb.linearVelocity;
                rb.linearVelocity = new Vector3(recoveryVelocity.x, 0f, recoveryVelocity.z);
            }
            return;
        }

        float targetY = hit.point.y + hoverHeight;
        float error = targetY - transform.position.y;
        float desiredVerticalVelocity = Mathf.Clamp(error * hoverCorrectionSpeed, -hoverMaxVerticalSpeed, hoverMaxVerticalSpeed);

        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(currentVelocity.x, desiredVerticalVelocity, currentVelocity.z);
    }

    private void MoveTowardsTarget()
    {
        Vector3 targetPosition = target.position;
        targetPosition.y = transform.position.y;
        Vector3 moveDirection = (targetPosition - transform.position).normalized;

        float baseSpeed = (stats.EffectiveStamina / 10f) * baseChargeSpeedMultiplier;
        float moveSpeed = baseSpeed * currentSpeedMultiplier * GetSpeedMultiplier();
        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, currentVelocity.y, moveDirection.z * moveSpeed);
    }

    public void NotifyKnockback()
    {
        knockbackRecoveryTimer = knockbackRecoveryDelay;
        SetApproaching(false);
    }

    private void SetupAudioSources()
    {
        // Create AudioSource components if not assigned
        AudioSource[] sources = GetComponents<AudioSource>();

        if (spottedAudioSource == null)
        {
            spottedAudioSource = gameObject.AddComponent<AudioSource>();
            spottedAudioSource.playOnAwake = false;
        }

        if (approachAudioSource == null)
        {
            approachAudioSource = gameObject.AddComponent<AudioSource>();
            approachAudioSource.playOnAwake = false;
        }

        if (deathAudioSource == null)
        {
            deathAudioSource = gameObject.AddComponent<AudioSource>();
            deathAudioSource.playOnAwake = false;
        }
    }

    private void HandleAudio()
    {
        if (isSeen)
        {
            if (!hasPlayedSpottedSound && spottedSound != null && spottedAudioSource != null)
            {
                spottedAudioSource.PlayOneShot(spottedSound);
                hasPlayedSpottedSound = true;
            }
            return;
        }

        hasPlayedSpottedSound = false;
    }

    private void SetApproaching(bool approaching)
    {
        if (approaching == isApproaching)
            return;

        isApproaching = approaching;

        if (approachAudioSource == null)
            return;

        if (approaching)
        {
            if (approachLoopSound != null)
            {
                approachAudioSource.clip = approachLoopSound;
                approachAudioSource.loop = true;
                approachAudioSource.Play();
            }
            return;
        }

        approachAudioSource.Stop();
    }

    private void AcquireTarget()
    {
        if (target != null && Vector3.Distance(transform.position, target.position) <= loseTargetRadius)
            return;

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

    private void FaceTarget(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private bool IsSeenByTarget()
    {
        if (target == null)
            return false;

        Camera playerCamera = target.GetComponentInChildren<Camera>();
        Transform observer = playerCamera != null ? playerCamera.transform : target;

        Vector3 toEyeball = transform.position - observer.position;
        float distance = toEyeball.magnitude;
        if (distance <= 0.001f)
            return true;

        Vector3 direction = toEyeball / distance;
        if (Vector3.Dot(observer.forward, direction) < seenDotThreshold)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(observer.position, direction, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        Collider[] targetColliders = target.GetComponentsInChildren<Collider>();

        foreach (RaycastHit hit in hits)
        {
            if (IsColliderInSet(hit.collider, targetColliders) || IsColliderInSet(hit.collider, ownColliders))
                continue;

            return false;
        }

        return true;
    }

    private static bool IsColliderInSet(Collider colliderToFind, Collider[] colliders)
    {
        foreach (Collider collider in colliders)
        {
            if (collider == colliderToFind)
                return true;
        }

        return false;
    }

    private void PlayDeathEffects()
    {
        if (explosionVFX != null)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        if (deathSound != null && deathAudioSource != null)
        {
            GameObject soundObject = new GameObject("EyeballDeathSound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = deathSound;
            tempAudio.Play();
            Destroy(soundObject, deathSound.length + 0.1f);
        }

        foreach (Renderer rendererRef in GetComponentsInChildren<Renderer>())
            rendererRef.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead || isExploding)
            return;

        if (target != null && other.transform == target)
            StartExplosionAttack();
    }

    private float GetSpeedMultiplier()
    {
        return statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f;
    }

    // Public methods for external systems
    public void Explode() => StartExplosionAttack();
    public void OnKilledExternally() => StartExplosionAttack();
}
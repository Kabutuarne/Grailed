using System.Linq;
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class EyeballAI : MonoBehaviour
{
    [Header("Targeting")]
    public string playerTag = "Player";
    public float detectionRadius = 22f;
    public float loseTargetRadius = 30f;

    [Header("Weeping Angel")]
    public bool freezeWhenSeen = true;
    public float seenDotThreshold = 0.6f;
    public LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    public float rotationSpeed = 12f;
    public float hoverHeight = 2f;
    public float hoverCorrectionSpeed = 4f;
    public float hoverMaxVerticalSpeed = 3f;
    public float baseChargeSpeedMultiplier = 1.1f;
    public float maxChargeSpeedMultiplier = 2.8f;
    public float accelerationPerSecond = 0.75f;

    [Header("Ground Reference")]
    public LayerMask groundMask = ~0;
    public float groundProbeHeight = 3f;
    public float groundProbeDistance = 12f;

    [Header("Explosion")]
    public float explodeRange = 1.5f;
    public float explosionDamageMultiplier = 1.5f;
    public KnockbackImpactBehaviour knockbackBehaviour;
    public EffectCarrier[] onExplodeEffects;
    public GameObject explosionVFX;

    [Header("Audio")]
    public AudioClip spottedSound;
    public AudioClip approachLoopSound;
    public AudioClip deathSound;

    [Header("Knockback Recovery")]
    public float knockbackRecoveryDelay = 0.6f;

    private EnemyStats stats;
    private Rigidbody rb;
    private AudioSource audioSource;
    private Transform target;
    private float currentSpeedMultiplier;
    private bool hasPlayedSpottedSound;
    private bool isApproaching;
    private float knockbackRecoveryTimer;
    private bool isSeen;

    // Cached colliders on this enemy to exclude from LOS raycast
    private Collider[] ownColliders;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        ownColliders = GetComponentsInChildren<Collider>().Append(GetComponent<Collider>()).ToArray();

        currentSpeedMultiplier = baseChargeSpeedMultiplier;

        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (audioSource != null)
        {
            audioSource.loop = false;
            audioSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        if (stats.IsDead) return;

        AcquireTarget();
        isSeen = freezeWhenSeen && target != null && IsSeenByTarget();
        HandleAudio();

        if (knockbackRecoveryTimer > 0f)
            knockbackRecoveryTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (stats.IsDead) return;

        if (isSeen)
        {
            // Fully kill all velocity while frozen — prevents any accumulated vertical drift
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            currentSpeedMultiplier = baseChargeSpeedMultiplier;
            SetApproaching(false);

            // Still face the target while frozen
            if (target != null)
                FaceTarget(target.position);

            return;
        }

        MaintainHover();

        if (target == null) return;

        FaceTarget(target.position);
        SetApproaching(true);

        currentSpeedMultiplier = Mathf.MoveTowards(
            currentSpeedMultiplier,
            maxChargeSpeedMultiplier,
            accelerationPerSecond * Time.fixedDeltaTime
        );

        MoveTowardsTarget();

        if (Vector3.Distance(transform.position, target.position) <= explodeRange)
            Explode();
    }

    private void MaintainHover()
    {
        if (rb == null) return;

        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
        float targetY;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                groundProbeHeight + groundProbeDistance, groundMask,
                QueryTriggerInteraction.Ignore))
        {
            targetY = hit.point.y + hoverHeight;
        }
        else
        {
            Vector3 vel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
            return;
        }

        if (knockbackRecoveryTimer > 0f)
        {
            // Only suppress upward velocity during recovery
            if (rb.linearVelocity.y > 0f)
            {
                Vector3 v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(v.x, 0f, v.z);
            }
            return;
        }

        float error = targetY - transform.position.y;
        float desiredVerticalVelocity = Mathf.Clamp(
            error * hoverCorrectionSpeed,
            -hoverMaxVerticalSpeed,
            hoverMaxVerticalSpeed
        );

        Vector3 vel2 = rb.linearVelocity;
        rb.linearVelocity = new Vector3(vel2.x, desiredVerticalVelocity, vel2.z);
    }

    private void MoveTowardsTarget()
    {
        Vector3 targetPosition = target.position;
        targetPosition.y = transform.position.y;
        Vector3 moveDir = (targetPosition - transform.position).normalized;

        float moveSpeed = stats.sprintSpeed * currentSpeedMultiplier;
        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(moveDir.x * moveSpeed, currentVelocity.y, moveDir.z * moveSpeed);
    }

    public void NotifyKnockback()
    {
        knockbackRecoveryTimer = knockbackRecoveryDelay;
        SetApproaching(false);
    }

    private void HandleAudio()
    {
        if (isSeen)
        {
            if (!hasPlayedSpottedSound && spottedSound != null)
            {
                audioSource.PlayOneShot(spottedSound);
                hasPlayedSpottedSound = true;
            }
        }
        else
        {
            hasPlayedSpottedSound = false;
        }
    }

    private void SetApproaching(bool approaching)
    {
        if (approaching == isApproaching) return;
        isApproaching = approaching;

        if (approaching)
        {
            if (approachLoopSound != null)
            {
                audioSource.clip = approachLoopSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            audioSource.Stop();
        }
    }

    private void AcquireTarget()
    {
        if (target != null)
        {
            if (Vector3.Distance(transform.position, target.position) <= loseTargetRadius)
                return;
            target = null;
        }

        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        float bestSqr = detectionRadius * detectionRadius;
        Transform best = null;

        foreach (PlayerStats player in players)
        {
            if (player == null) continue;
            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = player.transform; }
        }

        if (best == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null)
            {
                float sqr = (tagged.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr) best = tagged.transform;
            }
        }

        target = best;
    }

    private void FaceTarget(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        if (direction.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private bool IsSeenByTarget()
    {
        if (target == null) return false;

        Camera playerCamera = target.GetComponentInChildren<Camera>();
        Transform observer = playerCamera != null ? playerCamera.transform : target;

        Vector3 toEyeball = transform.position - observer.position;
        float distance = toEyeball.magnitude;
        if (distance <= 0.001f) return true;

        Vector3 direction = toEyeball / distance;
        if (Vector3.Dot(observer.forward, direction) < seenDotThreshold) return false;

        // Use QueryHitColliders to skip the player's own colliders and our own,
        // so being point-blank doesn't cause the raycast to hit the player and return false
        RaycastHit[] hits = Physics.RaycastAll(
            observer.position, direction, distance,
            lineOfSightMask, QueryTriggerInteraction.Ignore
        );

        Collider[] targetColliders = target.GetComponentsInChildren<Collider>();

        foreach (RaycastHit hit in hits)
        {
            // Skip the player's own colliders
            bool isTargetCollider = false;
            foreach (Collider c in targetColliders)
            {
                if (hit.collider == c) { isTargetCollider = true; break; }
            }
            if (isTargetCollider) continue;

            // Skip our own colliders
            bool isOwnCollider = false;
            foreach (Collider c in ownColliders)
            {
                if (hit.collider == c) { isOwnCollider = true; break; }
            }
            if (isOwnCollider) continue;

            // A solid object is between observer and eyeball — not seen
            return false;
        }

        return true;
    }

    public void Explode()
    {
        if (stats.IsDead) return;

        if (target != null)
        {
            PlayerStats playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null)
                playerStats.TakeDamage(stats.contactDamage * explosionDamageMultiplier);
        }

        if (knockbackBehaviour != null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, knockbackBehaviour.radius);
            knockbackBehaviour.Apply(gameObject, transform.position, hits, knockbackBehaviour.radius);
        }

        if (onExplodeEffects != null)
            foreach (EffectCarrier carrier in onExplodeEffects)
                if (carrier != null && target != null)
                    carrier.Apply(target.gameObject);

        PlayDeathEffects();
        stats.Kill();
    }

    private void PlayDeathEffects()
    {
        if (explosionVFX != null)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        if (deathSound != null)
        {
            GameObject soundObject = new GameObject("EyeballDeathSound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = deathSound;
            tempAudio.Play();
            Destroy(soundObject, deathSound.length + 0.1f);
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stats.IsDead) return;
        if (target != null && other.transform == target)
            Explode();
    }

    public void OnKilledExternally() => Explode();
}
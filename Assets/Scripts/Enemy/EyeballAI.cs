using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyStatusEffects))]
[DisallowMultipleComponent]
public class EyeballAI : MonoBehaviour
{
    [Header("Targeting")]
    public Transform explicitTarget;
    public string playerTag = "Player";
    public float detectionRadius = 22f;
    public float loseTargetRadius = 30f;

    [Header("Weeping angel")]
    public bool freezeWhenSeen = true;
    public float seenDotThreshold = 0.6f;
    public LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    public float rotationSpeed = 12f;
    public float hoverHeight = 2f;
    public float hoverLerpSpeed = 6f;
    public float baseChargeSpeedMultiplier = 1.1f;
    public float maxChargeSpeedMultiplier = 2.8f;
    public float accelerationPerSecond = 0.75f;

    [Header("Explosion")]
    public float explodeRange = 1.5f;
    public float explosionDamageMultiplier = 1.5f;
    public EffectCarrier[] onHitEffects;
    public EffectCarrier[] onSelfDestructEffects;

    [Header("Ground reference")]
    public LayerMask groundMask = ~0;
    public float groundProbeHeight = 3f;
    public float groundProbeDistance = 12f;

    private EnemyStats stats;
    private Rigidbody rb;
    private Transform target;
    private float currentSpeedMultiplier;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody>();
        currentSpeedMultiplier = baseChargeSpeedMultiplier;
        if (rb != null)
            rb.useGravity = false;
    }

    private void Update()
    {
        if (stats.IsDead)
            return;

        AcquireTarget();
    }

    private void FixedUpdate()
    {
        if (stats.IsDead)
            return;

        MaintainHover();

        if (target == null)
            return;

        FaceTarget(target.position);

        if (freezeWhenSeen && IsSeenByTarget())
        {
            currentSpeedMultiplier = baseChargeSpeedMultiplier;
            return;
        }

        currentSpeedMultiplier = Mathf.MoveTowards(currentSpeedMultiplier, maxChargeSpeedMultiplier, accelerationPerSecond * Time.fixedDeltaTime);
        MoveTowardsTarget();

        if (Vector3.Distance(transform.position, target.position) <= explodeRange)
            SelfDestruct();
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
            if (Vector3.Distance(transform.position, target.position) <= loseTargetRadius)
                return;

            target = null;
        }

        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        float bestSqr = detectionRadius * detectionRadius;
        Transform best = null;

        foreach (PlayerStats player in players)
        {
            if (player == null)
                continue;

            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = player.transform;
            }
        }

        if (best == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null)
            {
                float sqr = (tagged.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                    best = tagged.transform;
            }
        }

        target = best;
    }

    private void MaintainHover()
    {
        Vector3 desiredPosition = transform.position;
        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeHeight + groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
            desiredPosition.y = hit.point.y + hoverHeight;

        Vector3 newPosition = Vector3.Lerp(transform.position, desiredPosition, hoverLerpSpeed * Time.fixedDeltaTime);
        if (rb != null && !rb.isKinematic)
            rb.MovePosition(newPosition);
        else
            transform.position = newPosition;
    }

    private void MoveTowardsTarget()
    {
        Vector3 targetPosition = target.position;
        targetPosition.y = transform.position.y;
        Vector3 moveDir = (targetPosition - transform.position).normalized;

        float moveSpeed = stats.sprintSpeed * currentSpeedMultiplier;
        Vector3 nextPosition = transform.position + moveDir * moveSpeed * Time.fixedDeltaTime;

        if (rb != null && !rb.isKinematic)
            rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    private void FaceTarget(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        if (rb != null && !rb.isKinematic)
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }

    private bool IsSeenByTarget()
    {
        if (target == null)
            return false;

        Camera playerCamera = target.GetComponentInChildren<Camera>();
        Transform observer = playerCamera != null ? playerCamera.transform : target;

        Vector3 toEyeball = (transform.position - observer.position);
        float distance = toEyeball.magnitude;
        if (distance <= 0.001f)
            return true;

        Vector3 direction = toEyeball / distance;
        float dot = Vector3.Dot(observer.forward, direction);
        if (dot < seenDotThreshold)
            return false;

        if (Physics.Raycast(observer.position, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return hit.transform == transform || hit.transform.IsChildOf(transform);

        return true;
    }

    private void SelfDestruct()
    {
        if (target != null)
        {
            PlayerStats playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null)
                playerStats.TakeDamage(stats.contactDamage * explosionDamageMultiplier);

            ApplyEffectsTo(target.gameObject, onHitEffects);
        }

        ApplyEffectsTo(gameObject, onSelfDestructEffects);
        stats.Kill();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stats.IsDead)
            return;

        if (target != null && other.transform == target)
            SelfDestruct();
    }

    private static void ApplyEffectsTo(GameObject targetObject, EffectCarrier[] carriers)
    {
        if (targetObject == null || carriers == null)
            return;

        foreach (EffectCarrier carrier in carriers)
        {
            if (carrier != null)
                carrier.Apply(targetObject);
        }
    }
}

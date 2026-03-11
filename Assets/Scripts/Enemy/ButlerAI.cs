using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyStatusEffects))]
[DisallowMultipleComponent]
public class ButlerAI : MonoBehaviour
{
    [Header("Targeting")]
    public Transform explicitTarget;
    public string playerTag = "Player";
    public float detectionRadius = 16f;
    public float loseTargetRadius = 22f;
    public float attackRange = 1.9f;

    [Header("Movement")]
    public float rotationSpeed = 10f;
    public float stopDistance = 1.25f;
    public bool useRigidbodyMove = true;

    [Header("Combat")]
    public float meleeDamageMultiplier = 1f;
    public EffectCarrier[] onHitEffects;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundProbeHeight = 1f;
    public float groundSnapDistance = 3f;

    private EnemyStats stats;
    private Rigidbody rb;
    private Transform target;
    private float nextAttackTime;

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody>();
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

        if (target == null)
            return;

        FaceTarget(target.position);

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > attackRange)
        {
            MoveTowardsTarget();
        }
        else if (Time.time >= nextAttackTime)
        {
            PerformAttack();
        }

        SnapToGround();
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

    private void MoveTowardsTarget()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= stopDistance)
            return;

        Vector3 moveDir = toTarget.normalized;
        Vector3 nextPosition = transform.position + moveDir * stats.walkSpeed * Time.fixedDeltaTime;

        if (useRigidbodyMove && rb != null && !rb.isKinematic)
            rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    private void FaceTarget(Vector3 worldPosition)
    {
        Vector3 flatDirection = worldPosition - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        if (useRigidbodyMove && rb != null && !rb.isKinematic)
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }

    private void PerformAttack()
    {
        nextAttackTime = Time.time + Mathf.Max(0.01f, stats.attackCooldown);

        if (target == null)
            return;

        PlayerStats playerStats = target.GetComponent<PlayerStats>();
        if (playerStats != null)
            playerStats.TakeDamage(stats.contactDamage * meleeDamageMultiplier);

        ApplyEffectsTo(target.gameObject, onHitEffects);
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeHeight + groundSnapDistance, groundMask, QueryTriggerInteraction.Ignore))
            return;

        Vector3 snapped = transform.position;
        snapped.y = hit.point.y;

        if (useRigidbodyMove && rb != null && !rb.isKinematic)
            rb.MovePosition(new Vector3(rb.position.x, snapped.y, rb.position.z));
        else
            transform.position = snapped;
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

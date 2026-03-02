using UnityEngine;

// Eyeball monster behaviour:
// - Floats with a gentle bob
// - Always turns to look at the player
// - When the player is NOT looking at it (outside view cone), it charges quickly toward the player
// - On reaching explodeRange (or attack's range), triggers self-destruction (via SelfDestructAttack)
public class EyeballLookAndChargeBehaviour : EnemyBehaviour
{
    [Header("Floating")]
    public float baseHeightOffset = 1.5f;
    public float bobAmplitude = 0.25f;
    public float bobFrequency = 1.5f;
    public float rotationLerp = 10f;
    [Tooltip("If true, zeroes vertical velocity each frame to prevent drifting up/down.")]
    public bool lockVerticalVelocity = true;
    [Tooltip("If true, float relative to ground height using a downward raycast.")]
    public bool followGround = false;
    public float groundOffset = 1.5f;
    public float groundRayDistance = 10f;
    public LayerMask groundMask = ~0;

    [Header("Watching / Visibility")]
    [Tooltip("Approximate view-cone half-angle to consider the enemy 'watched'.")]
    public float seenHalfAngle = 35f;

    [Header("Charge")]
    public float chargeSpeed = 10f;
    public float chargeAcceleration = 50f;
    public float explodeRange = 1.25f;
    public bool onlyMoveWhenUnseen = true; // classic 'weeping angel' behavior

    [Header("References")]
    public SelfDestructAttack selfDestructAttack; // optional, fetched at runtime
    [Tooltip("Optional visual/model root. Assign to rotate the model separately from the actor.")]
    public Transform modelRoot;
    public float modelRotationLerp = 10f;

    EnemyStats stats;
    Transform player;
    Transform playerView; // camera pivot or main camera
    Rigidbody rb;
    float startY;

    public override void Initialize(EnemyStats stats)
    {
        this.stats = stats;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false; // floats
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // keep upright
        }
        if (selfDestructAttack == null)
            selfDestructAttack = GetComponent<SelfDestructAttack>();
        startY = transform.position.y;
    }

    void Start()
    {
        if (stats == null)
        {
            stats = GetComponent<EnemyStats>();
            if (stats != null) Initialize(stats);
        }
        ResolvePlayerRefs();
    }

    void Update()
    {
        TickBehaviour();
    }

    public override void TickBehaviour()
    {
        if (stats != null && stats.isDead) return;
        if (!ResolvePlayerRefs()) return;

        // Rotate to face player constantly
        Vector3 toPlayer = player.position - transform.position;
        Vector3 flatDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
            if (modelRoot != null)
            {
                modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRot, modelRotationLerp * Time.deltaTime);
            }
        }

        // Vertical bob around a base float height (optionally follow ground)
        float t = Time.time * bobFrequency;
        float bob = Mathf.Sin(t) * bobAmplitude;
        float baseY = startY + baseHeightOffset;
        if (followGround)
        {
            if (Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z), Vector3.down, out RaycastHit hit, groundRayDistance, groundMask))
            {
                baseY = hit.point.y + groundOffset;
            }
        }
        float targetY = baseY + bob;
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, 8f * Time.deltaTime);
        if (rb != null)
            rb.MovePosition(new Vector3(pos.x, pos.y, pos.z));
        else
            transform.position = pos;

        bool isWatched = IsPlayerLookingAtMe();

        // Movement forward when not watched (or always if configured)
        if (!onlyMoveWhenUnseen || !isWatched)
        {
            Vector3 forward = (player.position - transform.position);
            forward.y = 0f;
            float d = forward.magnitude;
            if (d > 0.001f)
            {
                Vector3 dir = forward / d;
                float desiredSpeed = chargeSpeed;
                Vector3 desiredVel = dir * desiredSpeed;

                if (rb != null)
                {
                    Vector3 vel = rb.linearVelocity;
                    Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
                    Vector3 accel = Vector3.ClampMagnitude(desiredVel - horizVel, chargeAcceleration * Time.deltaTime);
                    Vector3 newVel = horizVel + accel;
                    float yVel = lockVerticalVelocity ? 0f : rb.linearVelocity.y;
                    rb.linearVelocity = new Vector3(newVel.x, yVel, newVel.z);
                }
                else
                {
                    transform.position += dir * desiredSpeed * Time.deltaTime;
                }
            }
        }
        else
        {
            // If watched and using RB, damp horizontal velocity
            if (rb != null)
            {
                float yVel = lockVerticalVelocity ? 0f : rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(
                    Mathf.Lerp(rb.linearVelocity.x, 0f, 10f * Time.deltaTime),
                    yVel,
                    Mathf.Lerp(rb.linearVelocity.z, 0f, 10f * Time.deltaTime)
                );
            }
        }

        // Explode if close enough
        float flatDist = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(player.position.x, 0f, player.position.z));
        if (flatDist <= Mathf.Max(explodeRange, selfDestructAttack != null ? selfDestructAttack.attackRange : 0f))
        {
            if (selfDestructAttack != null)
                selfDestructAttack.TriggerExplosion();
        }
    }

    bool ResolvePlayerRefs()
    {
        if (player == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (playerView == null)
        {
            // Prefer the player's camera pivot; fall back to main camera
            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (Camera.main != null)
                playerView = Camera.main.transform;
        }
        return player != null && playerView != null;
    }

    bool IsPlayerLookingAtMe()
    {
        if (playerView == null) return false;
        Vector3 camFwd = playerView.forward;
        Vector3 toMe = (transform.position - playerView.position).normalized;
        float dot = Vector3.Dot(camFwd.normalized, toMe);
        float cosHalf = Mathf.Cos(seenHalfAngle * Mathf.Deg2Rad);
        return dot >= cosHalf; // inside the cone -> considered watched
    }
}

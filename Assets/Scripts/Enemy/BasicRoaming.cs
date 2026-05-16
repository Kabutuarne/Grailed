using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes an enemy patrol a circle of NavMesh-sampled waypoints when idle.
/// Call GetRoamVelocity() each frame instead of applying zero velocity.
/// </summary>
[DisallowMultipleComponent]
public class BasicRoaming : MonoBehaviour
{
    [Header("Roam Shape")]
    [Tooltip("Radius of the patrol circle around the spawn position")]
    public float roamRadius = 8f;

    [Tooltip("Number of evenly-spaced waypoints on the circle")]
    public int roamPoints = 6;

    [Tooltip("How close the enemy must get to advance to the next waypoint")]
    public float waypointReachedDistance = 0.6f;

    [Header("Speed")]
    [Tooltip("Movement speed while roaming (should be <= baseWalkSpeed)")]
    public float roamSpeed = 2f;

    [Header("Pausing")]
    [Tooltip("How long to stand still at each waypoint before moving on")]
    public float waitAtWaypointDuration = 1.5f;

    private EnemyPathing pathing;
    private Vector3[] waypoints;
    private int currentWaypointIndex;
    private float waitTimer;
    private bool isWaiting;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Initialize(MonoBehaviour owner)
    {
        pathing = owner != null ? owner.GetComponent<EnemyPathing>() : null;
        if (pathing == null)
            Debug.LogWarning("BasicRoaming requires an EnemyPathing component on the same GameObject.", owner);

        GenerateCircleWaypoints();
    }

    // ── Main API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the desired velocity for roaming this frame.
    /// Call this from ButlerAI when in Idle state instead of applying zero velocity.
    /// </summary>
    public Vector3 GetRoamVelocity()
    {
        if (waypoints == null || waypoints.Length == 0)
            return Vector3.zero;

        // Tick wait timer
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
                isWaiting = false;

            return Vector3.zero;
        }

        Vector3 target = waypoints[currentWaypointIndex];
        float flatDist = FlatDistance(transform.position, target);

        // Reached waypoint — pause and advance
        if (flatDist <= waypointReachedDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            isWaiting = true;
            waitTimer = waitAtWaypointDuration;
            return Vector3.zero;
        }

        if (pathing != null)
            return pathing.GetDesiredVelocityTowards(target, roamSpeed);

        Vector3 direct = target - transform.position;
        direct.y = 0f;
        return direct.sqrMagnitude > 0.0001f ? direct.normalized * roamSpeed : Vector3.zero;
    }

    /// <summary>Current roam target, used by ButlerAI for facing direction.</summary>
    public Vector3 CurrentTarget =>
        waypoints != null && waypoints.Length > 0
            ? waypoints[currentWaypointIndex]
            : transform.position;

    // ── Waypoint Generation ───────────────────────────────────────────────────

    private void GenerateCircleWaypoints()
    {
        Vector3 center = transform.position;
        waypoints = new Vector3[roamPoints];

        for (int i = 0; i < roamPoints; i++)
        {
            float angle = (360f / roamPoints) * i * Mathf.Deg2Rad;
            Vector3 candidate = center + new Vector3(
                Mathf.Cos(angle) * roamRadius,
                0f,
                Mathf.Sin(angle) * roamRadius
            );

            // Snap to NavMesh — fall back to center if no mesh nearby
            waypoints[i] = NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                ? hit.position
                : center;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypoints == null) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Gizmos.DrawSphere(waypoints[i], 0.2f);
            Gizmos.DrawLine(waypoints[i], waypoints[(i + 1) % waypoints.Length]);
        }

        // Highlight current target
        Gizmos.color = Color.yellow;
        if (waypoints.Length > 0)
            Gizmos.DrawSphere(waypoints[currentWaypointIndex], 0.3f);
    }
#endif
}
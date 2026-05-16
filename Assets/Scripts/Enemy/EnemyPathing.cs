using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyPathing : MonoBehaviour
{
    [Header("NavMesh Pathing")]
    public float pathRecalculationInterval = 0.25f;
    public float destinationUpdateDistance = 0.5f;
    public float waypointTolerance = 0.25f;
    public float navMeshSampleDistance = 1.5f;
    public bool debugPath;

    [Header("Door Interaction")]
    public float doorDetectionRadius = 0.35f;
    public float doorDetectionDistance = 1.8f;
    public float doorCloseDelay = 2.5f;
    public LayerMask doorLayer = ~0;

    [Header("Off-Mesh Link Jumping")]
    public float jumpHeight = 1.5f;
    public float jumpDuration = 0.55f;
    public float offMeshLinkMinDistance = 1.0f;

    private NavMeshPath navMeshPath;
    private Vector3 lastDestination;
    private float lastRecalcTime;
    private int currentCornerIndex;
    private bool hasValidPath;

    // Door tracking
    private Door lastOpenedDoor;
    private Coroutine closeDoorRoutine;

    // Jump state
    private bool isJumping;
    private Coroutine jumpRoutine;
    private Rigidbody ownerRigidbody;

    // Public so AI controllers can check before applying fallback velocity
    public bool IsJumping => isJumping;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Initialize(MonoBehaviour owner)
    {
        ownerRigidbody = owner != null ? owner.GetComponent<Rigidbody>() : null;
        if (ownerRigidbody == null)
            Debug.LogWarning("EnemyPathing requires a Rigidbody on the owner to perform off-mesh jumps.", owner);

        navMeshPath = new NavMeshPath();
        lastDestination = Vector3.positiveInfinity;
        lastRecalcTime = -Mathf.Infinity;
        currentCornerIndex = 0;
        hasValidPath = false;
    }

    void OnDrawGizmos()
    {
        if (navMeshPath == null || navMeshPath.corners.Length == 0) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < navMeshPath.corners.Length - 1; i++)
        {
            Gizmos.DrawLine(navMeshPath.corners[i], navMeshPath.corners[i + 1]);
            Gizmos.DrawSphere(navMeshPath.corners[i], 0.1f);
        }
        Gizmos.DrawSphere(navMeshPath.corners[navMeshPath.corners.Length - 1], 0.1f);
    }

    // ── Main API ──────────────────────────────────────────────────────────────

    public Vector3 GetDesiredVelocityTowards(Vector3 destination, float speed)
    {
        if (speed <= 0f || isJumping)
            return Vector3.zero;

        if (NeedsRepath(destination))
            RecalculatePath(destination);

        if (!hasValidPath)
            return GetDirectVelocity(destination, speed);

        Vector3 currentPosition = transform.position;

        // Advance past corners we've already reached
        while (currentCornerIndex < navMeshPath.corners.Length &&
               Vector3.Distance(currentPosition, navMeshPath.corners[currentCornerIndex]) <= waypointTolerance)
        {
            currentCornerIndex++;
        }

        if (currentCornerIndex >= navMeshPath.corners.Length)
            return Vector3.zero;

        Vector3 nextCorner = navMeshPath.corners[currentCornerIndex];

        // Check if next corner is across an off-mesh link (jump required)
        if (IsOffMeshLink(currentPosition, nextCorner))
        {
            if (jumpRoutine == null)
                jumpRoutine = StartCoroutine(JumpToCorner(nextCorner));
            return Vector3.zero;
        }

        // Check for doors between current position and next corner
        CheckAndInteractWithDoors(currentPosition, nextCorner);

        Vector3 direction = nextCorner - currentPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        if (debugPath)
        {
            Debug.DrawLine(currentPosition, nextCorner, Color.cyan);
            for (int i = currentCornerIndex; i < navMeshPath.corners.Length - 1; i++)
                Debug.DrawLine(navMeshPath.corners[i], navMeshPath.corners[i + 1], Color.yellow);
        }

        return direction.normalized * speed;
    }

    // ── Door Handling ─────────────────────────────────────────────────────────

    private void CheckAndInteractWithDoors(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = Mathf.Min(dir.magnitude, doorDetectionDistance);

        if (!Physics.SphereCast(from, doorDetectionRadius, dir.normalized, out RaycastHit hit, dist, doorLayer))
            return;

        Door door = hit.collider.GetComponentInParent<Door>();
        if (door == null || door == lastOpenedDoor)
            return;

        door.OpenForEnemy(gameObject);
        lastOpenedDoor = door;

        if (closeDoorRoutine != null)
            StopCoroutine(closeDoorRoutine);

        closeDoorRoutine = StartCoroutine(CloseAfterDelay(door));
    }

    private IEnumerator CloseAfterDelay(Door door)
    {
        yield return new WaitForSeconds(doorCloseDelay);

        if (door != null)
            door.CloseForEnemy(gameObject);

        lastOpenedDoor = null;
        closeDoorRoutine = null;
    }

    // ── Off-Mesh Link / Jump Handling ─────────────────────────────────────────

    /// <summary>
    /// Returns true when two consecutive path corners are not directly connected
    /// on the NavMesh surface, meaning they span an off-mesh link.
    /// </summary>
    private bool IsOffMeshLink(Vector3 from, Vector3 to)
    {
        // Too close to be a link — skip to avoid false positives at tight corners
        if (Vector3.Distance(from, to) < offMeshLinkMinDistance)
            return false;

        // NavMesh.Raycast returns true when it hits a navmesh boundary before reaching 'to',
        // meaning the straight path between these two points is not walkable navmesh surface.
        return NavMesh.Raycast(from, to, out _, NavMesh.AllAreas);
    }

    private IEnumerator JumpToCorner(Vector3 target)
    {
        isJumping = true;

        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // Parabolic arc: lerp horizontally, add bell-curve vertical offset
            Vector3 horizontal = Vector3.Lerp(start, target, t);
            float arc = jumpHeight * 4f * t * (1f - t);
            Vector3 worldPos = horizontal + Vector3.up * arc;

            if (ownerRigidbody != null)
                ownerRigidbody.MovePosition(worldPos);

            yield return null;
        }

        // Snap to exact target and advance corner index
        if (ownerRigidbody != null)
            ownerRigidbody.MovePosition(target);

        currentCornerIndex++;
        isJumping = false;
        jumpRoutine = null;
    }

    // ── Path Helpers ──────────────────────────────────────────────────────────

    private bool NeedsRepath(Vector3 destination)
    {
        if (!hasValidPath) return true;
        if (Time.time - lastRecalcTime >= pathRecalculationInterval) return true;
        if ((destination - lastDestination).sqrMagnitude >= destinationUpdateDistance * destinationUpdateDistance) return true;
        return false;
    }

    private void RecalculatePath(Vector3 destination)
    {
        lastRecalcTime = Time.time;
        lastDestination = destination;
        currentCornerIndex = 0;

        Vector3 sourcePosition = GetSampledNavMeshPosition(transform.position, out bool sourceFound);
        Vector3 targetPosition = GetSampledNavMeshPosition(destination, out bool targetFound);

        if (!sourceFound || !targetFound)
        {
            hasValidPath = false;
            return;
        }

        hasValidPath = NavMesh.CalculatePath(sourcePosition, targetPosition, NavMesh.AllAreas, navMeshPath)
                       && navMeshPath.status == NavMeshPathStatus.PathComplete
                       && navMeshPath.corners.Length > 1;

        if (hasValidPath)
            currentCornerIndex = 1;
    }

    private Vector3 GetSampledNavMeshPosition(Vector3 worldPosition, out bool found)
    {
        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            found = true;
            return hit.position;
        }

        found = false;
        return worldPosition;
    }

    private Vector3 GetDirectVelocity(Vector3 destination, float speed)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return Vector3.zero;
        return direction.normalized * speed;
    }
}
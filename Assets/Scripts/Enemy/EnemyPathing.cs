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

    private NavMeshPath navMeshPath;
    private Vector3 lastDestination;
    private float lastRecalcTime;
    private int currentCornerIndex;
    private bool hasValidPath;

    public void Initialize(MonoBehaviour owner)
    {
        navMeshPath = new NavMeshPath();
        lastDestination = Vector3.positiveInfinity;
        lastRecalcTime = -Mathf.Infinity;
        currentCornerIndex = 0;
        hasValidPath = false;
    }

    public Vector3 GetDesiredVelocityTowards(Vector3 destination, float speed)
    {
        if (speed <= 0f)
            return Vector3.zero;

        if (NeedsRepath(destination))
            RecalculatePath(destination);

        if (!hasValidPath)
            return GetDirectVelocity(destination, speed);

        Vector3 currentPosition = transform.position;

        while (currentCornerIndex < navMeshPath.corners.Length &&
               Vector3.Distance(currentPosition, navMeshPath.corners[currentCornerIndex]) <= waypointTolerance)
        {
            currentCornerIndex++;
        }

        if (currentCornerIndex >= navMeshPath.corners.Length)
            return Vector3.zero;

        Vector3 nextCorner = navMeshPath.corners[currentCornerIndex];
        Vector3 direction = nextCorner - currentPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 desiredVelocity = direction.normalized * speed;
        if (debugPath)
        {
            Debug.DrawLine(currentPosition, nextCorner, Color.cyan);
            for (int i = currentCornerIndex; i < navMeshPath.corners.Length - 1; i++)
            {
                Debug.DrawLine(navMeshPath.corners[i], navMeshPath.corners[i + 1], Color.yellow);
            }
        }

        return desiredVelocity;
    }

    private bool NeedsRepath(Vector3 destination)
    {
        if (!hasValidPath)
            return true;

        if (Time.time - lastRecalcTime >= pathRecalculationInterval)
            return true;

        if ((destination - lastDestination).sqrMagnitude >= destinationUpdateDistance * destinationUpdateDistance)
            return true;

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
        {
            // The first corner is usually the sampled start position.
            currentCornerIndex = 1;
        }
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
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return direction.normalized * speed;
    }
}

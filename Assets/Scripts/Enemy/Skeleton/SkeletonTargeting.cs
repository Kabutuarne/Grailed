using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonTargeting : MonoBehaviour
{
    [Header("Targeting")]
    public string playerTag = "Player";
    public float detectionRadius = 12f;
    public float loseTargetRadius = 20f;

    private SkeletonAI ai;
    private Transform trackedTarget;

    public Transform CurrentTarget => trackedTarget;
    public bool HasTarget => trackedTarget != null;

    public void Initialize(SkeletonAI skeletonAI) => ai = skeletonAI;

    public Transform AcquireTarget()
    {
        // Maintain target until it leaves lose radius
        if (trackedTarget != null)
        {
            if (FlatDistance(transform.position, trackedTarget.position) <= loseTargetRadius)
                return trackedTarget;

            trackedTarget = null;
        }

        trackedTarget = FindClosestPlayer();
        Debug.Log($"[{gameObject.name}] AcquireTarget: {(trackedTarget != null ? "Found target" : "No target found")}");
        return trackedTarget;
    }

    private Transform FindClosestPlayer()
    {
        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        float bestSqrDistance = detectionRadius * detectionRadius;
        Transform bestTarget = null;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerStats player = players[i];
            if (player == null) continue;

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance) continue;

            bestSqrDistance = sqrDistance;
            bestTarget = player.transform;
        }

        if (bestTarget != null || string.IsNullOrEmpty(playerTag))
            return bestTarget;

        // Fallback to tag-based search
        GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
        if (tagged == null) return null;

        float taggedDistance = (tagged.transform.position - transform.position).sqrMagnitude;
        return taggedDistance <= bestSqrDistance ? tagged.transform : null;
    }

    /// <summary>Distance on XZ plane only.</summary>
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
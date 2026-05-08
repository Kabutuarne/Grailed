using UnityEngine;

/// <summary>
/// Handles finding and tracking the player target.
/// </summary>
[DisallowMultipleComponent]
public class ButlerTargeting : MonoBehaviour
{
    [Header("Targeting")]
    public string playerTag = "Player";
    public float detectionRadius = 15f;
    public float loseTargetRadius = 25f;
    [Tooltip("Optional explicit target that overrides automatic acquisition")]
    public Transform explicitTarget;

    private ButlerAI ai;
    private Transform trackedTarget;
    private bool hasAlerted;

    public Transform CurrentTarget => trackedTarget;
    public bool HasTarget => trackedTarget != null;

    public void Initialize(ButlerAI butlerAI)
    {
        ai = butlerAI;
    }

    /// <summary>
    /// Called each frame by ButlerAI. Returns the current target or null.
    /// </summary>
    public Transform AcquireTarget()
    {
        // Explicit target overrides everything
        if (explicitTarget != null)
        {
            trackedTarget = explicitTarget;
            return trackedTarget;
        }

        // Check if we should lose current target
        if (trackedTarget != null)
        {
            if (FlatDistance(transform.position, trackedTarget.position) <= loseTargetRadius)
                return trackedTarget;

            trackedTarget = null;
            hasAlerted = false;
        }

        // Find new target
        trackedTarget = FindClosestPlayer();

        if (trackedTarget != null && !hasAlerted)
        {
            hasAlerted = true;
            ai.audioController.OnAlert();
        }

        if (trackedTarget == null)
            hasAlerted = false;

        return trackedTarget;
    }

    /// <summary>Forcefully set a target (e.g., from AlertTo).</summary>
    public void SetTarget(Transform newTarget)
    {
        trackedTarget = newTarget;
        hasAlerted = false;
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
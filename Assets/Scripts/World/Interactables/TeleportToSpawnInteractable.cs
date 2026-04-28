using UnityEngine;

/// <summary>
/// Teleports the player to the spawn point (StartTransform) of the generated level sections.
/// </summary>
public class TeleportToSpawnInteractable : BaseInteractable
{
    [Header("Spawn Settings")]
    [Tooltip("Tag to search for spawn point (default: 'StartTransform')")]
    public string spawnTag = "StartTransform";

    protected override void OnInteractComplete(GameObject interactor)
    {
        var spawnPoint = GameObject.FindWithTag(spawnTag);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"Could not find spawn point with tag '{spawnTag}'", this);
            return;
        }

        TeleportPlayerTo(spawnPoint.transform);
    }

    private void TeleportPlayerTo(Transform targetTransform)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("Could not find player with tag 'Player'", this);
            return;
        }

        var charController = player.GetComponent<CharacterController>();
        if (charController != null)
            charController.enabled = false;

        player.transform.position = targetTransform.position;
        player.transform.rotation = targetTransform.rotation;

        if (charController != null)
            charController.enabled = true;
    }
}

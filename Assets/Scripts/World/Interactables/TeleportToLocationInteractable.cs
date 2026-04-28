using UnityEngine;

/// <summary>
/// Teleports the player to a specific location set in the inspector.
/// </summary>
public class TeleportToLocationInteractable : BaseInteractable
{
    [Header("Destination")]
    [Tooltip("Target location to teleport to")]
    public Transform targetLocation;

    protected override void OnInteractComplete(GameObject interactor)
    {
        if (targetLocation == null)
        {
            Debug.LogError("TeleportToLocationInteractable has no target location assigned!", this);
            return;
        }

        TeleportPlayerTo(targetLocation);
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

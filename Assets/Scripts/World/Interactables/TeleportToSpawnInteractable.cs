using UnityEngine;

/// <summary>
/// Teleports the player to the spawn point (StartTransform) of the generated level sections.
/// Finds objects by name even if they are inactive.
/// </summary>
public class TeleportToSpawnInteractable : BaseInteractable
{
    [Header("Spawn Settings")]
    [Tooltip("Tag to search for spawn point (default: 'StartTransform')")]
    public string spawnTag = "StartTransform";

    [Header("Entry Actions")]
    [Tooltip("Optional hierarchy object name to enable when teleporting.")]
    public string enableObjectName;

    [Tooltip("Optional hierarchy object name to disable when teleporting.")]
    public string disableObjectName;

    protected override void OnInteractComplete(GameObject interactor)
    {
        var spawnPoint = GameObject.FindWithTag(spawnTag);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"Could not find spawn point with tag '{spawnTag}'", this);
            return;
        }

        TeleportPlayerTo(spawnPoint.transform);

        SetObjectActive(enableObjectName, true);
        SetObjectActive(disableObjectName, false);
    }

    private void SetObjectActive(string objectName, bool active)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return;

        GameObject obj = FindGameObjectByNameIncludingInactive(objectName);

        if (obj == null)
        {
            Debug.LogWarning($"Could not find GameObject named '{objectName}'", this);
            return;
        }

        obj.SetActive(active);
    }

    private GameObject FindGameObjectByNameIncludingInactive(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform t in transforms)
        {
            if (t.name == objectName &&
                t.gameObject.hideFlags == HideFlags.None)
            {
                return t.gameObject;
            }
        }

        return null;
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
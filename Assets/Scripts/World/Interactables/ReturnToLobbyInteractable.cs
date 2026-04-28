using UnityEngine;

/// <summary>
/// Exit interactable. When interacted with in a level, returns player to lobby with all items and stats.
/// </summary>
public class ReturnToLobbyInteractable : BaseInteractable
{
    [Header("Lobby Settings")]
    [Tooltip("Scene name to load (usually the main lobby/cabin)")]
    public string lobbySceneName = "CabinScene";

    protected override void OnInteractComplete(GameObject interactor)
    {
        // Load the lobby scene (preserves player state through scene manager or save system)
        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
    }
}

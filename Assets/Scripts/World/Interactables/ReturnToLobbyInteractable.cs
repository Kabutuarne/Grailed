using UnityEngine;

/// <summary>
/// Exit interactable. When interacted with in a mission scene, marks the current mission
/// as complete and returns the player to the lobby.
/// </summary>
public class ReturnToLobbyInteractable : BaseInteractable
{
    [Header("Lobby Settings")]
    [Tooltip("Scene name to load (usually the main lobby/cabin)")]
    public string lobbySceneName = "CabinScene";

    protected override void OnInteractComplete(GameObject interactor)
    {
        // Mark the active mission complete before leaving the scene.
        MissionManager.Instance?.EndCurrentMission();

        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
    }
}
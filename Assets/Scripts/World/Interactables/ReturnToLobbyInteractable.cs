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

    [Tooltip("Tag of the spawn point in the lobby scene")]
    public string spawnPointTag = "PlayerSpawnPoint";

    protected override void OnInteractComplete(GameObject interactor)
    {
        Debug.Log($"ReturnToLobbyInteractable: Storing spawn point '{spawnPointTag}' and loading '{lobbySceneName}'");

        // Store the spawn point information before loading the new scene
        // This tells PlayerPersistenceManager that we're returning from a mission
        // and should move the player to this spawn point
        PlayerPrefs.SetString("LastSpawnPointTag", spawnPointTag);
        PlayerPrefs.Save();

        // Mark the active mission complete before leaving the scene
        MissionManager.Instance?.EndCurrentMission();

        // Load the lobby scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
    }
}
using UnityEngine;

/// <summary>
/// Entry point interactable. When interacted with in the lobby, starts a level.
/// Loads the level scene with the specified catalog.
/// </summary>
public class LevelStartInteractable : BaseInteractable
{
    [Header("Level Settings")]
    [Tooltip("The PerLevelCatalog to use for this level")]
    public PerLevelCatalog levelCatalog;

    [Tooltip("Scene name to load")]
    public string levelSceneName = "Dungeon";

    protected override void OnInteractComplete(GameObject interactor)
    {
        if (levelCatalog == null)
        {
            Debug.LogError("LevelStartInteractable has no catalog assigned!", this);
            return;
        }

        // Set the active level in GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActiveLevel = levelCatalog;
        }

        // Load the level scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(levelSceneName);
    }
}

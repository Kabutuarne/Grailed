using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach this to any persistent GameObject in CabinScene.
///
/// Position restore is handled by PlayerPersistenceManager which runs its
/// teleport coroutine after scene initialisation, after this Start() runs.
/// </summary>
public class CabinQuitSave : MonoBehaviour
{
    private const string CabinSceneName = "CabinScene";

    // =====================================================================
    // Lifecycle
    // =====================================================================

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != CabinSceneName)
        {
            Debug.LogWarning("[CabinQuitSave] Not in CabinScene — component disabled.");
            enabled = false;
            return;
        }

        ApplySaveToScene();
    }

    private void Update()
    {
        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.TickPlayTime();
    }

    // =====================================================================
    // Public API -- wire to your pause UI Save & Quit button
    // =====================================================================

    /// <summary>
    /// Snapshots the current game state, writes to disk, and loads the main
    /// menu. Safe to call directly from a UI Button OnClick().
    /// </summary>
    public void SaveAndQuit()
    {
        if (GameSaveManager.Instance == null)
        {
            Debug.LogError("[CabinQuitSave] GameSaveManager not found.");
            return;
        }

        GameSaveManager.Instance.SaveAndQuitToMainMenu();
    }

    // =====================================================================
    // Private
    // =====================================================================

    private void ApplySaveToScene()
    {
        var gsm = GameSaveManager.Instance;
        if (gsm == null || gsm.ActiveSave == null) return;

        // Apply attributes and resources (position is handled by PlayerPersistenceManager).
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            var stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null)
                gsm.TryApplyToPlayer(stats);
        }
        else
        {
            Debug.LogWarning("[CabinQuitSave] No GameObject tagged 'Player' found.");
        }

        // Restore mission state.
        if (MissionManager.Instance != null)
            gsm.TryApplyMissionsToManager(MissionManager.Instance);
    }
}
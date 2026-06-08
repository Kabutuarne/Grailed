using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach this to any persistent GameObject in CabinScene.
///
/// This script provides a "Delete Save" button functionality that
/// removes the current save file and returns to the main menu.
/// Position restore is handled by PlayerPersistenceManager.
/// </summary>
public class CabinDeleteSave : MonoBehaviour
{
    private const string CabinSceneName = "CabinScene";

    // =====================================================================
    // Lifecycle
    // =====================================================================

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != CabinSceneName)
        {
            Debug.LogWarning("[CabinDeleteSave] Not in CabinScene — component disabled.");
            enabled = false;
            return;
        }
    }

    // =====================================================================
    // Public API -- wire to your pause UI Delete Save button
    // =====================================================================

    /// <summary>
    /// Deletes the current active save from disk and loads the main menu.
    /// Safe to call directly from a UI Button OnClick().
    /// </summary>
    public void DeleteSaveAndQuit()
    {
        if (GameSaveManager.Instance == null)
        {
            Debug.LogError("[CabinDeleteSave] GameSaveManager not found.");
            return;
        }

        GameSaveManager.Instance.DeleteSaveAndQuitToMainMenu();
    }
}
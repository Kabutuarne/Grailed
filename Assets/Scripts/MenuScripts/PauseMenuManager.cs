using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// In-game pause menu controller.
/// Attach to a UI GameObject (e.g. the Canvas). Assign the `pauseRoot` panel (initially inactive)
/// and optionally assign a `settingsPanel` (can reuse the same settings UI used in the Main Menu).
/// The script listens for the `Pause` action on the generated `PlayerInputActions` class.
/// Buttons should call `OnResumeButton`, `OnSettingsButton`, `OnSaveAndQuitButton`, and `OnCloseSettings`.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject GameCanvas;

    [Header("Scene")]
    [Tooltip("Name of the Main Menu scene to load when saving & quitting.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Options")]
    [Tooltip("If enabled, setting Time.timeScale to 0 will pause gameplay when menu opens.")]
    [SerializeField] private bool pauseTime = true;

    private PlayerInputActions input;
    private bool isPaused = false;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        // Ensure UI starts hidden
        if (pauseRoot != null) pauseRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Create input wrapper and bind Pause action (generated class).
        input = new PlayerInputActions();
        input.Player.Enable();

        try
        {
            input.Player.Pause.performed += OnPausePerformed;
        }
        catch (Exception)
        {
            Debug.LogWarning("Pause action not found on PlayerInputActions. You can still call TogglePause() from UI.");
        }
    }

    private void OnDestroy()
    {
        if (input != null)
        {
            try { input.Player.Pause.performed -= OnPausePerformed; } catch { }
            input.Player.Disable();
            input = null;
        }
    }

    // Note: we intentionally do not enable/disable input in OnEnable/OnDisable so
    // the input action remains active even if this GameObject's Canvas is toggled.

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    private void Pause()
    {
        if (isPaused) return;
        isPaused = true;

        previousTimeScale = Time.timeScale;
        if (pauseTime) Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseRoot != null) pauseRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Hide player UI canvas (but don't deactivate the GameObject if possible)
        SetGameCanvasActive(false);

        // Lock player controls if a PlayerController is present
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.SetControlLocked(true);
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;

        if (pauseTime) Time.timeScale = Mathf.Max(previousTimeScale, 1f);
        AudioListener.pause = false;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseRoot != null) pauseRoot.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Restore player UI canvas
        SetGameCanvasActive(true);

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.SetControlLocked(false);
    }

    // Attempt to hide/show the player UI without deactivating the GameObject that may
    // host this manager. Prefer disabling the Canvas/GraphicRaycaster/CanvasGroup so
    // MonoBehaviours on the same GameObject remain enabled.
    private void SetGameCanvasActive(bool active)
    {
        if (GameCanvas == null) return;

        var canvas = GameCanvas.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = active;

            var gr = GameCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (gr != null) gr.enabled = active;

            var cg = GameCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable = active;
                cg.blocksRaycasts = active;
                cg.alpha = active ? 1f : 1f;
            }

            return;
        }

        // Fallback to disabling the GameObject if no Canvas component found
        GameCanvas.SetActive(active);
    }

    // UI wiring methods ----------------------------------------------------

    public void OnResumeButton()
    {
        Resume();
        OnCloseSettings(); // also close settings if open
    }

    public void OnSettingsButton()
    {
        settingsPanel?.SetActive(true);
        mainPanel?.SetActive(false);
    }

    public void OnCloseSettings()
    {
        settingsPanel?.SetActive(false);
        mainPanel?.SetActive(true);
    }

    public void OnSaveAndQuitButton()
    {
        // Ensure a slot is selected (fall back to slot 0)
        if (SaveSlotContext.SelectedSlot < 0)
        {
            Debug.LogWarning("No save slot selected. Defaulting to slot 0 for AutoSave.");
            SaveSlotContext.SelectedSlot = 0;
        }

        var slot = SaveSlotContext.SelectedSlot;

        // Create a minimal SaveData snapshot. Fill attributes if PlayerStats exists.
        var save = new SaveData();
        save.isEmpty = false;
        save.saveName = $"AutoSave {slot + 1}";
        save.timestamp = DateTime.Now.ToString("s");
        save.playTimeSeconds = Time.timeSinceLevelLoad;

        var stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            try
            {
                save.intelligence = stats.intelligence;
                save.strength = stats.strength;
                save.staminaAttr = stats.staminaAttr;
                save.agility = stats.agility;
            }
            catch { /* if fields differ, leave defaults */ }
        }

        // Write and return to main menu
        SaveSlotContext.WriteActiveSave(save);

        // Restore time before changing scenes
        if (pauseTime) Time.timeScale = Mathf.Max(previousTimeScale, 1f);
        AudioListener.pause = false;

        // Load main menu scene
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogWarning("Main Menu scene name is empty. Configure the PauseMenuManager.");
    }
}

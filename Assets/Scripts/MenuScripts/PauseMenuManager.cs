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

    /// <summary>True while the pause menu is open.</summary>
    public bool IsPaused => isPaused;

    // Track ALL blocking UI states, not just the backpack.
    // Previously only wasBackpackOpenBeforePause was tracked, so mission picker
    // open before pause would cause Resume() to take the wrong branch and
    // permanently lock controls or hide the HUD.
    private bool wasBackpackOpenBeforePause = false;
    private bool wasMissionPickerOpenBeforePause = false;

    private void Awake()
    {
        if (pauseRoot != null) pauseRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

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

        // Snapshot ALL blocking UI states before pausing, not just backpack.
        var playerUi = FindFirstObjectByType<PlayerUI>();
        var missionPicker = FindFirstObjectByType<MissionPickerUI>();

        wasBackpackOpenBeforePause = playerUi != null && playerUi.IsBackpackOpen;
        wasMissionPickerOpenBeforePause = missionPicker != null && missionPicker.IsOpen;

        previousTimeScale = Time.timeScale;
        if (pauseTime) Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseRoot != null) pauseRoot.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);

        // Disable gameplay input while paused; keep Pause (and Backpack so Tab still
        // works) enabled so the player can toggle back.
        if (input != null)
        {
            try
            {
                input.Player.Disable();
                input.Player.Pause.Enable();
            }
            catch { }
        }

        var cast = FindFirstObjectByType<CastUI>();
        if (cast != null) cast.Hide();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Only hide the game canvas if NO other blocking UI was already showing it
        // with mouse-cursor mode active. Previously this always hid the canvas, which
        // clashed with MissionPickerUI having already set up its own overlay on top of
        // the game canvas. Now we only hide if nothing else needed the canvas visible.
        if (!wasBackpackOpenBeforePause && !wasMissionPickerOpenBeforePause)
        {
            SetGameCanvasActive(false);
        }

        // Only lock player movement if no other UI was already locking it.
        // If MissionPickerUI already called SetControlLocked(true), calling it again
        // is harmless, but we must NOT unlock it on resume if that UI is still open.
        if (!wasBackpackOpenBeforePause && !wasMissionPickerOpenBeforePause)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) pc.SetControlLocked(true);
        }
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;

        // Restore exactly the timescale that was active before the pause.
        // The old code used Mathf.Max(previous, 1f) which would incorrectly snap
        // slow-motion or any sub-1 timescale up to 1 on resume.
        if (pauseTime) Time.timeScale = previousTimeScale;
        AudioListener.pause = false;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseRoot != null) pauseRoot.SetActive(false);

        // Re-check what blocking UIs are CURRENTLY open (the player may have closed
        // the backpack or mission picker while the pause menu was up, even though
        // input was disabled – they could be closed by other code paths).
        var missionPicker = FindFirstObjectByType<MissionPickerUI>();
        var playerUi = FindFirstObjectByType<PlayerUI>();

        bool backpackOpen = playerUi != null && playerUi.IsBackpackOpen;
        bool missionPickerOpen = missionPicker != null && missionPicker.IsOpen;
        bool anyBlockingUI = backpackOpen || missionPickerOpen;

        if (input != null)
        {
            try
            {
                if (anyBlockingUI)
                {
                    // Keep most gameplay inputs off; allow Pause and Backpack toggling.
                    input.Player.Disable();
                    input.Player.Pause.Enable();
                    input.Player.Backpack.Enable();
                }
                else
                {
                    // Nothing blocking – restore all gameplay inputs.
                    input.Player.Enable();
                }
            }
            catch { }
        }

        if (!anyBlockingUI)
        {
            // Normal resume: lock cursor, restore canvas, unlock movement.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SetGameCanvasActive(true);

            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) pc.SetControlLocked(false);
        }
        else
        {
            // Some blocking UI is still open. Keep the canvas visible and cursor
            // unlocked, but do NOT re-lock player movement – the respective UI
            // manager (MissionPickerUI / backpack) owns that lock and will release
            // it when it closes.
            SetGameCanvasActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Ensure the HUD is visible regardless of which UI is open.
            // Previously, when the mission picker was open before pause, and you
            // resumed, the HUD could end up hidden because SetGameCanvasActive(false)
            // had been called on pause and the canvas was never re-shown.
            if (playerUi != null && playerUi.hudRoot != null)
                playerUi.hudRoot.SetActive(true);
        }
    }

    /// <summary>
    /// Attempt to hide/show the player UI without deactivating the GameObject that may
    /// host this manager. Prefer disabling the Canvas/GraphicRaycaster/CanvasGroup so
    /// MonoBehaviours on the same GameObject remain enabled.
    /// </summary>
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
                cg.alpha = active ? 1f : 0f;
            }

            return;
        }

        // Fallback to disabling the GameObject if no Canvas component found.
        GameCanvas.SetActive(active);
    }

    // UI wiring methods ----------------------------------------------------

    public void OnResumeButton()
    {
        Resume();
        OnCloseSettings();
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
        //Always restore timescale and audio before loading a new scene,
        // otherwise the main menu will load in a frozen/muted state.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
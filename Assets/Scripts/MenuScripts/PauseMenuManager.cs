using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject gameCanvas;       // The HUD canvas, not the entire GameCanvas
    [SerializeField] private GameObject missionPickerCanvas; // Assign the MissionPickerUI's root Canvas here (optional)
    public QuitButtonLabel quitButtonLabel;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Options")]
    [SerializeField] private bool pauseTime = true;

    private PlayerInputActions input;
    private bool isPaused = false;
    private float previousTimeScale = 1f;
    public bool IsPaused => isPaused;

    private bool wasBackpackOpenBeforePause = false;
    private bool wasMissionPickerOpenBeforePause = false;

    private void Awake()
    {
        if (pauseRoot != null) pauseRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        input = new PlayerInputActions();
        input.Player.Enable();
        try { input.Player.Pause.performed += OnPausePerformed; }
        catch (System.Exception) { Debug.LogWarning("Pause action not found."); }
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
        quitButtonLabel?.UpdateLabel();
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

        var playerUi = FindFirstObjectByType<PlayerUI>();
        var missionPicker = FindFirstObjectByType<MissionPickerUI>();

        wasBackpackOpenBeforePause = playerUi != null && playerUi.IsBackpackOpen;
        wasMissionPickerOpenBeforePause = missionPicker != null && missionPicker.IsOpen;

        previousTimeScale = Time.timeScale;
        if (pauseTime) Time.timeScale = 0f;
        AudioListener.pause = true;

        pauseRoot?.SetActive(true);
        settingsPanel?.SetActive(false);
        mainPanel?.SetActive(true);

        if (input != null)
        {
            try { input.Player.Disable(); input.Player.Pause.Enable(); } catch { }
        }

        FindFirstObjectByType<CastUI>()?.Hide();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Only hide the HUD canvas, NOT the mission picker's canvas
        if (!wasMissionPickerOpenBeforePause && gameCanvas != null)
        {
            gameCanvas.SetActive(false);
        }
        if (missionPickerCanvas != null)
        {
            // Ensure mission picker canvas remains enabled if it was open
            missionPickerCanvas.SetActive(wasMissionPickerOpenBeforePause);
        }

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

        if (pauseTime) Time.timeScale = previousTimeScale;
        AudioListener.pause = false;

        settingsPanel?.SetActive(false);
        pauseRoot?.SetActive(false);

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
                    input.Player.Disable();
                    input.Player.Pause.Enable();
                    input.Player.Backpack.Enable();
                }
                else
                {
                    input.Player.Enable();
                }
            }
            catch { }
        }

        if (!anyBlockingUI)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (gameCanvas != null) gameCanvas.SetActive(true);
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) pc.SetControlLocked(false);
        }
        else
        {
            if (gameCanvas != null) gameCanvas.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerUi?.hudRoot != null) playerUi.hudRoot.SetActive(true);

            // Ensure mission picker canvas remains enabled
            if (missionPickerCanvas != null && missionPickerOpen)
                missionPickerCanvas.SetActive(true);
        }
    }

    public void OnResumeButton() { Resume(); OnCloseSettings(); }
    public void OnSettingsButton() { settingsPanel?.SetActive(true); mainPanel?.SetActive(false); }
    public void OnCloseSettings() { settingsPanel?.SetActive(false); mainPanel?.SetActive(true); }

    public void OnSaveAndQuitButton()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSaveManager.Instance?.SaveAndQuitToMainMenu();
    }
}
using UnityEngine;

/// <summary>
/// Controls which UI panel is visible in the Main Menu.
/// Assign each panel in the Inspector. Call Show* methods from Button OnClick events.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Core Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject savesPanel;

    [Header("Optional Panels")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject loadingPanel;

    private GameObject _activePanel;

    private void Start()
    {
        // Ensure only the main panel is visible on startup
        DisableAllPanels();
        TransitionTo(mainPanel);
    }

    // ── Public Navigation ──────────────

    public void ShowMain() => TransitionTo(mainPanel);
    public void ShowSettings() => TransitionTo(settingsPanel);
    public void ShowSaves() => TransitionTo(savesPanel);
    public void ShowCredits() => TransitionTo(creditsPanel);
    public void ShowLoading() => TransitionTo(loadingPanel);

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void TransitionTo(GameObject targetPanel)
    {
        if (targetPanel == null) return;

        if (_activePanel != null)
            _activePanel.SetActive(false);

        _activePanel = targetPanel;
        _activePanel.SetActive(true);
    }

    private void DisableAllPanels()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (savesPanel != null) savesPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }
}
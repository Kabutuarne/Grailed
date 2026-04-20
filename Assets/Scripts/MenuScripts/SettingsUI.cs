using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Wires the Settings panel UI to the persistent <see cref="SettingsManager"/>.
/// Place this on the root of your Settings panel. Assign sub-panels and controls
/// in the Inspector. Works in any scene as long as SettingsManager exists.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    // ── Sub-section panels (toggle via the tab buttons) ───────────────────────

    [Header("Settings Sub-Sections")]
    [SerializeField] private GameObject audioSection;
    [SerializeField] private GameObject displaySection;
    [SerializeField] private GameObject keybindsSection;

    [Header("Settings Sub-Sections Tabs")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private GameObject audioTabButtonGlow;
    [SerializeField] private Button displayTabButton;
    [SerializeField] private GameObject displayTabButtonGlow;
    [SerializeField] private Button keybindsTabButton;
    [SerializeField] private GameObject keybindsTabButtonGlow;

    // ── Audio ─────────────────────────────────────────────────────────────────

    [Header("Audio Controls")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text musicVolumeLabel;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text sfxVolumeLabel;

    // ── Display ───────────────────────────────────────────────────────────────

    [Header("Display Controls")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider resolutionScaleSlider;
    [SerializeField] private TMP_Text resolutionScaleLabel;
    /// <summary>
    /// Slider range should be set in the Inspector to
    /// [SettingsManager.MouseSensitivityMin … SettingsManager.MouseSensitivityMax].
    /// </summary>
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Text mouseSensitivityLabel;

    // ── Keybinds ──────────────────────────────────────────────────────────────

    [Header("Keybind Controls")]
    [SerializeField] private Transform keybindContainer;   // Scroll content parent
    [SerializeField] private RebindActionUI rebindRowPrefab;    // Prefab with RebindActionUI
    [SerializeField] private Button resetBindingsButton;

    // ── Internal ──────────────────────────────────────────────────────────────

    private SettingsManager _settings;
    private readonly List<RebindActionUI> _rebindRows = new();

    /// <summary>
    /// Actions to skip entirely in the keybind list (e.g. mouse-driven controls
    /// that don't make sense to rebind via keyboard).
    /// </summary>
    private static readonly HashSet<string> SkippedActions = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "Look",
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        _settings = SettingsManager.Instance;

        if (_settings == null)
        {
            Debug.LogWarning("[SettingsUI] SettingsManager not found. " +
                             "Make sure it exists in the boot/first scene.");
            return;
        }

        InitialiseResolutionSlider();
        InitialiseMouseSensitivitySlider();
        RefreshAllControls();
        SubscribeToEvents();
        RegisterUICallbacks();

        // Show the Display section by default when opening Settings
        ShowDisplaySection();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        UnregisterUICallbacks();
    }

    // ── Section Tabs ──────────────────────────────────────────────────────────

    public void ShowAudioSection() => ActivateSection(audioSection);
    public void ShowDisplaySection() => ActivateSection(displaySection);
    public void ShowKeybindsSection()
    {
        ActivateSection(keybindsSection);
        BuildKeybindRows(); // Rebuild in case bindings changed externally
    }

    private void ActivateSection(GameObject target)
    {
        (GameObject section, Button tab, GameObject glow)[] tabs =
        {
            (audioSection,    audioTabButton,    audioTabButtonGlow),
            (displaySection,  displayTabButton,  displayTabButtonGlow),
            (keybindsSection, keybindsTabButton, keybindsTabButtonGlow),
        };

        foreach (var (section, tab, glow) in tabs)
        {
            bool isActive = section == target;
            section.SetActive(isActive);
            glow.SetActive(isActive);

            var label = tab.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = isActive
                    ? new Color(1f, 0.84f, 0f)  // Gold — active tab
                    : new Color(1f, 0f, 0f); // Red  — inactive tab
        }
    }

    // ── Control Registration ──────────────────────────────────────────────────

    private void RegisterUICallbacks()
    {
        musicVolumeSlider?.onValueChanged.AddListener(OnMusicSliderChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSFXSliderChanged);
        fullscreenToggle?.onValueChanged.AddListener(OnFullscreenToggleChanged);
        resolutionScaleSlider?.onValueChanged.AddListener(OnResolutionScaleSliderChanged);
        mouseSensitivitySlider?.onValueChanged.AddListener(OnMouseSensitivitySliderChanged);
        resetBindingsButton?.onClick.AddListener(OnResetBindingsClicked);
    }

    private void UnregisterUICallbacks()
    {
        musicVolumeSlider?.onValueChanged.RemoveListener(OnMusicSliderChanged);
        sfxVolumeSlider?.onValueChanged.RemoveListener(OnSFXSliderChanged);
        fullscreenToggle?.onValueChanged.RemoveListener(OnFullscreenToggleChanged);
        resolutionScaleSlider?.onValueChanged.RemoveListener(OnResolutionScaleSliderChanged);
        mouseSensitivitySlider?.onValueChanged.RemoveListener(OnMouseSensitivitySliderChanged);
        resetBindingsButton?.onClick.RemoveListener(OnResetBindingsClicked);
    }

    private void SubscribeToEvents()
    {
        _settings.OnMusicVolumeChanged += OnMusicVolumeChangedExternally;
        _settings.OnSFXVolumeChanged += OnSFXVolumeChangedExternally;
        _settings.OnFullscreenChanged += OnFullscreenChangedExternally;
        _settings.OnResolutionScaleChanged += OnResolutionScaleChangedExternally;
        _settings.OnMouseSensitivityChanged += OnMouseSensitivityChangedExternally;
    }

    private void UnsubscribeFromEvents()
    {
        if (_settings == null) return;
        _settings.OnMusicVolumeChanged -= OnMusicVolumeChangedExternally;
        _settings.OnSFXVolumeChanged -= OnSFXVolumeChangedExternally;
        _settings.OnFullscreenChanged -= OnFullscreenChangedExternally;
        _settings.OnResolutionScaleChanged -= OnResolutionScaleChangedExternally;
        _settings.OnMouseSensitivityChanged -= OnMouseSensitivityChangedExternally;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void RefreshAllControls()
    {
        SetMusicUI(_settings.MusicVolume);
        SetSFXUI(_settings.SFXVolume);
        SetFullscreenUI(_settings.IsFullscreen);
        SetResolutionUI(_settings.ResolutionScaleIndex);
        SetMouseSensitivityUI(_settings.MouseSensitivity);
    }

    private void InitialiseResolutionSlider()
    {
        if (resolutionScaleSlider == null) return;
        resolutionScaleSlider.minValue = 0;
        resolutionScaleSlider.maxValue = SettingsManager.ResolutionScaleValues.Length - 1;
        resolutionScaleSlider.wholeNumbers = true;
    }

    private void InitialiseMouseSensitivitySlider()
    {
        if (mouseSensitivitySlider == null) return;
        mouseSensitivitySlider.minValue = SettingsManager.MouseSensitivityMin;
        mouseSensitivitySlider.maxValue = SettingsManager.MouseSensitivityMax;
        mouseSensitivitySlider.wholeNumbers = false;
    }

    // ── UI Setters ────────────────────────────────────────────────────────────

    private void SetMusicUI(float value)
    {
        musicVolumeSlider?.SetValueWithoutNotify(value);
        if (musicVolumeLabel != null)
            musicVolumeLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    private void SetSFXUI(float value)
    {
        sfxVolumeSlider?.SetValueWithoutNotify(value);
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    private void SetFullscreenUI(bool value)
    {
        fullscreenToggle?.SetIsOnWithoutNotify(value);
    }

    private void SetResolutionUI(int index)
    {
        resolutionScaleSlider?.SetValueWithoutNotify(index);
        if (resolutionScaleLabel != null)
            resolutionScaleLabel.text = SettingsManager.ResolutionScaleLabels[index];
    }

    private void SetMouseSensitivityUI(float value)
    {
        mouseSensitivitySlider?.SetValueWithoutNotify(value);
        if (mouseSensitivityLabel != null)
            mouseSensitivityLabel.text = $"{value:F1}x";
    }

    // ── UI Event Handlers ─────────────────────────────────────────────────────

    private void OnMusicSliderChanged(float value) => _settings.SetMusicVolume(value);
    private void OnSFXSliderChanged(float value) => _settings.SetSFXVolume(value);
    private void OnFullscreenToggleChanged(bool value) => _settings.SetFullscreen(value);
    private void OnMouseSensitivitySliderChanged(float v) => _settings.SetMouseSensitivity(v);

    private void OnResolutionScaleSliderChanged(float value)
        => _settings.SetResolutionScaleIndex(Mathf.RoundToInt(value));

    private void OnResetBindingsClicked()
    {
        _settings.ResetAllBindings();
        BuildKeybindRows();
    }

    // ── SettingsManager Event Handlers ────────────────────────────────────────

    private void OnMusicVolumeChangedExternally(float v) => SetMusicUI(v);
    private void OnSFXVolumeChangedExternally(float v) => SetSFXUI(v);
    private void OnFullscreenChangedExternally(bool v) => SetFullscreenUI(v);
    private void OnResolutionScaleChangedExternally(int i) => SetResolutionUI(i);
    private void OnMouseSensitivityChangedExternally(float v) => SetMouseSensitivityUI(v);

    // ── Keybind Row Builder ───────────────────────────────────────────────────

    private void BuildKeybindRows()
    {
        if (keybindContainer == null || rebindRowPrefab == null) return;

        // Destroy old rows
        foreach (var row in _rebindRows)
            if (row != null) Destroy(row.gameObject);
        _rebindRows.Clear();

        var actions = _settings.GetInputActions();
        if (actions == null) return;

        foreach (var map in actions.actionMaps)
        {
            foreach (var action in map.actions)
            {
                // Skip mouse-driven actions that can't be meaningfully rebound here
                if (SkippedActions.Contains(action.name)) continue;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];

                    // Skip composite *roots* (e.g. "2D Vector") — show the parts instead
                    if (binding.isComposite) continue;

                    // Build a human-readable label for this row
                    string displayName = BuildDisplayName(action, binding);

                    var row = Instantiate(rebindRowPrefab, keybindContainer);
                    row.Initialise(action, i, displayName);
                    _rebindRows.Add(row);
                }
            }
        }
    }

    /// <summary>
    /// Returns a player-facing label for a binding row.
    /// Composite parts are shown as "{ActionFriendlyName}: {PartName}"
    /// (e.g. "Move: Up"), while standalone bindings use the friendly action name.
    /// </summary>
    private static string BuildDisplayName(InputAction action, InputBinding binding)
    {
        string actionFriendly = RebindActionUI.FriendlyActionName(action.name);

        if (binding.isPartOfComposite)
        {
            // binding.name is e.g. "Up", "Down", "Left", "Right", "Positive", etc.
            string partName = CapitaliseFirst(binding.name);
            return $"{actionFriendly}: {partName}";
        }

        return actionFriendly;
    }

    private static string CapitaliseFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// Persistent singleton that owns all game settings (audio, display, keybinds).
/// Place on a GameObject in your first/boot scene. It survives scene loads.
/// Requires: an AudioMixer with exposed parameters "MusicVolume" and "SFXVolume".
/// </summary>
[DefaultExecutionOrder(-100)]
public class SettingsManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static SettingsManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Audio Mixer")]
    [Tooltip("AudioMixer with exposed 'MusicVolume' and 'SFXVolume' parameters.")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Input Actions")]
    [Tooltip("The PlayerInputActions asset used by your game.")]
    [SerializeField] private InputActionAsset inputActions;

    // ── Resolution Scale Options ──────────────────────────────────────────────

    /// <summary>Render / window scale fractions applied to the native resolution.</summary>
    public static readonly float[] ResolutionScaleValues = { 0.50f, 0.667f, 0.75f, 1.00f, 1.25f, 1.50f };
    public static readonly string[] ResolutionScaleLabels = { "50%", "67%", "75%", "100%", "125%", "150%" };
    public const int DefaultResolutionScaleIndex = 3; // 100 %

    // ── Public State ──────────────────────────────────────────────────────────

    public float MusicVolume { get; private set; } = 1f;
    public float SFXVolume { get; private set; } = 1f;
    public bool IsFullscreen { get; private set; } = true;
    public int ResolutionScaleIndex { get; private set; } = DefaultResolutionScaleIndex;

    // ── Events (used by SettingsUI to stay in sync) ───────────────────────────

    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    public event Action<bool> OnFullscreenChanged;
    public event Action<int> OnResolutionScaleChanged;
    public event Action OnBindingsChanged;

    // ── PlayerPrefs Keys ──────────────────────────────────────────────────────

    private const string KeyMusic = "Settings_MusicVolume";
    private const string KeySFX = "Settings_SFXVolume";
    private const string KeyFullscreen = "Settings_Fullscreen";
    private const string KeyResSca = "Settings_ResolutionScale";
    private const string KeyBindings = "Settings_KeybindOverrides";

    // ── Native Resolution Cache ───────────────────────────────────────────────

    private int _nativeWidth;
    private int _nativeHeight;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheNativeResolution();
        LoadSettings();
        ApplyAllSettings();
    }

    private void CacheNativeResolution()
    {
        Resolution[] all = Screen.resolutions;
        if (all.Length > 0)
        {
            // The last entry is typically the highest supported resolution
            _nativeWidth = all[^1].width;
            _nativeHeight = all[^1].height;
        }
        else
        {
            _nativeWidth = Screen.currentResolution.width;
            _nativeHeight = Screen.currentResolution.height;
        }
    }

    // ── Music Volume ──────────────────────────────────────────────────────────

    /// <param name="value">Linear 0–1 volume.</param>
    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
        PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
        OnMusicVolumeChanged?.Invoke(MusicVolume);
    }

    private void ApplyMusicVolume()
    {
        if (audioMixer != null)
            audioMixer.SetFloat("MusicVolume", LinearToDecibel(MusicVolume));
    }

    // ── SFX Volume ────────────────────────────────────────────────────────────

    /// <param name="value">Linear 0–1 volume.</param>
    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        ApplySFXVolume();
        PlayerPrefs.SetFloat(KeySFX, SFXVolume);
        OnSFXVolumeChanged?.Invoke(SFXVolume);
    }

    private void ApplySFXVolume()
    {
        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", LinearToDecibel(SFXVolume));
    }

    // ── Fullscreen ────────────────────────────────────────────────────────────

    public void SetFullscreen(bool value)
    {
        IsFullscreen = value;
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(KeyFullscreen, IsFullscreen ? 1 : 0);
        OnFullscreenChanged?.Invoke(IsFullscreen);
    }

    // ── Resolution Scale ──────────────────────────────────────────────────────
    public void SetResolutionScaleIndex(int index)
    {
        ResolutionScaleIndex = Mathf.Clamp(index, 0, ResolutionScaleValues.Length - 1);
        ApplyResolutionScale();
        PlayerPrefs.SetInt(KeyResSca, ResolutionScaleIndex);
        OnResolutionScaleChanged?.Invoke(ResolutionScaleIndex);
    }

    private void ApplyResolutionScale()
    {
        float scale = ResolutionScaleValues[ResolutionScaleIndex];
        int w = Mathf.Max(1, Mathf.RoundToInt(_nativeWidth * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(_nativeHeight * scale));
        Screen.SetResolution(w, h, Screen.fullScreen);
    }

    // ── Keybinds ──────────────────────────────────────────────────────────────

    /// <summary>Returns the InputActionAsset so UI scripts can iterate actions.</summary>
    public InputActionAsset GetInputActions() => inputActions;

    /// <summary>Serialises all current binding overrides to PlayerPrefs.</summary>
    public void SaveBindingOverrides()
    {
        if (inputActions == null) return;
        PlayerPrefs.SetString(KeyBindings, inputActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }

    /// <summary>Removes all overrides and clears the saved data.</summary>
    public void ResetAllBindings()
    {
        if (inputActions == null) return;

        foreach (var map in inputActions.actionMaps)
            map.RemoveAllBindingOverrides();

        PlayerPrefs.DeleteKey(KeyBindings);
        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 1f);
        SFXVolume = PlayerPrefs.GetFloat(KeySFX, 1f);
        IsFullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
        ResolutionScaleIndex = PlayerPrefs.GetInt(KeyResSca, DefaultResolutionScaleIndex);
        ResolutionScaleIndex = Mathf.Clamp(ResolutionScaleIndex, 0, ResolutionScaleValues.Length - 1);

        if (inputActions != null && PlayerPrefs.HasKey(KeyBindings))
        {
            string json = PlayerPrefs.GetString(KeyBindings, string.Empty);
            if (!string.IsNullOrEmpty(json))
                inputActions.LoadBindingOverridesFromJson(json);
        }
    }

    private void ApplyAllSettings()
    {
        ApplyMusicVolume();
        ApplySFXVolume();
        Screen.fullScreen = IsFullscreen;
        ApplyResolutionScale();
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    /// <summary>Converts a linear 0–1 gain to decibels for AudioMixer.</summary>
    private static float LinearToDecibel(float linear) =>
        linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
}
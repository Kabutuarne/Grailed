using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles a single keybind row in the Settings → Keybinds panel.
/// Instantiated at runtime by <see cref="SettingsUI"/>.
///
/// Required GameObject hierarchy:
///   RebindRow (RebindActionUI)
///   ├── ActionNameLabel  (TMP_Text)
///   ├── RebindButton     (Button)
///   │   └── BindingLabel (TMP_Text)  ← child of button
///   └── ListeningOverlay (GameObject) — "Press a key…" message, disabled by default
/// </summary>
public class RebindActionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text actionNameLabel;
    [SerializeField] private Button rebindButton;
    [SerializeField] private TMP_Text bindingLabel;      // Shows current key
    [SerializeField] private GameObject listeningOverlay; // "Listening…" state

    // ── Friendly Name Dictionary ──────────────────────────────────────────────

    /// <summary>
    /// Maps raw InputAction names to player-facing strings.
    /// Composite-part rows are handled by SettingsUI and passed in via
    /// <see cref="Initialise"/>, so only the top-level action name is needed here.
    /// </summary>
    private static readonly Dictionary<string, string> FriendlyNames
        = new(System.StringComparer.OrdinalIgnoreCase)
    {
        // ── Movement ──────────────────────────────────────
        { "Move",        "Move"               }, // parts shown as "Move: Up" etc.
        { "Jump",        "Jump"               },
        { "Sprint",      "Sprint"             },
        { "Crouch",      "Crouch"             },

        // ── Interaction ───────────────────────────────────
        { "Interact",    "Interact"           },
        { "Cast",        "Cast Spell"         },
        { "Consume",     "Consume Item"           },
        { "DropItem",    "Drop Item"          },

        // ── Inventory / UI ────────────────────────────────
        { "Backpack",    "Toggle Backpack"    },
        { "SpellScroll", "Select Next/Previous Spell" },
        { "Pause",       "Pause"             },
    };

    // ── Internal State ────────────────────────────────────────────────────────

    private InputAction _action;
    private int _bindingIndex;
    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        rebindButton?.onClick.AddListener(StartRebind);
        SetListeningState(false);
    }

    private void OnDestroy()
    {
        // Always dispose to avoid native memory leaks
        _rebindOperation?.Dispose();
    }

    // ── Public Initialisation ─────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="SettingsUI"/> after instantiation.
    /// </summary>
    /// <param name="action">The InputAction this row controls.</param>
    /// <param name="bindingIndex">Index into <paramref name="action"/>.bindings.</param>
    /// <param name="displayName">
    /// Pre-built human-readable label supplied by SettingsUI
    /// (e.g. "Move: Up" for composite parts, or "Toggle Backpack" for a normal action).
    /// If null or empty, the row falls back to auto-formatting the action name.
    /// </param>
    public void Initialise(InputAction action, int bindingIndex, string displayName = null)
    {
        _action = action;
        _bindingIndex = bindingIndex;

        if (actionNameLabel != null)
        {
            actionNameLabel.text = string.IsNullOrEmpty(displayName)
                ? FriendlyActionName(action.name)
                : displayName;
        }

        RefreshBindingDisplay();
    }

    // ── Rebind Flow ───────────────────────────────────────────────────────────

    private void StartRebind()
    {
        if (_action == null) return;

        // Disable the action so it doesn't fire during rebinding
        _action.Disable();
        SetListeningState(true);

        _rebindOperation = _action
            .PerformInteractiveRebinding(_bindingIndex)
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(OnRebindComplete)
            .OnCancel(OnRebindCancelled)
            .Start();
    }

    private void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation op)
    {
        FinishRebind();
        SettingsManager.Instance?.SaveBindingOverrides();
        RefreshBindingDisplay();
    }

    private void OnRebindCancelled(InputActionRebindingExtensions.RebindingOperation op)
    {
        FinishRebind();
        RefreshBindingDisplay(); // Restore previous display
    }

    private void FinishRebind()
    {
        _rebindOperation?.Dispose();
        _rebindOperation = null;
        _action?.Enable();
        SetListeningState(false);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    private void RefreshBindingDisplay()
    {
        if (_action == null || bindingLabel == null) return;

        string display = _action.GetBindingDisplayString(
            _bindingIndex,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);

        bindingLabel.text = string.IsNullOrEmpty(display) ? "<None>" : display;
    }

    private void SetListeningState(bool listening)
    {
        if (listeningOverlay != null) listeningOverlay.SetActive(listening);
        if (rebindButton != null) rebindButton.interactable = !listening;
        if (bindingLabel != null && listening) bindingLabel.text = "…";
    }

    // ── Name Formatting (public so SettingsUI can use it too) ─────────────────

    /// <summary>
    /// Returns the friendly display name for a raw action name.
    /// Falls back to auto-formatting (camelCase → "Camel Case") if no
    /// explicit mapping exists in <see cref="FriendlyNames"/>.
    /// </summary>
    public static string FriendlyActionName(string rawActionName)
    {
        if (FriendlyNames.TryGetValue(rawActionName, out string friendly))
            return friendly;

        // Auto-format: insert spaces before uppercase letters and replace underscores
        string spaced = Regex.Replace(rawActionName, @"(\B[A-Z])", " $1");
        return spaced.Replace('_', ' ');
    }
}
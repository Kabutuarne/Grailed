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

    /// <summary>Called by SettingsUI after instantiation.</summary>
    public void Initialise(InputAction action, int bindingIndex)
    {
        _action = action;
        _bindingIndex = bindingIndex;

        if (actionNameLabel != null)
        {
            var binding = action.bindings[bindingIndex];
            // Use composite name (e.g. "Move/Up") or plain action name
            string rawName = binding.isComposite ? $"{action.name} ({binding.name})" : action.name;
            actionNameLabel.text = FormatName(rawName);
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

    /// <summary>Converts "MoveLeft" / "move_left" → "Move Left".</summary>
    private static string FormatName(string raw)
    {
        // Insert space before uppercase letters (camelCase → "Camel Case")
        string spaced = Regex.Replace(raw, @"(\B[A-Z])", " $1");
        // Replace underscores with spaces
        return spaced.Replace('_', ' ');
    }
}
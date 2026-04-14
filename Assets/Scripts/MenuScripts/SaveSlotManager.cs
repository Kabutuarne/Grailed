using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ── Save Data Model ───────────────────────────────────────────────────────────

/// <summary>
/// All data written to disk for a single save slot.
/// </summary>
[Serializable]
public class SaveData
{
    public bool isEmpty = true;
    public string saveName = "";
    public string timestamp = "";        // Human-readable creation date
    public float playTimeSeconds = 0f;

    // ── Game data fields ─────────────────────────────────────────────
    // public float   playerHealth;
    // public Vector3 playerPosition;
    // public int levelSeed;
    // etc.
}

// ── Static Cross-Scene Context ────────────────────────────────────────────────

/// <summary>
/// Carries the chosen slot index between the Main Menu and Game scenes.
/// Access via <c>SaveSlotContext.SelectedSlot</c> from anywhere.
/// </summary>
public static class SaveSlotContext
{
    /// <summary>Index of the save slot currently in use. -1 = none selected.</summary>
    public static int SelectedSlot { get; set; } = -1;

    /// <summary>
    /// Helper: load and return the active slot's SaveData.
    /// Returns null if no slot is selected or the file is missing.
    /// </summary>
    public static SaveData LoadActiveSave()
    {
        if (SelectedSlot < 0) return null;
        string path = SaveSlotManager.GetSavePath(SelectedSlot);
        if (!File.Exists(path)) return null;

        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
        catch { return null; }
    }

    /// <summary>
    /// Helper: write SaveData to the active slot's file.
    /// </summary>
    public static void WriteActiveSave(SaveData data)
    {
        if (SelectedSlot < 0 || data == null) return;
        SaveSlotManager.WriteSaveToDisk(SelectedSlot, data);
    }
}

// ── Save Slot Manager ─────────────────────────────────────────────────────────

/// <summary>
/// Manages the 6-slot save system in the Main Menu.
///
/// Inspector setup:
///  • slotButtons      — 6 buttons, one per slot
///  • slotNameLabels   — 6 TMP_Text labels (slot title)
///  • slotInfoLabels   — 6 TMP_Text labels (date + play-time)
///  • newSaveButton    — shown for ANY selected slot
///  • playButton       — shown only when selected slot has a save
///  • deleteSaveButton — shown only when selected slot has a save
///  • newSaveDialog / confirmDeleteDialog — confirmation popups
/// </summary>
public class SaveSlotManager : MonoBehaviour
{
    public const int SlotCount = 6;

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Slot UI (must have exactly 6 entries)")]
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotNameLabels;
    [SerializeField] private TMP_Text[] slotInfoLabels;

    [Header("Action Buttons")]
    [SerializeField] private Button newSaveButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button deleteSaveButton;

    [Header("New Save Dialog")]
    [SerializeField] private GameObject newSaveDialog;
    [SerializeField] private TMP_InputField saveNameInput;
    [SerializeField] private Button confirmNewSaveButton;
    [SerializeField] private Button cancelNewSaveButton;

    [Header("Confirm Delete Dialog")]
    [SerializeField] private GameObject confirmDeleteDialog;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;

    [Header("Scene")]
    [Tooltip("Exact name of the scene to load when starting/continuing a game.")]
    [SerializeField] private string gameSceneName = "CabinScene";

    // ── Colours ───────────────────────────────────────────────────────────────

    [Header("Slot Highlight Colours")]
    [SerializeField] private Color slotNormalColor = Color.white;
    [SerializeField] private Color slotSelectedColor = new Color(0.75f, 0.90f, 1f);

    // ── Internal State ────────────────────────────────────────────────────────

    private SaveData[] _slots = new SaveData[SlotCount];
    private int _selectedSlot = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        ValidateSlotArrays();
        LoadAllSlots();
        WireButtonListeners();
    }

    private void Start()
    {
        RefreshAllSlotUI();
        SetActionButtonsActive(false, false, false);
        HideAllDialogs();
    }

    // ── Slot Selection ────────────────────────────────────────────────────────

    private void SelectSlot(int index)
    {
        _selectedSlot = index;
        UpdateSlotHighlights();

        bool hasSave = !_slots[index].isEmpty;
        // New Save  → always available once a slot is selected
        // Play/Delete → only when the slot already has data
        SetActionButtonsActive(newSave: true, play: hasSave, delete: hasSave);
    }

    private void UpdateSlotHighlights()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            var cb = slotButtons[i].colors;
            cb.normalColor = (i == _selectedSlot) ? slotSelectedColor : slotNormalColor;
            slotButtons[i].colors = cb;
        }
    }

    // ── Action Button Handlers ────────────────────────────────────────────────

    private void OnNewSaveClicked()
    {
        if (_selectedSlot < 0) return;
        if (newSaveDialog != null)
        {
            newSaveDialog.SetActive(true);
            if (saveNameInput != null)
                saveNameInput.text = $"Save {_selectedSlot + 1}";
        }
    }

    private void OnConfirmNewSave()
    {
        if (_selectedSlot < 0) return;

        string name = (saveNameInput != null && !string.IsNullOrWhiteSpace(saveNameInput.text))
            ? saveNameInput.text.Trim()
            : $"Save {_selectedSlot + 1}";

        var data = new SaveData
        {
            isEmpty = false,
            saveName = name,
            timestamp = DateTime.Now.ToString("MMM dd, yyyy  HH:mm"),
            playTimeSeconds = 0f
        };

        _slots[_selectedSlot] = data;
        WriteSaveToDisk(_selectedSlot, data);
        RefreshSlotUI(_selectedSlot);
        HideAllDialogs();

        LoadGame(_selectedSlot);
    }

    private void OnPlayClicked()
    {
        if (_selectedSlot < 0 || _slots[_selectedSlot].isEmpty) return;
        LoadGame(_selectedSlot);
    }

    private void OnDeleteSaveClicked()
    {
        if (_selectedSlot < 0 || _slots[_selectedSlot].isEmpty) return;
        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(true);
    }

    private void OnConfirmDelete()
    {
        if (_selectedSlot < 0) return;

        DeleteSlotFromDisk(_selectedSlot);
        _slots[_selectedSlot] = new SaveData();
        RefreshSlotUI(_selectedSlot);
        HideAllDialogs();

        // Slot is still selected but now empty → only New Save is valid
        SetActionButtonsActive(newSave: true, play: false, delete: false);
    }

    // ── UI Refresh ────────────────────────────────────────────────────────────

    private void RefreshAllSlotUI()
    {
        for (int i = 0; i < SlotCount; i++)
            RefreshSlotUI(i);
    }

    private void RefreshSlotUI(int i)
    {
        var data = _slots[i];
        bool hasSave = !data.isEmpty;

        if (slotNameLabels != null && i < slotNameLabels.Length && slotNameLabels[i] != null)
            slotNameLabels[i].text = hasSave ? data.saveName : $"Empty Slot {i + 1}";

        if (slotInfoLabels != null && i < slotInfoLabels.Length && slotInfoLabels[i] != null)
            slotInfoLabels[i].text = hasSave
                ? $"{data.timestamp}   •   {FormatPlayTime(data.playTimeSeconds)}"
                : string.Empty;
    }

    private void SetActionButtonsActive(bool newSave, bool play, bool delete)
    {
        if (newSaveButton != null) newSaveButton.gameObject.SetActive(newSave);
        if (playButton != null) playButton.gameObject.SetActive(play);
        if (deleteSaveButton != null) deleteSaveButton.gameObject.SetActive(delete);
    }

    private void HideAllDialogs()
    {
        if (newSaveDialog != null) newSaveDialog.SetActive(false);
        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
    }

    // ── Scene Loading ─────────────────────────────────────────────────────────

    private void LoadGame(int slotIndex)
    {
        SaveSlotContext.SelectedSlot = slotIndex;
        SceneManager.LoadScene(gameSceneName);
    }

    // ── Disk I/O (internal) ───────────────────────────────────────────────────

    private void LoadAllSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            string path = GetSavePath(i);
            if (!File.Exists(path))
            {
                _slots[i] = new SaveData();
                continue;
            }
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                _slots[i] = data ?? new SaveData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSlotManager] Could not read slot {i}: {ex.Message}");
                _slots[i] = new SaveData();
            }
        }
    }

    private void DeleteSlotFromDisk(int index)
    {
        string path = GetSavePath(index);
        if (!File.Exists(path)) return;
        try { File.Delete(path); }
        catch (Exception ex) { Debug.LogError($"[SaveSlotManager] Delete slot {index} failed: {ex.Message}"); }
    }

    // ── Disk I/O (public static — callable from gameplay code) ────────────────

    /// <summary>Writes a SaveData object to the specified slot file.</summary>
    public static void WriteSaveToDisk(int slotIndex, SaveData data)
    {
        string path = GetSavePath(slotIndex);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSlotManager] Write slot {slotIndex} failed: {ex.Message}");
        }
    }

    /// <summary>Returns the full file path for a given slot index.</summary>
    public static string GetSavePath(int slotIndex) =>
        Path.Combine(Application.persistentDataPath, "Saves", $"slot_{slotIndex}.json");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void WireButtonListeners()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            int captured = i;
            slotButtons[i]?.onClick.AddListener(() => SelectSlot(captured));
        }

        newSaveButton?.onClick.AddListener(OnNewSaveClicked);
        playButton?.onClick.AddListener(OnPlayClicked);
        deleteSaveButton?.onClick.AddListener(OnDeleteSaveClicked);

        confirmNewSaveButton?.onClick.AddListener(OnConfirmNewSave);
        cancelNewSaveButton?.onClick.AddListener(HideAllDialogs);

        confirmDeleteButton?.onClick.AddListener(OnConfirmDelete);
        cancelDeleteButton?.onClick.AddListener(HideAllDialogs);
    }

    private void ValidateSlotArrays()
    {
        if (slotButtons == null || slotButtons.Length != SlotCount)
            Debug.LogError($"[SaveSlotManager] slotButtons must have exactly {SlotCount} entries.");
        if (slotNameLabels == null || slotNameLabels.Length != SlotCount)
            Debug.LogError($"[SaveSlotManager] slotNameLabels must have exactly {SlotCount} entries.");
        if (slotInfoLabels == null || slotInfoLabels.Length != SlotCount)
            Debug.LogError($"[SaveSlotManager] slotInfoLabels must have exactly {SlotCount} entries.");
    }

    private static string FormatPlayTime(float seconds)
    {
        int h = (int)(seconds / 3600f);
        int m = (int)(seconds % 3600f / 60f);
        return h > 0 ? $"{h}h {m:D2}m" : $"{m}m";
    }
}
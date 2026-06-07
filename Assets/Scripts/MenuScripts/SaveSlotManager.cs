using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the 6-slot save UI in the Main Menu.
///
/// </summary>
public class SaveSlotManager : MonoBehaviour
{
    //  Inspector 

    [Header("Slot UI (exactly 6 entries each)")]
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotNameLabels;
    [SerializeField] private TMP_Text[] slotInfoLabels;

    [Header("Action Buttons")]
    [SerializeField] private Button newSaveButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button deleteSaveButton;

    [Header("New Save Dialog")]
    [SerializeField] private GameObject newSaveDialog;
    [SerializeField] private GameObject newSaveTitleSection;
    [SerializeField] private GameObject newSaveAttributesSection;
    [SerializeField] private TMP_InputField saveNameInput;

    [Header("Attribute Sliders")]
    [SerializeField] private Slider intelligenceSlider;
    [SerializeField] private TMP_Text intelligenceValueLabel;
    [SerializeField] private Slider strengthSlider;
    [SerializeField] private TMP_Text strengthValueLabel;
    [SerializeField] private Slider agilitySlider;
    [SerializeField] private TMP_Text agilityValueLabel;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TMP_Text staminaValueLabel;
    [SerializeField] private TMP_Text remainingPointsLabel;

    [Header("New Save Dialog Buttons")]
    [SerializeField] private Button nextSectionButton;    // Title → Attributes
    [SerializeField] private Button confirmNewSaveButton;
    [SerializeField] private Button cancelNewSaveButton;

    [Header("Confirm Delete Dialog")]
    [SerializeField] private GameObject confirmDeleteDialog;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;

    [Header("Slot Highlight Colours")]
    [SerializeField] private Color slotNormalColor = Color.white;
    [SerializeField] private Color slotSelectedColor = new Color(0.75f, 0.90f, 1f);

    //  Attribute constraints 

    private const int AttrMin = 1;
    private const int AttrMax = 10;
    private const int AttrTotalMax = 29;   // points the player can distribute

    //  Internal state 

    private int _selectedSlot = -1;
    private bool _suppressSliderCallbacks;

    //  Lifecycle 

    private void Awake()
    {
        ValidateArrays();
        WireListeners();
        SetupAttributeSliders();
    }

    private void Start()
    {
        RefreshAllSlotUI();
        SetActionButtonsVisible(false, false, false);
        HideAllDialogs();
    }

    //  Slot selection 

    private void SelectSlot(int index)
    {
        _selectedSlot = index;
        UpdateHighlights();

        bool hasSave = !GameSaveManager.Instance.GetSlotData(index).isEmpty;
        SetActionButtonsVisible(newSave: true, play: hasSave, delete: hasSave);
    }

    private void UpdateHighlights()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            var cb = slotButtons[i].colors;
            cb.normalColor = (i == _selectedSlot) ? slotSelectedColor : slotNormalColor;
            slotButtons[i].colors = cb;
        }
    }

    //  New Save flow 

    private void OnNewSaveClicked()
    {
        if (_selectedSlot < 0) return;

        // Pre-fill name
        if (saveNameInput != null)
            saveNameInput.text = $"Save {_selectedSlot + 1}";

        // Pre-fill sliders from existing slot or balanced defaults
        var slot = GameSaveManager.Instance.GetSlotData(_selectedSlot);
        if (!slot.isEmpty)
        {
            SetSlidersSilently(slot.intelligence, slot.strength, slot.agility, slot.staminaAttr);
        }
        else
        {
            // Distribute AttrTotalMax as evenly as possible
            int baseVal = AttrTotalMax / 4;   // 7
            int remainder = AttrTotalMax % 4;   // 1

            float iv = baseVal + (remainder-- > 0 ? 1 : 0);
            float sv = baseVal + (remainder-- > 0 ? 1 : 0);
            float av = baseVal + (remainder-- > 0 ? 1 : 0);
            float stv = baseVal + (remainder > 0 ? 1 : 0);
            SetSlidersSilently(iv, sv, av, stv);
        }

        UpdateAttributeLabels();

        if (newSaveDialog != null) newSaveDialog.SetActive(true);
        if (newSaveTitleSection != null) newSaveTitleSection.SetActive(true);
        if (newSaveAttributesSection != null) newSaveAttributesSection.SetActive(false);
    }

    private void OnNextSectionClicked()
    {
        if (newSaveTitleSection != null) newSaveTitleSection.SetActive(false);
        if (newSaveAttributesSection != null) newSaveAttributesSection.SetActive(true);
    }

    private void OnConfirmNewSave()
    {
        if (_selectedSlot < 0) return;

        string name = (saveNameInput != null && !string.IsNullOrWhiteSpace(saveNameInput.text))
            ? saveNameInput.text.Trim()
            : $"Save {_selectedSlot + 1}";

        float iv = intelligenceSlider != null ? intelligenceSlider.value : 10f;
        float sv = strengthSlider != null ? strengthSlider.value : 10f;
        float av = agilitySlider != null ? agilitySlider.value : 10f;
        float stv = staminaSlider != null ? staminaSlider.value : 10f;

        // This writes the save and loads CabinScene.
        GameSaveManager.Instance.CreateNewSave(_selectedSlot, name, iv, sv, av, stv);

        HideAllDialogs();
    }

    //  Play 

    private void OnPlayClicked()
    {
        if (_selectedSlot < 0) return;
        if (GameSaveManager.Instance.GetSlotData(_selectedSlot).isEmpty) return;

        GameSaveManager.Instance.LoadSlotIntoGame(_selectedSlot);
    }

    //  Delete flow 

    private void OnDeleteClicked()
    {
        if (_selectedSlot < 0) return;
        if (GameSaveManager.Instance.GetSlotData(_selectedSlot).isEmpty) return;
        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(true);
    }

    private void OnConfirmDelete()
    {
        if (_selectedSlot < 0) return;
        GameSaveManager.Instance.DeleteSlot(_selectedSlot);
        RefreshSlotUI(_selectedSlot);
        HideAllDialogs();
        // Slot is still selected but now empty
        SetActionButtonsVisible(newSave: true, play: false, delete: false);
    }

    //  UI refresh 

    private void RefreshAllSlotUI()
    {
        for (int i = 0; i < GameSaveManager.SlotCount; i++)
            RefreshSlotUI(i);
    }

    private void RefreshSlotUI(int i)
    {
        var data = GameSaveManager.Instance.GetSlotData(i);
        bool hasSave = !data.isEmpty;

        if (slotNameLabels != null && i < slotNameLabels.Length && slotNameLabels[i] != null)
            slotNameLabels[i].text = hasSave ? data.saveName : $"Empty Slot {i + 1}";

        if (slotInfoLabels != null && i < slotInfoLabels.Length && slotInfoLabels[i] != null)
            slotInfoLabels[i].text = hasSave
                ? $"{data.timestamp}   •   {FormatPlayTime(data.playTimeSeconds)}"
                : string.Empty;
    }

    //  Attribute sliders 

    private void SetupAttributeSliders()
    {
        Slider[] sliders = { intelligenceSlider, strengthSlider, agilitySlider, staminaSlider };
        for (int i = 0; i < sliders.Length; i++)
        {
            var s = sliders[i];
            if (s == null) continue;
            s.minValue = AttrMin;
            s.maxValue = AttrMax;
            s.wholeNumbers = true;
            int captured = i;
            s.onValueChanged.AddListener(_ => OnSliderChanged(captured));
        }
    }

    private void OnSliderChanged(int index)
    {
        if (_suppressSliderCallbacks) return;
        _suppressSliderCallbacks = true;

        var changed = GetSlider(index);
        if (changed != null)
        {
            float sumOthers = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i == index) continue;
                var s = GetSlider(i);
                if (s != null) sumOthers += s.value;
            }

            float allowed = Mathf.Max(AttrMin, AttrTotalMax - sumOthers);
            float clamped = Mathf.Clamp(changed.value, AttrMin, Mathf.Min(AttrMax, allowed));
            if (Mathf.Abs(changed.value - clamped) > 0.001f)
                changed.value = clamped;
        }

        UpdateAttributeLabels();
        _suppressSliderCallbacks = false;
    }

    private void UpdateAttributeLabels()
    {
        int iv = intelligenceSlider != null ? Mathf.RoundToInt(intelligenceSlider.value) : 0;
        int sv = strengthSlider != null ? Mathf.RoundToInt(strengthSlider.value) : 0;
        int av = agilitySlider != null ? Mathf.RoundToInt(agilitySlider.value) : 0;
        int stv = staminaSlider != null ? Mathf.RoundToInt(staminaSlider.value) : 0;

        if (intelligenceValueLabel != null) intelligenceValueLabel.text = iv.ToString();
        if (strengthValueLabel != null) strengthValueLabel.text = sv.ToString();
        if (agilityValueLabel != null) agilityValueLabel.text = av.ToString();
        if (staminaValueLabel != null) staminaValueLabel.text = stv.ToString();

        int remaining = AttrTotalMax - (iv + sv + av + stv);
        if (remainingPointsLabel != null) remainingPointsLabel.text = remaining.ToString();
    }

    private void SetSlidersSilently(float iv, float sv, float av, float stv)
    {
        _suppressSliderCallbacks = true;
        if (intelligenceSlider != null) intelligenceSlider.value = iv;
        if (strengthSlider != null) strengthSlider.value = sv;
        if (agilitySlider != null) agilitySlider.value = av;
        if (staminaSlider != null) staminaSlider.value = stv;
        _suppressSliderCallbacks = false;
    }

    private Slider GetSlider(int index) => index switch
    {
        0 => intelligenceSlider,
        1 => strengthSlider,
        2 => agilitySlider,
        3 => staminaSlider,
        _ => null
    };

    //  Helpers 

    private void SetActionButtonsVisible(bool newSave, bool play, bool delete)
    {
        if (newSaveButton != null) newSaveButton.gameObject.SetActive(newSave);
        if (playButton != null) playButton.gameObject.SetActive(play);
        if (deleteSaveButton != null) deleteSaveButton.gameObject.SetActive(delete);
    }

    private void HideAllDialogs()
    {
        if (newSaveDialog != null) newSaveDialog.SetActive(false);
        if (newSaveTitleSection != null) newSaveTitleSection.SetActive(false);
        if (newSaveAttributesSection != null) newSaveAttributesSection.SetActive(false);
        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
    }

    private void WireListeners()
    {
        for (int i = 0; i < GameSaveManager.SlotCount; i++)
        {
            int captured = i;
            slotButtons[i]?.onClick.AddListener(() => SelectSlot(captured));
        }

        newSaveButton?.onClick.AddListener(OnNewSaveClicked);
        playButton?.onClick.AddListener(OnPlayClicked);
        deleteSaveButton?.onClick.AddListener(OnDeleteClicked);

        nextSectionButton?.onClick.AddListener(OnNextSectionClicked);
        confirmNewSaveButton?.onClick.AddListener(OnConfirmNewSave);
        cancelNewSaveButton?.onClick.AddListener(HideAllDialogs);

        confirmDeleteButton?.onClick.AddListener(OnConfirmDelete);
        cancelDeleteButton?.onClick.AddListener(HideAllDialogs);
    }

    private void ValidateArrays()
    {
        int n = GameSaveManager.SlotCount;
        if (slotButtons == null || slotButtons.Length != n) Debug.LogError($"[SaveSlotManager] slotButtons must have {n} entries.");
        if (slotNameLabels == null || slotNameLabels.Length != n) Debug.LogError($"[SaveSlotManager] slotNameLabels must have {n} entries.");
        if (slotInfoLabels == null || slotInfoLabels.Length != n) Debug.LogError($"[SaveSlotManager] slotInfoLabels must have {n} entries.");
    }

    private static string FormatPlayTime(float seconds)
    {
        int h = (int)(seconds / 3600f);
        int m = (int)(seconds % 3600f / 60f);
        return h > 0 ? $"{h}h {m:D2}m" : $"{m}m";
    }
}
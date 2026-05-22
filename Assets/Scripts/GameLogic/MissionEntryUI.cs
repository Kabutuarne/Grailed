using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MissionEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text assignedByText;
    [SerializeField] private TMP_Text difficultyText; // Roman numeral I, II, III, etc. graded by difficulty
    [SerializeField] private Button selectButton;

    [Header("Selection Visuals")]
    [SerializeField] private GameObject glowObject;
    [SerializeField] private GameObject cornerGlowObject;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite normalBackground;
    [SerializeField] private Sprite selectedBackground;

    private MissionData mission;
    private bool _isSelected;
    private bool _isHovered;

    private void Awake()
    {
        UpdateVisualState();
    }

    public void Setup(MissionData mission, Action<MissionData, MissionEntryUI> onSelect)
    {
        this.mission = mission;

        if (titleText != null)
            titleText.text = mission != null ? mission.title : "Unknown Mission";

        if (assignedByText != null)
            assignedByText.text = mission != null ? $"Assigned by {mission.assignedBy}" : string.Empty;

        if (difficultyText != null)
            difficultyText.text = mission != null ? mission.DifficultyRoman : string.Empty;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke(this.mission, this));
        }

        _isSelected = false;
        _isHovered = false;
        UpdateVisualState();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        UpdateVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        bool glowOn = _isHovered || _isSelected;
        if (glowObject != null)
            glowObject.SetActive(glowOn);

        if (cornerGlowObject != null)
            cornerGlowObject.SetActive(_isSelected);

        if (backgroundImage != null)
            backgroundImage.sprite = _isSelected && selectedBackground != null ? selectedBackground : normalBackground;
    }
}

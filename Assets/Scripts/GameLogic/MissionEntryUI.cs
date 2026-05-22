using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text assignedByText;
    [SerializeField] private TMP_Text difficultyText; // Roman numeral I, II, III, etc. graded by difficulty
    [SerializeField] private Button selectButton;

    private MissionData mission;

    public void Setup(MissionData mission, Action<MissionData> onSelect)
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
            selectButton.onClick.AddListener(() => onSelect?.Invoke(this.mission));
        }
    }
}

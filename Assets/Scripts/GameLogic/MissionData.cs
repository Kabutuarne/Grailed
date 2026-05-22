using UnityEngine;

[CreateAssetMenu(fileName = "NewMission", menuName = "Dungeon/Mission")]
public class MissionData : ScriptableObject
{
    public enum Difficulty { Easy, Medium, Hard, Elite }

    [Header("Mission Info")]
    [Tooltip("Unique identifier used by the mission progression system.")]
    public string missionId;
    public string title;
    [TextArea(2, 4)]
    public string description;
    [Tooltip("Character or entity who assigned this mission.")]
    public string assignedBy;
    public Difficulty difficulty = Difficulty.Easy;

    public string DifficultyRoman => difficulty switch
    {
        Difficulty.Easy => "I",
        Difficulty.Medium => "II",
        Difficulty.Hard => "III",
        Difficulty.Elite => "IV",
        _ => "I",
    };

    [Header("Level")]
    [Tooltip("Scene name that will be loaded for this mission.")]
    public string sceneName = "Dungeon";
    [Tooltip("Level catalog used by the level generator for this mission.")]
    public PerLevelCatalog levelCatalog;

    /// <summary>Falls back to the asset name when missionId is blank.</summary>
    public string Id => string.IsNullOrWhiteSpace(missionId) ? name : missionId.Trim();
}
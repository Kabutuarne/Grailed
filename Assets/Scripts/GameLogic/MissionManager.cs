using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that owns all mission state and persists across scene loads.
/// Simplified to focus on the core flow: dialogue -> mission -> success/failure -> next dialogue
/// </summary>
public class MissionManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static MissionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<IReadOnlyCollection<MissionData>> OnAvailableMissionsChanged;
    public event Action<MissionData> OnMissionEnded;
    public event Action<MissionData> OnMissionStarted;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly HashSet<string> completedMissionIds = new HashSet<string>();
    private readonly HashSet<string> playedMissionIds = new HashSet<string>();
    private readonly HashSet<string> completedSequenceIds = new HashSet<string>();
    private readonly List<MissionData> availableMissions = new List<MissionData>();

    private MissionData currentMission;
    private MissionData lastCompletedMission;

    // ── Public API ────────────────────────────────────────────────────────────

    public IReadOnlyCollection<MissionData> GetAvailableMissions()
    {
        return availableMissions.AsReadOnly();
    }

    public void UnlockMission(MissionData mission)
    {
        if (mission == null) return;
        if (completedMissionIds.Contains(mission.Id)) return;
        if (availableMissions.Contains(mission)) return;

        availableMissions.Add(mission);
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
        Debug.Log($"[MissionManager] Mission unlocked: {mission.title}");
    }

    public void UnlockMissions(MissionData[] missions)
    {
        if (missions == null || missions.Length == 0) return;

        bool changed = false;
        foreach (var mission in missions)
        {
            if (mission == null) continue;
            if (completedMissionIds.Contains(mission.Id)) continue;
            if (availableMissions.Contains(mission)) continue;

            availableMissions.Add(mission);
            changed = true;
            Debug.Log($"[MissionManager] Mission unlocked: {mission.title}");
        }

        if (changed)
            OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
    }

    public void StartMission(MissionData mission)
    {
        if (mission == null) return;

        currentMission = mission;
        MarkMissionAsPlayed(mission);

        if (GameManager.Instance != null)
            GameManager.Instance.ActiveLevel = mission.levelCatalog;

        Debug.Log($"[MissionManager] Starting mission: {mission.title} → scene: {mission.sceneName}");
        OnMissionStarted?.Invoke(mission);
        SceneManager.LoadScene(mission.sceneName);
    }

    public void MarkMissionAsPlayed(MissionData mission)
    {
        if (mission != null)
            playedMissionIds.Add(mission.Id);
    }

    public bool HasMissionBeenPlayed(MissionData mission)
    {
        return mission != null && playedMissionIds.Contains(mission.Id);
    }

    public void EndCurrentMission()
    {
        if (currentMission == null) return;

        completedMissionIds.Add(currentMission.Id);
        lastCompletedMission = currentMission;
        availableMissions.Remove(currentMission);

        Debug.Log($"[MissionManager] Mission complete: {currentMission.title}");
        OnMissionEnded?.Invoke(currentMission);

        currentMission = null;
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
    }

    public void FailCurrentMission()
    {
        if (currentMission == null) return;

        // Don't add to completed, just end and notify
        Debug.Log($"[MissionManager] Mission failed: {currentMission.title}");
        var failedMission = currentMission;
        currentMission = null;

        // Notify listeners that the mission ended (they can check completion status)
        OnMissionEnded?.Invoke(failedMission);
    }

    public bool IsMissionComplete(MissionData mission)
    {
        return mission != null && completedMissionIds.Contains(mission.Id);
    }

    public bool IsMissionActive(MissionData mission)
    {
        return currentMission == mission;
    }

    public MissionData GetLastCompletedMission() => lastCompletedMission;

    // ── Sequence Management ───────────────────────────────────────────────────

    public bool HasSequenceBeenPlayed(DoorSequenceData sequence)
    {
        return sequence != null && completedSequenceIds.Contains(sequence.name);
    }

    public void MarkSequenceAsPlayed(DoorSequenceData sequence)
    {
        if (sequence != null)
            completedSequenceIds.Add(sequence.name);
    }

    public void ClearAllProgress()
    {
        completedMissionIds.Clear();
        playedMissionIds.Clear();
        completedSequenceIds.Clear();
        availableMissions.Clear();
        currentMission = null;
        lastCompletedMission = null;
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
        Debug.Log($"[MissionManager] All progress cleared.");
    }

    public MissionData CurrentMission => currentMission;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "CabinScene" && lastCompletedMission != null)
        {
            Debug.Log($"[MissionManager] Returned to lobby after mission: {lastCompletedMission.title}");
        }
    }
}
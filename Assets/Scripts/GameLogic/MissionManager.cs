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
    // =====================================================================
    // Singleton
    // =====================================================================

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

    // =====================================================================
    // Events
    // =====================================================================

    public event Action<IReadOnlyCollection<MissionData>> OnAvailableMissionsChanged;
    public event Action<MissionData> OnMissionEnded;
    public event Action<MissionData> OnMissionStarted;

    // =====================================================================
    // State
    // =====================================================================

    private readonly HashSet<string> completedMissionIds = new HashSet<string>();
    private readonly HashSet<string> playedMissionIds = new HashSet<string>();
    private readonly HashSet<string> completedSequenceIds = new HashSet<string>();
    private readonly List<MissionData> availableMissions = new List<MissionData>();

    private MissionData currentMission;
    private MissionData lastCompletedMission;

    // =====================================================================
    // Public API — mission queries
    // =====================================================================

    public IReadOnlyCollection<MissionData> GetAvailableMissions()
    {
        return availableMissions.AsReadOnly();
    }

    public bool IsMissionComplete(MissionData mission)
    {
        return mission != null && completedMissionIds.Contains(mission.Id);
    }

    public bool IsMissionActive(MissionData mission)
    {
        return currentMission == mission;
    }

    public bool HasMissionBeenPlayed(MissionData mission)
    {
        return mission != null && playedMissionIds.Contains(mission.Id);
    }

    public MissionData GetLastCompletedMission() => lastCompletedMission;

    public MissionData CurrentMission => currentMission;

    // =====================================================================
    // Public API — mission lifecycle
    // =====================================================================

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

        Debug.Log($"[MissionManager] Starting mission: {mission.title} -> scene: {mission.sceneName}");
        OnMissionStarted?.Invoke(mission);
        SceneManager.LoadScene(mission.sceneName);
    }

    public void MarkMissionAsPlayed(MissionData mission)
    {
        if (mission != null)
            playedMissionIds.Add(mission.Id);
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

        Debug.Log($"[MissionManager] Mission failed: {currentMission.title}");
        var failedMission = currentMission;
        currentMission = null;

        OnMissionEnded?.Invoke(failedMission);
    }

    // =====================================================================
    // Public API -- sequence management
    // =====================================================================

    public bool HasSequenceBeenPlayed(DoorSequenceData sequence)
    {
        return sequence != null && completedSequenceIds.Contains(sequence.name);
    }

    public void MarkSequenceAsPlayed(DoorSequenceData sequence)
    {
        if (sequence != null)
            completedSequenceIds.Add(sequence.name);
    }

    // =====================================================================
    // Public API -- progress reset
    // =====================================================================

    public void ClearAllProgress()
    {
        completedMissionIds.Clear();
        playedMissionIds.Clear();
        completedSequenceIds.Clear();
        availableMissions.Clear();
        currentMission = null;
        lastCompletedMission = null;
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
        Debug.Log("[MissionManager] All progress cleared.");
    }

    // =====================================================================
    // Public API -- save system integration (called by GameSaveManager)
    // =====================================================================

    /// <summary>
    /// Returns a snapshot list of the Ids of all currently available missions.
    /// Called by GameSaveManager when writing to disk.
    /// </summary>
    public List<string> GetAvailableMissionIds()
    {
        var ids = new List<string>(availableMissions.Count);
        foreach (var m in availableMissions)
            if (m != null) ids.Add(m.Id);
        return ids;
    }

    /// <summary>
    /// Returns a snapshot list of the Ids of all completed missions.
    /// Called by GameSaveManager when writing to disk.
    /// </summary>
    public List<string> GetCompletedMissionIds()
    {
        return new List<string>(completedMissionIds);
    }

    /// <summary>
    /// Returns a snapshot list of the Ids of all missions the player has started at least once.
    /// Called by GameSaveManager when writing to disk.
    /// </summary>
    public List<string> GetPlayedMissionIds()
    {
        return new List<string>(playedMissionIds);
    }

    /// <summary>
    /// Returns a snapshot list of all completed door sequence asset names.
    /// Called by GameSaveManager when writing to disk.
    /// </summary>
    public List<string> GetCompletedSequenceIds()
    {
        return new List<string>(completedSequenceIds);
    }

    /// <summary>
    /// Marks a mission as completed without firing any events.
    /// Used during save restore so listeners are not accidentally triggered.
    /// </summary>
    public void ForceMarkCompleted(MissionData mission)
    {
        if (mission == null) return;
        completedMissionIds.Add(mission.Id);
    }

    /// <summary>
    /// Marks a door sequence as completed by raw asset name.
    /// Used during save restore so the sequence is not replayed.
    /// </summary>
    public void ForceMarkSequenceCompleted(string sequenceAssetName)
    {
        if (!string.IsNullOrWhiteSpace(sequenceAssetName))
            completedSequenceIds.Add(sequenceAssetName);
    }

    // =====================================================================
    // Private -- scene events
    // =====================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "CabinScene" && lastCompletedMission != null)
        {
            Debug.Log($"[MissionManager] Returned to lobby after mission: {lastCompletedMission.title}");
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that owns all mission state and persists across scene loads.
///
/// Lifecycle:
///   Door dialogue finishes  →  UnlockMission(data)
///   Player opens board      →  GetAvailableMissions()
///   Player picks mission    →  StartMission(data)   — loads mission scene
///   Player returns to lobby →  EndCurrentMission()  — marks complete, fires event
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

        // Subscribe to scene loaded events to handle post-mission cleanup
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever the available mission list changes (unlock or completion).</summary>
    public event Action<IReadOnlyCollection<MissionData>> OnAvailableMissionsChanged;

    /// <summary>Fired when a mission is ended (completed).</summary>
    public event Action<MissionData> OnMissionEnded;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks which missions have been completed.
    /// Referenced by MissionData.IsAvailable().
    /// </summary>
    public class MissionProgress
    {
        private readonly HashSet<string> completedIds = new HashSet<string>();

        public void MarkComplete(string id) => completedIds.Add(id);
        public bool IsMissionComplete(string id) => completedIds.Contains(id);
        public void Clear() => completedIds.Clear();
    }

    private readonly MissionProgress progress = new MissionProgress();

    /// <summary>
    /// Tracks which missions have been played at least once (started, regardless of completion).
    /// Used for conditional dialogue after returning from a mission.
    /// </summary>
    private readonly HashSet<string> playedMissionIds = new HashSet<string>();

    // Missions the player has been told about but not yet completed.
    private readonly List<MissionData> availableMissions = new List<MissionData>();

    private MissionData currentMission;

    // Track the last completed mission for conditional dialogue purposes
    private MissionData lastCompletedMission;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns missions that are available (unlocked and not yet complete).
    /// Called by MissionPickerUI to populate the board.
    /// </summary>
    public IReadOnlyCollection<MissionData> GetAvailableMissions()
    {
        return availableMissions.AsReadOnly();
    }

    /// <summary>
    /// Unlocks a mission after its door dialogue finishes.
    /// Ignored if the mission is null, already available, or already complete.
    /// </summary>
    public void UnlockMission(MissionData mission)
    {
        if (mission == null)
            return;

        if (progress.IsMissionComplete(mission.Id))
            return;

        if (availableMissions.Contains(mission))
            return;

        availableMissions.Add(mission);
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());

        Debug.Log($"[MissionManager] Mission unlocked: {mission.title}");
    }

    /// <summary>
    /// Starts a mission — stores it as the active mission and loads its scene.
    /// Called by MissionPickerUI when the player presses "Start Mission".
    /// Also marks the mission as "played" for conditional dialogue purposes.
    /// </summary>
    public void StartMission(MissionData mission)
    {
        if (mission == null)
            return;

        currentMission = mission;

        // Mark this mission as played (has been started at least once)
        MarkMissionAsPlayed(mission);

        // Set the active level catalog so the level loader/generator can pick it up
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActiveLevel = mission.levelCatalog;
        }

        Debug.Log($"[MissionManager] Starting mission: {mission.title}  →  scene: {mission.sceneName}");
        SceneManager.LoadScene(mission.sceneName);
    }

    /// <summary>
    /// Marks a mission as having been played (started at least once).
    /// </summary>
    public void MarkMissionAsPlayed(MissionData mission)
    {
        if (mission == null)
            return;

        if (playedMissionIds.Add(mission.Id))
        {
            Debug.Log($"[MissionManager] Mission marked as played: {mission.title}");
        }
    }

    /// <summary>
    /// Checks if a mission has been played at least once.
    /// </summary>
    public bool HasMissionBeenPlayed(MissionData mission)
    {
        if (mission == null)
            return false;

        return playedMissionIds.Contains(mission.Id);
    }

    /// <summary>
    /// Ends the active mission and marks it complete.
    /// Call this before loading the lobby scene (e.g. from ReturnToLobbyInteractable).
    /// </summary>
    public void EndCurrentMission()
    {
        if (currentMission == null)
            return;

        string id = currentMission.Id;
        progress.MarkComplete(id);
        lastCompletedMission = currentMission;
        availableMissions.Remove(currentMission);

        Debug.Log($"[MissionManager] Mission complete: {currentMission.title}");

        OnMissionEnded?.Invoke(currentMission);

        currentMission = null;
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
    }

    /// <summary>
    /// Gets the last completed mission (useful for conditional dialogue after returning to lobby).
    /// </summary>
    public MissionData GetLastCompletedMission() => lastCompletedMission;

    /// <summary>
    /// Clears all mission progress (useful for new game or testing).
    /// </summary>
    public void ClearAllProgress()
    {
        progress.Clear();
        playedMissionIds.Clear();
        availableMissions.Clear();
        currentMission = null;
        lastCompletedMission = null;
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
        Debug.Log($"[MissionManager] All mission progress cleared.");
    }

    /// <summary>The mission currently in progress, or null if none.</summary>
    public MissionData CurrentMission => currentMission;

    /// <summary>Exposes progress for MissionData.IsAvailable().</summary>
    public MissionProgress Progress => progress;

    // ── Scene Handling ────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When returning to lobby (CabinScene), we can trigger any post-mission logic here
        if (scene.name == "CabinScene" && lastCompletedMission != null)
        {
            Debug.Log($"[MissionManager] Returned to lobby after completing mission: {lastCompletedMission.title}");
            // The conditional dialogue check will happen in DoorDialogueSequence when the door is interacted with
        }
    }
}
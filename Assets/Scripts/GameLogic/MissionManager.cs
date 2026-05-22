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
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever the available mission list changes (unlock or completion).</summary>
    public event Action<IReadOnlyCollection<MissionData>> OnAvailableMissionsChanged;

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
    }

    private readonly MissionProgress progress = new MissionProgress();

    // Missions the player has been told about but not yet completed.
    private readonly List<MissionData> availableMissions = new List<MissionData>();

    private MissionData currentMission;

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
    /// </summary>
    public void StartMission(MissionData mission)
    {
        if (mission == null)
            return;

        currentMission = mission;
        Debug.Log($"[MissionManager] Starting mission: {mission.title}  →  scene: {mission.sceneName}");
        SceneManager.LoadScene(mission.sceneName);
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
        availableMissions.Remove(currentMission);

        Debug.Log($"[MissionManager] Mission complete: {currentMission.title}");

        currentMission = null;
        OnAvailableMissionsChanged?.Invoke(availableMissions.AsReadOnly());
    }

    /// <summary>The mission currently in progress, or null if none.</summary>
    public MissionData CurrentMission => currentMission;

    /// <summary>Exposes progress for MissionData.IsAvailable().</summary>
    public MissionProgress Progress => progress;
}
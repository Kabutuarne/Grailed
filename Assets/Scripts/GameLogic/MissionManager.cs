using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    public event Action<IReadOnlyCollection<MissionData>> OnAvailableMissionsChanged;
    public event Action<MissionData> OnMissionEnded;
    public event Action<MissionData> OnMissionStarted;

    private readonly HashSet<string> completedMissionIds = new HashSet<string>();
    private readonly HashSet<string> playedMissionIds = new HashSet<string>();
    private readonly HashSet<string> completedSequenceIds = new HashSet<string>();
    private readonly HashSet<string> playedSequenceIds = new HashSet<string>();
    private readonly List<MissionData> unlockedMissions = new List<MissionData>();

    private MissionData currentMission;
    private MissionData lastCompletedMission;
    private string lastUnlockedMissionId = "";

    // Door progression
    private readonly Dictionary<string, string> doorCurrentSequenceNames = new Dictionary<string, string>();

    // ---------- Public queries ----------
    public IReadOnlyCollection<MissionData> GetUnlockedMissions() => unlockedMissions.AsReadOnly();
    public IReadOnlyCollection<MissionData> GetAvailableMissions() => unlockedMissions.AsReadOnly(); // alias
    public bool IsMissionComplete(MissionData mission) => mission != null && completedMissionIds.Contains(mission.Id);
    public bool IsMissionActive(MissionData mission) => currentMission == mission;
    public bool HasMissionBeenPlayed(MissionData mission) => mission != null && playedMissionIds.Contains(mission.Id);
    public MissionData GetLastCompletedMission() => lastCompletedMission;
    public MissionData CurrentMission => currentMission;
    public string GetLastUnlockedMissionId() => lastUnlockedMissionId;

    // ---------- Mission lifecycle ----------
    public void UnlockMission(MissionData mission)
    {
        if (mission == null) return;
        if (completedMissionIds.Contains(mission.Id)) return;
        if (unlockedMissions.Contains(mission)) return;

        unlockedMissions.Add(mission);
        lastUnlockedMissionId = mission.Id;
        OnAvailableMissionsChanged?.Invoke(unlockedMissions.AsReadOnly());
        Debug.Log($"[MissionManager] Mission unlocked: {mission.title}");
    }

    /// <summary>Unlock a mission without firing the event (for batch restore).</summary>
    public void UnlockMissionWithoutNotify(MissionData mission)
    {
        if (mission == null) return;
        if (completedMissionIds.Contains(mission.Id)) return;
        if (unlockedMissions.Contains(mission)) return;
        unlockedMissions.Add(mission);
        lastUnlockedMissionId = mission.Id;
        Debug.Log($"[MissionManager] Mission unlocked (silent): {mission.title}");
    }

    /// <summary>Fire the available missions changed event once (call after bulk operations).</summary>
    public void NotifyAvailableMissionsChanged()
    {
        OnAvailableMissionsChanged?.Invoke(unlockedMissions.AsReadOnly());
    }

    public void StartMission(MissionData mission)
    {
        if (mission == null) return;
        currentMission = mission;
        MarkMissionAsPlayed(mission);
        if (GameManager.Instance != null) GameManager.Instance.ActiveLevel = mission.levelCatalog;
        OnMissionStarted?.Invoke(mission);
        SceneManager.LoadScene(mission.sceneName);
    }

    public void MarkMissionAsPlayed(MissionData mission)
    {
        if (mission != null) playedMissionIds.Add(mission.Id);
    }

    public void EndCurrentMission()
    {
        if (currentMission == null) return;
        completedMissionIds.Add(currentMission.Id);
        lastCompletedMission = currentMission;
        unlockedMissions.Remove(currentMission);
        Debug.Log($"[MissionManager] Mission complete: {currentMission.title}");
        OnMissionEnded?.Invoke(currentMission);
        currentMission = null;
        NotifyAvailableMissionsChanged();
    }

    public void FailCurrentMission()
    {
        if (currentMission == null) return;
        Debug.Log($"[MissionManager] Mission failed: {currentMission.title}");
        var failed = currentMission;
        currentMission = null;
        OnMissionEnded?.Invoke(failed);
    }

    // ---------- Sequences ----------
    public bool HasSequenceBeenPlayed(DoorSequenceData sequence) =>
        sequence != null && (completedSequenceIds.Contains(sequence.name) || playedSequenceIds.Contains(sequence.name));

    public void MarkSequenceAsPlayed(DoorSequenceData sequence)
    {
        if (sequence == null) return;
        playedSequenceIds.Add(sequence.name);
        completedSequenceIds.Add(sequence.name); // playing = played at least once
    }

    /// <summary>Used by save system to mark a sequence as played without a DoorSequenceData reference.</summary>
    public void MarkSequenceAsPlayedById(string sequenceAssetName)
    {
        if (!string.IsNullOrWhiteSpace(sequenceAssetName))
        {
            playedSequenceIds.Add(sequenceAssetName);
            completedSequenceIds.Add(sequenceAssetName);
        }
    }

    // ---------- Door progression ----------
    public void SetDoorCurrentSequence(string doorId, DoorSequenceData sequence)
    {
        if (string.IsNullOrEmpty(doorId) || sequence == null) return;
        doorCurrentSequenceNames[doorId] = sequence.name;
    }

    public string GetDoorCurrentSequenceName(string doorId)
    {
        doorCurrentSequenceNames.TryGetValue(doorId, out string name);
        return name;
    }

    public void GetDoorProgression(out List<string> keys, out List<string> values)
    {
        keys = new List<string>(doorCurrentSequenceNames.Keys);
        values = new List<string>(doorCurrentSequenceNames.Values);
    }

    public void RestoreDoorProgression(List<string> keys, List<string> values)
    {
        doorCurrentSequenceNames.Clear();
        if (keys == null || values == null || keys.Count != values.Count) return;
        for (int i = 0; i < keys.Count; i++)
            doorCurrentSequenceNames[keys[i]] = values[i];
    }

    // ---------- Save system helpers ----------
    public List<string> GetUnlockedMissionIds()
    {
        var ids = new List<string>(unlockedMissions.Count);
        foreach (var m in unlockedMissions) if (m != null) ids.Add(m.Id);
        return ids;
    }
    public List<string> GetCompletedMissionIds() => new List<string>(completedMissionIds);
    public List<string> GetPlayedMissionIds() => new List<string>(playedMissionIds);
    public List<string> GetCompletedSequenceIds() => new List<string>(completedSequenceIds);
    public List<string> GetPlayedSequenceIds() => new List<string>(playedSequenceIds);

    public void ForceMarkCompleted(MissionData mission)
    {
        if (mission != null) completedMissionIds.Add(mission.Id);
    }
    public void ForceMarkSequenceCompleted(string seqName)
    {
        if (!string.IsNullOrWhiteSpace(seqName))
            completedSequenceIds.Add(seqName);
    }

    public void ClearAllProgress()
    {
        completedMissionIds.Clear();
        playedMissionIds.Clear();
        completedSequenceIds.Clear();
        playedSequenceIds.Clear();
        unlockedMissions.Clear();
        doorCurrentSequenceNames.Clear();
        currentMission = null;
        lastCompletedMission = null;
        lastUnlockedMissionId = "";
        OnAvailableMissionsChanged?.Invoke(unlockedMissions.AsReadOnly());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "CabinScene" && lastCompletedMission != null)
        {
            Debug.Log($"[MissionManager] Returned to lobby after mission: {lastCompletedMission.title}");
        }
    }
}
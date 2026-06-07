using System;
using System.Collections.Generic;
using BayatGames.SaveGameFree;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Singleton that owns all save-slot state and the active runtime save.
public class GameSaveManager : MonoBehaviour
{
    // =====================================================================
    // Singleton
    // =====================================================================

    public static GameSaveManager Instance { get; private set; }

    // =====================================================================
    // Inspector
    // =====================================================================

    [Header("Mission Registry")]
    [Tooltip("Assign the MissionRegistry ScriptableObject asset here.")]
    [SerializeField] private MissionRegistry missionRegistry;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string cabinScene = "CabinScene";

    // =====================================================================
    // Constants
    // =====================================================================

    public const int SlotCount = 6;
    private const string SlotKeyPrefix = "slot_";

    // =====================================================================
    // State
    // =====================================================================

    /// <summary>Index of the slot currently in use. -1 means none selected.</summary>
    public int ActiveSlotIndex { get; private set; } = -1;

    /// <summary>Live copy of the active save kept in memory while playing.</summary>
    public GameSaveData ActiveSave { get; private set; }

    /// <summary>
    /// True when the active save has a real world position that should be
    /// restored. False on a brand-new save so the spawn-point tag is used.
    /// PlayerPersistenceManager reads this before teleporting the player.
    /// </summary>
    public bool ShouldSkipSpawnPoint =>
        ActiveSave != null && ActiveSave.hasSavedPosition;

    /// <summary>
    /// True when the intro camera fade should be skipped because it has
    /// already played on this save. IntroCameraFade reads this on Start().
    /// </summary>
    public bool ShouldSkipIntro =>
        ActiveSave != null && ActiveSave.introHasPlayed;

    // Cached slot headers so the Main Menu UI never deserialises everything.
    private readonly GameSaveData[] _slotCache = new GameSaveData[SlotCount];

    // =====================================================================
    // Lifecycle
    // =====================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAllSlotHeaders();
    }

    private void OnDestroy()
    {
        // Best-effort save if the app quits unexpectedly while in-game.
        if (ActiveSlotIndex >= 0 && ActiveSave != null)
            WriteActiveSaveToDisk();
    }

    // =====================================================================
    // Slot header cache
    // =====================================================================

    /// <summary>
    /// Reads every slot from disk into the local cache.
    /// Called once on startup and again after any write.
    /// </summary>
    public void LoadAllSlotHeaders()
    {
        for (int i = 0; i < SlotCount; i++)
            _slotCache[i] = ReadSlotFromDisk(i) ?? new GameSaveData();
    }

    /// <summary>Returns the cached data for a slot. Never returns null.</summary>
    public GameSaveData GetSlotData(int index)
    {
        if (index < 0 || index >= SlotCount) return new GameSaveData();
        return _slotCache[index] ?? new GameSaveData();
    }

    // =====================================================================
    // Public API -- Main Menu
    // =====================================================================

    /// <summary>
    /// Creates a brand-new save in the given slot and loads into CabinScene.
    /// introHasPlayed and hasSavedPosition are both false so the wake-up
    /// animation plays and the spawn-point tag positions the player.
    /// </summary>
    public void CreateNewSave(int slotIndex, string saveName,
                              float intelligence, float strength,
                              float agility, float staminaAttr)
    {
        var data = new GameSaveData
        {
            isEmpty = false,
            saveName = saveName,
            timestamp = DateTime.Now.ToString("MMM dd, yyyy  HH:mm"),
            playTimeSeconds = 0f,
            intelligence = intelligence,
            strength = strength,
            agility = agility,
            staminaAttr = staminaAttr,
            health = -1f,   // PlayerStats fills to max on Start()
            mana = -1f,
            stamina = -1f,
            hasSavedPosition = false, // use spawn-point tag on first load
            introHasPlayed = false  // play wake-up animation on first load
        };

        WriteSlotToDisk(slotIndex, data);
        _slotCache[slotIndex] = data;
        ActivateSlot(slotIndex, data);
        SceneManager.LoadScene(cabinScene);
    }

    /// <summary>
    /// Loads an existing save slot and transitions to CabinScene.
    /// </summary>
    public void LoadSlotIntoGame(int slotIndex)
    {
        var data = ReadSlotFromDisk(slotIndex);
        if (data == null || data.isEmpty)
        {
            Debug.LogError($"[GameSaveManager] Tried to load empty slot {slotIndex}.");
            return;
        }

        _slotCache[slotIndex] = data;
        ActivateSlot(slotIndex, data);
        SceneManager.LoadScene(cabinScene);
    }

    /// <summary>
    /// Permanently deletes a save slot from disk.
    /// </summary>
    public void DeleteSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        string key = SlotKey(slotIndex);
        if (SaveGame.Exists(key)) SaveGame.Delete(key);
        _slotCache[slotIndex] = new GameSaveData();
        Debug.Log($"[GameSaveManager] Slot {slotIndex} deleted.");
    }

    // =====================================================================
    // Public API -- In-Game
    // =====================================================================

    /// <summary>
    /// Snapshots the current game state, writes to disk, and returns to the
    /// main menu. Must only be called from CabinScene via CabinQuitSave.
    /// </summary>
    public void SaveAndQuitToMainMenu()
    {
        if (ActiveSlotIndex < 0 || ActiveSave == null)
        {
            Debug.LogError("[GameSaveManager] SaveAndQuitToMainMenu called with no active slot.");
            return;
        }

        SnapshotGameState();
        WriteActiveSaveToDisk();
        LoadAllSlotHeaders();

        ActiveSlotIndex = -1;
        ActiveSave = null;

        SceneManager.LoadScene(mainMenuScene);
    }

    /// <summary>
    /// Accumulates unscaled play time into the active save. Call every frame
    /// from CabinQuitSave.Update(). Does not write to disk.
    /// </summary>
    public void TickPlayTime()
    {
        if (ActiveSave == null) return;
        ActiveSave.playTimeSeconds += Time.unscaledDeltaTime;
    }

    // =====================================================================
    // Apply loaded save to the scene
    // =====================================================================

    /// <summary>
    /// Applies saved attributes and resources to the player.
    /// Position is intentionally NOT set here because PlayerPersistenceManager
    /// handles teleportation after scene initialisation via a coroutine.
    /// Call this from CabinQuitSave before the first frame.
    /// </summary>
    public bool TryApplyToPlayer(PlayerStats stats)
    {
        if (ActiveSave == null || ActiveSave.isEmpty) return false;

        stats.intelligence = ActiveSave.intelligence;
        stats.strength = ActiveSave.strength;
        stats.staminaAttr = ActiveSave.staminaAttr;
        stats.agility = ActiveSave.agility;

        // -1 means new save: leave resources at the max that PlayerStats set in Start().
        if (ActiveSave.health >= 0f) stats.health = Mathf.Clamp(ActiveSave.health, 0f, stats.maxHealth);
        if (ActiveSave.mana >= 0f) stats.mana = Mathf.Clamp(ActiveSave.mana, 0f, stats.maxMana);
        if (ActiveSave.stamina >= 0f) stats.stamina = Mathf.Clamp(ActiveSave.stamina, 0f, stats.maxStamina);

        return true;
    }

    /// <summary>
    /// Restores saved mission state into MissionManager without firing events.
    /// Call this from CabinQuitSave on scene start.
    /// </summary>
    public void TryApplyMissionsToManager(MissionManager manager)
    {
        if (ActiveSave == null || ActiveSave.isEmpty) return;
        if (missionRegistry == null)
        {
            Debug.LogWarning("[GameSaveManager] No MissionRegistry assigned — mission state won't be restored.");
            return;
        }

        manager.ClearAllProgress();

        foreach (var id in ActiveSave.completedMissionIds)
        {
            var m = missionRegistry.GetById(id);
            if (m != null) manager.ForceMarkCompleted(m);
        }

        foreach (var id in ActiveSave.playedMissionIds)
        {
            var m = missionRegistry.GetById(id);
            if (m != null) manager.MarkMissionAsPlayed(m);
        }

        foreach (var id in ActiveSave.availableMissionIds)
        {
            var m = missionRegistry.GetById(id);
            if (m != null) manager.UnlockMission(m);
        }

        foreach (var id in ActiveSave.completedSequenceIds)
            manager.ForceMarkSequenceCompleted(id);
    }

    // =====================================================================
    // Internal helpers
    // =====================================================================

    private void ActivateSlot(int slotIndex, GameSaveData data)
    {
        ActiveSlotIndex = slotIndex;
        ActiveSave = data;
    }

    /// <summary>
    /// Reads the live game state into ActiveSave before writing to disk.
    /// Also marks introHasPlayed and hasSavedPosition as true so subsequent
    /// loads skip the intro and use the saved position.
    /// </summary>
    private void SnapshotGameState()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            var stats = playerObj.GetComponent<PlayerStats>();
            if (stats != null)
            {
                ActiveSave.health = stats.health;
                ActiveSave.mana = stats.mana;
                ActiveSave.stamina = stats.stamina;
            }

            var t = playerObj.transform;
            ActiveSave.posX = t.position.x;
            ActiveSave.posY = t.position.y;
            ActiveSave.posZ = t.position.z;
            ActiveSave.rotY = t.eulerAngles.y;

            // After the first save-and-quit the position is valid and the
            // intro should never play again.
            ActiveSave.hasSavedPosition = true;
            ActiveSave.introHasPlayed = true;
        }
        else
        {
            Debug.LogWarning("[GameSaveManager] No GameObject tagged 'Player' found — position and resources not saved.");
        }

        if (MissionManager.Instance != null)
        {
            ActiveSave.availableMissionIds = MissionManager.Instance.GetAvailableMissionIds();
            ActiveSave.completedMissionIds = MissionManager.Instance.GetCompletedMissionIds();
            ActiveSave.playedMissionIds = MissionManager.Instance.GetPlayedMissionIds();
            ActiveSave.completedSequenceIds = MissionManager.Instance.GetCompletedSequenceIds();
        }
    }

    // =====================================================================
    // Disk I/O
    // =====================================================================

    private void WriteActiveSaveToDisk()
    {
        if (ActiveSlotIndex < 0 || ActiveSave == null) return;
        WriteSlotToDisk(ActiveSlotIndex, ActiveSave);
    }

    private static void WriteSlotToDisk(int index, GameSaveData data)
    {
        try
        {
            SaveGame.Save(SlotKey(index), data);
            Debug.Log($"[GameSaveManager] Slot {index} written.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameSaveManager] Failed to write slot {index}: {ex.Message}");
        }
    }

    private static GameSaveData ReadSlotFromDisk(int index)
    {
        string key = SlotKey(index);
        try
        {
            if (!SaveGame.Exists(key)) return null;
            return SaveGame.Load<GameSaveData>(key);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameSaveManager] Failed to read slot {index}: {ex.Message}");
            return null;
        }
    }

    private static string SlotKey(int index) => $"{SlotKeyPrefix}{index}";
}
using System;
using System.Collections.Generic;
using BayatGames.SaveGameFree;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }

    [Header("Mission Registry")]
    [SerializeField] private MissionRegistry missionRegistry;

    [Header("Item Registry")]
    [SerializeField] private ItemRegistry itemRegistry;

    [Header("World Item Tags (used to find items on the ground)")]
    [SerializeField] private string[] worldItemTags = new string[] { "WorldItem" };

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenuScene";
    [SerializeField] private string cabinScene = "CabinScene";

    public const int SlotCount = 6;
    private const string SlotKeyPrefix = "slot_";

    public int ActiveSlotIndex { get; private set; } = -1;
    public GameSaveData ActiveSave { get; private set; }

    public bool ShouldSkipSpawnPoint => ActiveSave != null && ActiveSave.hasSavedPosition;
    public bool ShouldSkipIntro => ActiveSave != null && ActiveSave.introHasPlayed;

    private readonly GameSaveData[] _slotCache = new GameSaveData[SlotCount];
    private bool missionsRestoredThisSession = false;
    private bool itemsRestoredThisSession = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAllSlotHeaders();
    }

    private void OnDestroy()
    {
        if (ActiveSlotIndex >= 0 && ActiveSave != null)
            WriteActiveSaveToDisk();
    }

    // ── Slot management (unchanged) ──────────────────────────────────────
    public void LoadAllSlotHeaders()
    {
        for (int i = 0; i < SlotCount; i++)
            _slotCache[i] = ReadSlotFromDisk(i) ?? new GameSaveData();
    }

    public GameSaveData GetSlotData(int index)
    {
        if (index < 0 || index >= SlotCount) return new GameSaveData();
        return _slotCache[index] ?? new GameSaveData();
    }

    public void DeleteSaveAndQuitToMainMenu()
    {
        if (ActiveSlotIndex < 0 || ActiveSave == null) return;
        DeleteSlot(ActiveSlotIndex);
        if (MissionManager.Instance != null)
            MissionManager.Instance.ClearAllProgress();

        ActiveSlotIndex = -1;
        ActiveSave = null;
        missionsRestoredThisSession = false;
        itemsRestoredThisSession = false;
        LoadAllSlotHeaders();
        SceneManager.LoadScene(mainMenuScene);
    }

    public void CreateNewSave(int slotIndex, string saveName,
                              float intelligence, float strength,
                              float agility, float staminaAttr)
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.ClearAllProgress();

        var data = new GameSaveData
        {
            isEmpty = false,
            saveName = saveName,
            timestamp = DateTime.Now.ToString("MMM dd, yyyy  HH:mm"),
            playTimeSeconds = 0f,
            intelligence = intelligence,
            strength = strength,
            agility = agility,
            staminaAttr = staminaAttr
        };
        WriteSlotToDisk(slotIndex, data);
        _slotCache[slotIndex] = data;
        ActivateSlot(slotIndex, data);
        missionsRestoredThisSession = true;
        itemsRestoredThisSession = true;   // fresh save = nothing to restore
        SceneManager.LoadScene(cabinScene);
    }

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
        missionsRestoredThisSession = false;
        itemsRestoredThisSession = false;   // will be restored after scene load
        SceneManager.LoadScene(cabinScene);
    }

    public void DeleteSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        string key = SlotKey(slotIndex);
        if (SaveGame.Exists(key)) SaveGame.Delete(key);
        _slotCache[slotIndex] = new GameSaveData();
    }

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
        missionsRestoredThisSession = false;
        itemsRestoredThisSession = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void TickPlayTime()
    {
        if (ActiveSave != null)
            ActiveSave.playTimeSeconds += Time.unscaledDeltaTime;
    }

    // ── Player stats (unchanged) ─────────────────────────────────────────
    public bool TryApplyToPlayer(PlayerStats stats)
    {
        if (ActiveSave == null || ActiveSave.isEmpty) return false;
        stats.intelligence = ActiveSave.intelligence;
        stats.strength = ActiveSave.strength;
        stats.staminaAttr = ActiveSave.staminaAttr;
        stats.agility = ActiveSave.agility;
        if (ActiveSave.health >= 0f) stats.health = Mathf.Clamp(ActiveSave.health, 0f, stats.maxHealth);
        if (ActiveSave.mana >= 0f) stats.mana = Mathf.Clamp(ActiveSave.mana, 0f, stats.maxMana);
        if (ActiveSave.stamina >= 0f) stats.stamina = Mathf.Clamp(ActiveSave.stamina, 0f, stats.maxStamina);
        return true;
    }

    // ── Mission restore (your fixed version) ─────────────────────────────
    public void TryApplyMissionsToManager(MissionManager manager)
    {
        if (ActiveSave == null || ActiveSave.isEmpty) return;
        if (missionsRestoredThisSession) return;

        if (missionRegistry == null)
        {
            Debug.LogWarning("[GameSaveManager] No MissionRegistry assigned.");
            return;
        }

        if (manager.GetAvailableMissions().Count == 0 &&
            manager.GetCompletedMissionIds().Count == 0 &&
            manager.GetPlayedMissionIds().Count == 0)
        {
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
            foreach (var id in ActiveSave.unlockedMissionIds)
            {
                var m = missionRegistry.GetById(id);
                if (m != null) manager.UnlockMissionWithoutNotify(m);
            }
            manager.NotifyAvailableMissionsChanged();

            foreach (var id in ActiveSave.completedSequenceIds)
                manager.ForceMarkSequenceCompleted(id);
            foreach (var id in ActiveSave.playedSequenceIds)
                manager.MarkSequenceAsPlayedById(id);

            manager.RestoreDoorProgression(ActiveSave.doorCurrentSequenceKeys, ActiveSave.doorCurrentSequenceValues);
        }
        missionsRestoredThisSession = true;
    }

    // ── Item restore (NEW – automatic) ───────────────────────────────────
    /// <summary>
    /// Call this after the scene is loaded and PlayerInventory is ready.
    /// It will restore world items and player inventory exactly as they were saved.
    /// </summary>
    public void TryApplyItems(GameObject player)
    {
        if (ActiveSave == null || ActiveSave.isEmpty) return;
        if (itemsRestoredThisSession) return;

        PlayerInventory inventory = null;

        // 1. Try direct component on the passed player object (or its children)
        if (player != null)
            inventory = player.GetComponentInChildren<PlayerInventory>();

        // 2. Fallback: search the whole scene (useful if player inventory is on a different root)
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogWarning("[GameSaveManager] PlayerInventory not found – items not restored.");
            return;
        }

        RestoreItems(ActiveSave, inventory);
        itemsRestoredThisSession = true;
    }

    // ── Public method kept for backward compatibility ────────────────────
    public void TryApplyItemsAndEffects(CabinItemPersistence persistence)
    {
        // No longer needed; item restore is handled automatically.
    }

    // ── Internal snapshot & restore ──────────────────────────────────────

    private void SnapshotGameState()
    {
        // Player stats & position
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
            ActiveSave.hasSavedPosition = true;
            ActiveSave.introHasPlayed = true;
        }

        // Missions & sequences
        if (MissionManager.Instance != null)
        {
            ActiveSave.unlockedMissionIds = MissionManager.Instance.GetUnlockedMissionIds();
            ActiveSave.lastUnlockedMissionId = MissionManager.Instance.GetLastUnlockedMissionId();
            ActiveSave.completedMissionIds = MissionManager.Instance.GetCompletedMissionIds();
            ActiveSave.playedMissionIds = MissionManager.Instance.GetPlayedMissionIds();
            ActiveSave.completedSequenceIds = MissionManager.Instance.GetCompletedSequenceIds();
            ActiveSave.playedSequenceIds = MissionManager.Instance.GetPlayedSequenceIds();

            MissionManager.Instance.GetDoorProgression(out List<string> keys, out List<string> values);
            ActiveSave.doorCurrentSequenceKeys = keys;
            ActiveSave.doorCurrentSequenceValues = values;
        }

        // ── ITEM SNAPSHOT ────────────────────────────────────────────────
        SnapshotItems();
    }

    private void SnapshotItems()
    {
        ActiveSave.savedItems = new List<SavedItemData>();

        // 1. World items (lying on the ground)
        List<GameObject> worldObjects = new List<GameObject>();
        foreach (string tag in worldItemTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            worldObjects.AddRange(GameObject.FindGameObjectsWithTag(tag));
        }

        foreach (var go in worldObjects)
        {
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup == null || pickup.itemPrefab == null) continue;

            var data = new SavedItemData
            {
                prefabName = pickup.itemPrefab.name,
                location = ItemSaveLocation.World,
                worldX = go.transform.position.x,
                worldY = go.transform.position.y,
                worldZ = go.transform.position.z,
                worldRotX = go.transform.rotation.x,
                worldRotY = go.transform.rotation.y,
                worldRotZ = go.transform.rotation.z,
                worldRotW = go.transform.rotation.w
            };
            AppendWandData(data, go);
            ActiveSave.savedItems.Add(data);
        }

        // 2. Player inventory
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        var inventory = playerObj.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        // Right hand
        if (inventory.rightHandItem != null)
        {
            var pickup = inventory.rightHandItem.GetComponent<ItemPickup>();
            if (pickup != null && pickup.itemPrefab != null)
            {
                var data = new SavedItemData
                {
                    prefabName = pickup.itemPrefab.name,
                    location = ItemSaveLocation.Hand
                };
                AppendWandData(data, inventory.rightHandItem);
                ActiveSave.savedItems.Add(data);
            }
        }

        // Backpack
        for (int i = 0; i < inventory.backpack.Length; i++)
        {
            var item = inventory.backpack[i];
            if (item == null) continue;

            var pickup = item.GetComponent<ItemPickup>();
            if (pickup == null || pickup.itemPrefab == null) continue;

            var data = new SavedItemData
            {
                prefabName = pickup.itemPrefab.name,
                location = ItemSaveLocation.Backpack,
                backpackSlot = i
            };
            AppendWandData(data, item);
            ActiveSave.savedItems.Add(data);
        }
    }

    private void AppendWandData(SavedItemData data, GameObject item)
    {
        var wand = item.GetComponent<WandItem>();
        if (wand == null) return;

        data.wandSelectedIndex = wand.SelectedIndex;
        data.wandSlotPrefabNames = new List<string>();

        for (int i = 0; i < wand.SlotCount; i++)
        {
            var slotItem = wand.GetSlotItem(i);
            if (slotItem == null)
            {
                data.wandSlotPrefabNames.Add(string.Empty);
                continue;
            }
            var slotPickup = slotItem.GetComponent<ItemPickup>();
            if (slotPickup != null && slotPickup.itemPrefab != null)
                data.wandSlotPrefabNames.Add(slotPickup.itemPrefab.name);
            else
                data.wandSlotPrefabNames.Add(string.Empty);
        }
    }

    private void RestoreItems(GameSaveData saveData, PlayerInventory inventory)
    {
        if (saveData.savedItems == null || saveData.savedItems.Count == 0) return;
        if (itemRegistry == null)
        {
            Debug.LogWarning("[GameSaveManager] No ItemRegistry assigned – cannot restore items.");
            return;
        }

        // Destroy all existing world items to avoid duplicates
        foreach (string tag in worldItemTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            GameObject[] worldObjects = GameObject.FindGameObjectsWithTag(tag);
            foreach (var go in worldObjects) Destroy(go);
        }

        // Clear the player's current inventory (optional, but safe)
        if (inventory.rightHandItem != null)
        {
            Destroy(inventory.rightHandItem);
            inventory.rightHandItem = null;
        }
        for (int i = 0; i < inventory.backpack.Length; i++)
        {
            if (inventory.backpack[i] != null)
            {
                Destroy(inventory.backpack[i]);
                inventory.backpack[i] = null;
            }
        }

        foreach (var saved in saveData.savedItems)
        {
            if (string.IsNullOrEmpty(saved.prefabName)) continue;

            var prefab = itemRegistry.GetPrefabByName(saved.prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameSaveManager] Prefab '{saved.prefabName}' not in ItemRegistry – skipped.");
                continue;
            }

            var instance = Instantiate(prefab);

            // Restore wand slots before placing the item
            var wand = instance.GetComponent<WandItem>();
            if (wand != null && saved.wandSlotPrefabNames != null && saved.wandSlotPrefabNames.Count > 0)
                RestoreWandSlots(wand, saved);

            switch (saved.location)
            {
                case ItemSaveLocation.World:
                    instance.transform.SetParent(null);
                    instance.transform.position = new Vector3(saved.worldX, saved.worldY, saved.worldZ);
                    instance.transform.rotation = new Quaternion(saved.worldRotX, saved.worldRotY, saved.worldRotZ, saved.worldRotW);

                    var rb = instance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.detectCollisions = true;
                    }
                    foreach (var col in instance.GetComponentsInChildren<Collider>())
                        col.enabled = true;

                    instance.SetActive(true);
                    break;

                case ItemSaveLocation.Hand:
                    // Use EquipRight to properly set up the item
                    inventory.EquipRight(instance);
                    break;

                case ItemSaveLocation.Backpack:
                    instance.transform.SetParent(inventory.transform);
                    instance.SetActive(false);
                    if (saved.backpackSlot >= 0 && saved.backpackSlot < inventory.backpack.Length)
                        inventory.backpack[saved.backpackSlot] = instance;
                    else
                    {
                        // Fallback: put in first free slot
                        for (int i = 0; i < inventory.backpack.Length; i++)
                        {
                            if (inventory.backpack[i] == null)
                            {
                                inventory.backpack[i] = instance;
                                break;
                            }
                        }
                    }
                    break;
            }
        }

        // Fire inventory changed event so UI updates
        // inventory.OnInventoryChanged?.Invoke();
    }

    private void RestoreWandSlots(WandItem wand, SavedItemData saved)
    {
        for (int i = 0; i < saved.wandSlotPrefabNames.Count && i < wand.SlotCount; i++)
        {
            string slotName = saved.wandSlotPrefabNames[i];
            if (string.IsNullOrEmpty(slotName)) continue;

            var prefab = itemRegistry.GetPrefabByName(slotName);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameSaveManager] Wand slot {i} prefab '{slotName}' not found.");
                continue;
            }

            var slotInstance = Instantiate(prefab);
            wand.SetSlotItem(i, slotInstance);
        }
        wand.SelectedIndex = saved.wandSelectedIndex;
    }

    // ── Disk I/O (unchanged) ─────────────────────────────────────────────
    private void WriteActiveSaveToDisk()
    {
        if (ActiveSlotIndex < 0 || ActiveSave == null) return;
        WriteSlotToDisk(ActiveSlotIndex, ActiveSave);
    }

    private static void WriteSlotToDisk(int index, GameSaveData data)
    {
        try { SaveGame.Save(SlotKey(index), data); }
        catch (Exception ex) { Debug.LogError($"[GameSaveManager] Write failed: {ex.Message}"); }
    }

    private static GameSaveData ReadSlotFromDisk(int index)
    {
        try
        {
            if (!SaveGame.Exists(SlotKey(index))) return null;
            return SaveGame.Load<GameSaveData>(SlotKey(index));
        }
        catch (Exception ex) { Debug.LogWarning($"[GameSaveManager] Read failed: {ex.Message}"); return null; }
    }

    private static string SlotKey(int index) => $"{SlotKeyPrefix}{index}";

    // ── Backward‑compatible public snapshot helper ───────────────────────
    public void SnapshotItemsAndEffects(CabinItemPersistence persistence)
    {
        // No longer used, but kept so any existing calls don't break.
        // Items are now saved automatically inside SnapshotGameState.
    }

    private void ActivateSlot(int slotIndex, GameSaveData data)
    {
        ActiveSlotIndex = slotIndex;
        ActiveSave = data;
    }
}
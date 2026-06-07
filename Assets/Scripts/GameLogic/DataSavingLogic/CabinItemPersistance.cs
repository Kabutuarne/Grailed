using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to the same GameObject as CabinQuitSave (or any persistent cabin
/// object). Responsible for two jobs:
///
///   1. SNAPSHOT  — CollectSnapshot() walks every item in the cabin (world +
///                  player inventory) and writes SavedItemData entries into
///                  GameSaveData.savedItems. Also writes SavedEffectData
///                  entries for every active DurationEffect on the player.
///                  Call this from CabinQuitSave before GameSaveManager
///                  writes to disk.
///
///   2. RESTORE   — StartRestore() reads SavedItemData / SavedEffectData from
///                  GameSaveData and reconstructs the scene. Call this from
///                  CabinQuitSave after the scene has finished loading (i.e.
///                  after PlayerStats and PlayerInventory are ready).
///
/// IMPORTANT: This component only manages items that belong to CabinScene. Items
/// that were dropped in a level scene are already gone — they are simply absent
/// from the save and will not be re-instantiated.
/// </summary>
public class CabinItemPersistence : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Tags used to find every world item in CabinScene that can be picked up. Supports multiple tags like Painting, Book, etc.")]
    [SerializeField] private string[] worldItemTags = new string[] { "WorldItem" };

    [Tooltip("The ItemRegistry ScriptableObject — same reference used by GameSaveManager.")]
    [SerializeField] private ItemRegistry itemRegistry;

    // =========================================================================
    // SNAPSHOT  (call at save time, before writing to disk)
    // =========================================================================

    /// <summary>
    /// Collects the current state of all cabin items and active player effects
    /// into the supplied <paramref name="saveData"/>. Overwrites any previously
    /// stored item / effect lists.
    /// </summary>
    public void CollectSnapshot(GameSaveData saveData)
    {
        if (saveData == null) return;

        saveData.savedItems = new List<SavedItemData>();
        saveData.savedEffects = new List<SavedEffectData>();

        SnapshotItems(saveData);
        SnapshotEffects(saveData);
    }

    // ── Item snapshot ─────────────────────────────────────────────────────

    private void SnapshotItems(GameSaveData saveData)
    {
        // ── World items ───────────────────────────────────────────────────
        // Find every active WorldItem in the scene across all specified tags.
        // Items held in the player's inventory are deactivated, so this only
        // returns items actually lying on the ground.
        List<GameObject> worldObjects = new List<GameObject>();
        foreach (string tag in worldItemTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            worldObjects.AddRange(taggedObjects);
        }

        foreach (var go in worldObjects)
        {
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup == null) continue;

            var data = BuildItemData(go, pickup, ItemSaveLocation.World);
            if (data != null)
                saveData.savedItems.Add(data);
        }

        // ── Player inventory ──────────────────────────────────────────────
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("[CabinItemPersistence] No GameObject tagged 'Player' found — inventory not saved.");
            return;
        }

        var inventory = playerObj.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        // Right-hand item
        if (inventory.rightHandItem != null)
        {
            var pickup = inventory.rightHandItem.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                var data = BuildItemData(inventory.rightHandItem, pickup, ItemSaveLocation.Hand);
                if (data != null)
                    saveData.savedItems.Add(data);
            }
        }

        // Backpack slots
        for (int i = 0; i < inventory.backpack.Length; i++)
        {
            var item = inventory.backpack[i];
            if (item == null) continue;

            var pickup = item.GetComponent<ItemPickup>();
            if (pickup == null) continue;

            var data = BuildItemData(item, pickup, ItemSaveLocation.Backpack);
            if (data != null)
            {
                data.backpackSlot = i;
                saveData.savedItems.Add(data);
            }
        }
    }

    /// <summary>
    /// Creates a SavedItemData for <paramref name="go"/>.
    /// Returns null if the item has no valid prefab reference in its ItemPickup.
    /// </summary>
    private SavedItemData BuildItemData(GameObject go, ItemPickup pickup, ItemSaveLocation loc)
    {
        if (pickup.itemPrefab == null)
        {
            Debug.LogWarning($"[CabinItemPersistence] '{go.name}' has no itemPrefab assigned on ItemPickup — skipping save.");
            return null;
        }

        var data = new SavedItemData
        {
            prefabName = pickup.itemPrefab.name,
            location = loc
        };

        // World position is only meaningful for ground items; we still store it
        // for hand / backpack items so a future restore can choose to use it if needed.
        var t = go.transform;
        data.worldX = t.position.x;
        data.worldY = t.position.y;
        data.worldZ = t.position.z;
        data.worldRotX = t.rotation.x;
        data.worldRotY = t.rotation.y;
        data.worldRotZ = t.rotation.z;
        data.worldRotW = t.rotation.w;

        // If this item is a wand, save its internal spell slots too.
        var wand = go.GetComponent<WandItem>();
        if (wand != null)
            AppendWandData(data, wand);

        return data;
    }

    private void AppendWandData(SavedItemData data, WandItem wand)
    {
        data.wandSelectedIndex = wand.SelectedIndex;
        data.wandSlotPrefabNames = new List<string>();

        for (int i = 0; i < wand.SlotCount; i++)
        {
            var slotItem = wand.GetSlotItem(i);
            if (slotItem == null)
            {
                // Empty slot represented by empty string.
                data.wandSlotPrefabNames.Add(string.Empty);
                continue;
            }

            var slotPickup = slotItem.GetComponent<ItemPickup>();
            if (slotPickup != null && slotPickup.itemPrefab != null)
                data.wandSlotPrefabNames.Add(slotPickup.itemPrefab.name);
            else
            {
                Debug.LogWarning($"[CabinItemPersistence] Wand slot {i} item '{slotItem.name}' has no itemPrefab — slot saved as empty.");
                data.wandSlotPrefabNames.Add(string.Empty);
            }
        }
    }

    // ── Effect snapshot ───────────────────────────────────────────────────

    private void SnapshotEffects(GameSaveData saveData)
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        var statusEffects = playerObj.GetComponent<StatusEffects>();
        if (statusEffects == null) return;

        foreach (var effect in statusEffects.ActiveEffects)
        {
            // Skip instant effects (duration == 0) — already consumed.
            // Skip infinite effects (duration < 0) — managed by other systems.
            if (effect.IsInstant || effect.IsInfinite) continue;

            // Only save effects that still have meaningful time left.
            if (effect.timer <= 0f) continue;

            var saved = new SavedEffectData
            {
                effectId = effect.id,
                carrierName = effect.carrier != null ? effect.carrier.name : string.Empty,
                remainingTimer = effect.timer,
                originalDuration = effect.duration,

                speedMultiplier = effect.speedMultiplier,
                healthRegenMultiplier = effect.healthRegenMultiplier,
                manaRegenMultiplier = effect.manaRegenMultiplier,
                energyRegenMultiplier = effect.energyRegenMultiplier,

                healthPerSecond = effect.healthPerSecond,
                manaPerSecond = effect.manaPerSecond,
                energyPerSecond = effect.energyPerSecond,

                addStrength = effect.addStrength,
                addIntelligence = effect.addIntelligence,
                addStaminaAttr = effect.addStaminaAttr,
                addAgility = effect.addAgility
            };

            saveData.savedEffects.Add(saved);
        }
    }

    // =========================================================================
    // RESTORE  (call after scene is ready, player and inventory exist)
    // =========================================================================

    /// <summary>
    /// Re-instantiates all saved items and re-applies saved status effects from
    /// <paramref name="saveData"/>. Uses a one-frame coroutine so that all scene
    /// Awake / Start calls have completed before items are parented into the
    /// player's inventory — matching the same ordering guarantee that
    /// PlayerPersistenceManager uses for position restoration.
    /// </summary>
    public void StartRestore(GameSaveData saveData)
    {
        if (saveData == null || saveData.isEmpty) return;
        StartCoroutine(RestoreCoroutine(saveData));
    }

    private IEnumerator RestoreCoroutine(GameSaveData saveData)
    {
        // Wait one frame so PlayerInventory.Awake / Start have run.
        yield return null;

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("[CabinItemPersistence] No 'Player' GameObject found during restore.");
            yield break;
        }

        var inventory = playerObj.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("[CabinItemPersistence] PlayerInventory not found on Player during restore.");
            yield break;
        }

        RestoreItems(saveData, inventory);
        RestoreEffects(saveData, playerObj);
    }

    // ── Item restore ──────────────────────────────────────────────────────

    private void RestoreItems(GameSaveData saveData, PlayerInventory inventory)
    {
        if (saveData.savedItems == null) return;

        // Destroy all currently active WorldItems across all tags so we start
        // clean — prevents duplicate items if the scene's pre-placed defaults
        // are still present. Items parented to the player are NOT destroyed
        // here; they are either overwritten by the restore or left alone if
        // savedItems is authoritative.
        DestroyExistingWorldItems();

        foreach (var saved in saveData.savedItems)
        {
            if (string.IsNullOrEmpty(saved.prefabName)) continue;

            var prefab = itemRegistry.GetPrefabByName(saved.prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[CabinItemPersistence] Prefab '{saved.prefabName}' not found in ItemRegistry — item skipped.");
                continue;
            }

            var instance = Instantiate(prefab);

            // Restore wand internals before placing the item anywhere.
            var wand = instance.GetComponent<WandItem>();
            if (wand != null && saved.wandSlotPrefabNames != null && saved.wandSlotPrefabNames.Count > 0)
                RestoreWandSlots(wand, saved);

            switch (saved.location)
            {
                case ItemSaveLocation.World:
                    PlaceInWorld(instance, saved);
                    break;

                case ItemSaveLocation.Hand:
                    inventory.EquipRight(instance);
                    break;

                case ItemSaveLocation.Backpack:
                    // EquipRight / backpack path: directly write into slot so we
                    // honour the exact slot index that was saved.
                    PlaceInBackpackSlot(instance, inventory, saved.backpackSlot);
                    break;
            }
        }
    }

    private void DestroyExistingWorldItems()
    {
        // Destroy all world items across every configured tag.
        foreach (string tag in worldItemTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            GameObject[] worldObjects = GameObject.FindGameObjectsWithTag(tag);
            foreach (var go in worldObjects)
                Destroy(go);
        }
    }

    private void PlaceInWorld(GameObject instance, SavedItemData saved)
    {
        instance.transform.SetParent(null, false);
        instance.transform.position = new Vector3(saved.worldX, saved.worldY, saved.worldZ);
        instance.transform.rotation = new Quaternion(saved.worldRotX, saved.worldRotY, saved.worldRotZ, saved.worldRotW);

        // Ensure physics and colliders are active for a world item.
        var rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        foreach (var col in instance.GetComponentsInChildren<Collider>())
            col.enabled = true;

        instance.SetActive(true);
    }

    private void PlaceInBackpackSlot(GameObject instance, PlayerInventory inventory, int slot)
    {
        if (slot < 0 || slot >= inventory.backpack.Length)
        {
            Debug.LogWarning($"[CabinItemPersistence] Saved backpack slot {slot} is out of range — item '{instance.name}' placed in first available slot.");
            // Fall back to first free slot via normal pickup path.
            instance.transform.SetParent(inventory.transform, true);
            instance.SetActive(false);
            for (int i = 0; i < inventory.backpack.Length; i++)
            {
                if (inventory.backpack[i] == null)
                {
                    inventory.backpack[i] = instance;
                    break;
                }
            }
            return;
        }

        instance.transform.SetParent(inventory.transform, true);
        instance.SetActive(false);
        inventory.backpack[slot] = instance;
    }

    private void RestoreWandSlots(WandItem wand, SavedItemData saved)
    {
        for (int i = 0; i < saved.wandSlotPrefabNames.Count && i < wand.SlotCount; i++)
        {
            var slotPrefabName = saved.wandSlotPrefabNames[i];
            if (string.IsNullOrEmpty(slotPrefabName)) continue;

            var slotPrefab = itemRegistry.GetPrefabByName(slotPrefabName);
            if (slotPrefab == null)
            {
                Debug.LogWarning($"[CabinItemPersistence] Wand slot {i} prefab '{slotPrefabName}' not in ItemRegistry — slot left empty.");
                continue;
            }

            var slotInstance = Instantiate(slotPrefab);
            wand.SetSlotItem(i, slotInstance);
        }

        // Restore the previously selected scroll index.
        wand.SelectedIndex = saved.wandSelectedIndex;
    }

    // ── Effect restore ────────────────────────────────────────────────────

    private void RestoreEffects(GameSaveData saveData, GameObject playerObj)
    {
        if (saveData.savedEffects == null || saveData.savedEffects.Count == 0) return;

        var statusEffects = playerObj.GetComponent<StatusEffects>();
        if (statusEffects == null)
        {
            Debug.LogWarning("[CabinItemPersistence] No StatusEffects component found on Player — effects not restored.");
            return;
        }

        foreach (var saved in saveData.savedEffects)
        {
            if (string.IsNullOrEmpty(saved.effectId)) continue;
            if (saved.remainingTimer <= 0f) continue;

            // Look up the carrier by asset name so visuals are restored.
            EffectCarrier carrier = null;
            if (!string.IsNullOrEmpty(saved.carrierName))
                carrier = itemRegistry.GetCarrierByName(saved.carrierName);

            // Reconstruct a StatusEffectData from the saved numeric fields.
            // We set duration to originalDuration and timer to remainingTimer so
            // the effect continues exactly where it left off.
            var effectData = new StatusEffectData(saved.effectId, saved.originalDuration)
            {
                timer = saved.remainingTimer,
                carrier = carrier,

                speedMultiplier = saved.speedMultiplier,
                healthRegenMultiplier = saved.healthRegenMultiplier,
                manaRegenMultiplier = saved.manaRegenMultiplier,
                energyRegenMultiplier = saved.energyRegenMultiplier,

                healthPerSecond = saved.healthPerSecond,
                manaPerSecond = saved.manaPerSecond,
                energyPerSecond = saved.energyPerSecond,

                addStrength = saved.addStrength,
                addIntelligence = saved.addIntelligence,
                addStaminaAttr = saved.addStaminaAttr,
                addAgility = saved.addAgility
            };

            statusEffects.AddEffect(effectData);
        }
    }
}
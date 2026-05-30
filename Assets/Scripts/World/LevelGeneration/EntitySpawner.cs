using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EntitySpawner : MonoBehaviour
{
    public static void PopulateLevel(PerLevelCatalog catalog)
    {
        var spawners = FindObjectsByType<RoomRandomSpawner>(FindObjectsSortMode.None);
        if (spawners == null || spawners.Length == 0)
        {
            Debug.LogWarning("[EntitySpawner] No RoomRandomSpawner instances found for level population.");
            return;
        }

        // Collect all spawning slots
        var allSlots = new List<(Transform transform, PerLevelCatalog.PlacementCategory category)>();
        foreach (var spawner in spawners)
        {
            AddSlots(allSlots, spawner.InChest, PerLevelCatalog.PlacementCategory.InChest);
            AddSlots(allSlots, spawner.InShelf, PerLevelCatalog.PlacementCategory.InShelf);
            AddSlots(allSlots, spawner.OnGround, PerLevelCatalog.PlacementCategory.OnGround);
            AddSlots(allSlots, spawner.OnWall, PerLevelCatalog.PlacementCategory.OnWall);
            AddSlots(allSlots, spawner.OnTable, PerLevelCatalog.PlacementCategory.OnTable);
            AddSlots(allSlots, spawner.OnCounter, PerLevelCatalog.PlacementCategory.OnCounter);
            AddSlots(allSlots, spawner.OnOther, PerLevelCatalog.PlacementCategory.OnOther);
        }

        // Shuffle for random distribution
        var random = new System.Random();
        allSlots = allSlots.OrderBy(x => random.Next()).ToList();

        Debug.Log($"[EntitySpawner] Found {spawners.Length} spawner(s) and {allSlots.Count} total placement slot(s).");

        int spawnedItems = 0;
        int skippedItems = 0;

        // Spawn items
        if (catalog.items != null)
        {
            foreach (var item in catalog.items)
            {
                if (item.prefab == null)
                {
                    Debug.LogWarning($"[EntitySpawner] Item entry has no prefab assigned in catalog '{catalog.name}'.");
                    skippedItems++;
                    continue;
                }

                var slot = allSlots.FirstOrDefault(s => item.allowedSpots.Contains(s.category));
                if (slot.transform != null)
                {
                    Instantiate(item.prefab, slot.transform.position, slot.transform.rotation, slot.transform.parent);
                    allSlots.Remove(slot);
                    spawnedItems++;
                }
                else
                {
                    skippedItems++;
                    Debug.LogWarning($"[EntitySpawner] No available spawn slot for item prefab '{item.prefab.name}' in catalog '{catalog.name}'.");
                }
            }
        }

        Debug.Log($"[EntitySpawner] Spawned {spawnedItems} item(s), skipped {skippedItems} item(s).");

        int spawnedEnemies = 0;
        int skippedEnemies = 0;

        // Spawn enemies
        if (catalog.enemies != null)
        {
            foreach (var enemy in catalog.enemies)
            {
                if (enemy.prefab == null)
                {
                    Debug.LogWarning($"[EntitySpawner] Enemy entry has no prefab assigned in catalog '{catalog.name}'.");
                    skippedEnemies++;
                    continue;
                }

                var slot = allSlots.FirstOrDefault(s => enemy.allowedSpots.Contains(s.category));
                if (slot.transform != null)
                {
                    Instantiate(enemy.prefab, slot.transform.position, slot.transform.rotation, slot.transform.parent);
                    allSlots.Remove(slot);
                    spawnedEnemies++;
                }
                else
                {
                    skippedEnemies++;
                    Debug.LogWarning($"[EntitySpawner] No available spawn slot for enemy prefab '{enemy.prefab.name}' in catalog '{catalog.name}'.");
                }
            }
        }

        Debug.Log($"[EntitySpawner] Spawned {spawnedEnemies} enemy(s), skipped {skippedEnemies} enemy(s). Total remaining slots: {allSlots.Count}.");
    }

    private static void AddSlots(List<(Transform, PerLevelCatalog.PlacementCategory)> list, Transform[] transforms, PerLevelCatalog.PlacementCategory category)
    {
        if (transforms == null)
            return;

        foreach (var t in transforms)
        {
            if (t != null)
                list.Add((t, category));
        }
    }
}
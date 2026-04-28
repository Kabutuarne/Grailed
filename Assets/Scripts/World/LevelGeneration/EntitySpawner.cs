using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EntitySpawner : MonoBehaviour
{
    public static void PopulateLevel(PerLevelCatalog catalog)
    {
        var spawners = FindObjectsByType<RoomRandomSpawner>(FindObjectsSortMode.None);
        if (spawners == null || spawners.Length == 0)
            return;

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

        // Spawn items
        if (catalog.items != null)
        {
            foreach (var item in catalog.items)
            {
                var slot = allSlots.FirstOrDefault(s => item.allowedSpots.Contains(s.category));
                if (slot.transform != null && item.prefab != null)
                {
                    Instantiate(item.prefab, slot.transform.position, slot.transform.rotation, slot.transform.parent);
                    allSlots.Remove(slot);
                }
            }
        }

        // Spawn enemies
        if (catalog.enemies != null)
        {
            foreach (var enemy in catalog.enemies)
            {
                var slot = allSlots.FirstOrDefault(s => enemy.allowedSpots.Contains(s.category));
                if (slot.transform != null && enemy.prefab != null)
                {
                    Instantiate(enemy.prefab, slot.transform.position, slot.transform.rotation, slot.transform.parent);
                    allSlots.Remove(slot);
                }
            }
        }
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
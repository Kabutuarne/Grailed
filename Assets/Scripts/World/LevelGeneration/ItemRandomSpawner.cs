using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ItemRandomSpawner : MonoBehaviour
{
    [Header("Seeding")]
    [Tooltip("If true, will try to read the Seed from Kartograph's LevelGenerator component in the scene.")]
    public bool useKartographSeed = true;
    [Tooltip("If Kartograph seed can't be found, this is used instead.")]
    public int fallbackSeed = 12345;

    [Header("Runtime")]
    [SerializeField] private bool spawnedAlready;

    ItemCatalog catalog;

    void Awake()
    {
        if (catalog == null)
            catalog = ItemCatalog.Instance ?? FindFirstObjectByType<ItemCatalog>();
    }

    /// <summary>Call this manually (or via generator) to begin spawning when the level exists.</summary>
    public IEnumerator SpawnWhenReady()
    {
        if (spawnedAlready) yield break;
        // Wait until rooms have been created by Kartograph.
        RoomRandomSpawner[] rooms = null;
        while (true)
        {
            rooms = FindObjectsByType<RoomRandomSpawner>(FindObjectsSortMode.None);
            if (rooms != null && rooms.Length > 0) break;
            yield return null;
        }

        SpawnAll(rooms);
        yield break;
    }

    void SpawnAll(RoomRandomSpawner[] rooms)
    {
        if (spawnedAlready) return;
        spawnedAlready = true;

        if (catalog == null)
            catalog = ItemCatalog.Instance ?? FindFirstObjectByType<ItemCatalog>();

        if (catalog == null || catalog.items == null || catalog.items.Count == 0)
        {
            Debug.Log("ItemRandomSpawner: No catalog or no items to spawn.");
            return;
        }

        int globalSeed = useKartographSeed ? TryGetKartographSeed(out int s) ? s : fallbackSeed : fallbackSeed;

        for (int ri = 0; ri < rooms.Length; ri++)
        {
            var room = rooms[ri];
            if (room == null) continue;

            int roomSeed = DeriveRoomSeed(room, globalSeed);

            // Build category list
            var categories = new (ItemCatalog.PlacementCategory cat, Transform[] anchors)[]
            {
                (ItemCatalog.PlacementCategory.InChest, room.InChest),
                (ItemCatalog.PlacementCategory.InShelf, room.InShelf),
                (ItemCatalog.PlacementCategory.OnGround, room.OnGround),
                (ItemCatalog.PlacementCategory.OnWall, room.OnWall),
                (ItemCatalog.PlacementCategory.OnTable, room.OnTable),
                (ItemCatalog.PlacementCategory.OnCounter, room.OnCounter),
                (ItemCatalog.PlacementCategory.OnOther, room.OnOther),
            };

            // Track per-room spawn counts for maxPerRoom enforcement
            var counts = new Dictionary<ItemCatalog.ItemEntry, int>();

            for (int c = 0; c < categories.Length; c++)
            {
                var (cat, anchors) = categories[c];
                if (anchors == null || anchors.Length == 0) continue;

                for (int ai = 0; ai < anchors.Length; ai++)
                {
                    var anchor = anchors[ai];
                    if (anchor == null) continue;

                    int pointSeed = HashCombine(roomSeed, HashCombine((int)cat, ai));
                    var rng = new System.Random(pointSeed);

                    // Chance to spawn nothing
                    if (rng.NextDouble() < room.chanceNone) continue;

                    // Gather candidates allowed at this category
                    var candidates = new List<ItemCatalog.ItemEntry>();
                    for (int ei = 0; ei < catalog.items.Count; ei++)
                    {
                        var entry = catalog.items[ei];
                        if (entry == null || entry.prefab == null) continue;
                        if (entry.weight <= 0f) continue;
                        if (AllowsPlacement(entry, cat)) candidates.Add(entry);
                    }

                    if (candidates.Count == 0) continue;

                    // Enforce maxPerRoom by filtering candidates during selection
                    ItemCatalog.ItemEntry chosen = null;
                    var working = new List<ItemCatalog.ItemEntry>(candidates);
                    while (working.Count > 0)
                    {
                        chosen = PickWeighted(working, rng);
                        if (chosen == null) break;

                        counts.TryGetValue(chosen, out int have);
                        if (have >= Mathf.Max(1, chosen.maxPerRoom))
                        {
                            // remove and retry
                            working.Remove(chosen);
                            chosen = null;
                            continue;
                        }

                        break; // chosen accepted
                    }

                    if (chosen == null) continue;

                    Transform parent = room.spawnedContainer != null ? room.spawnedContainer : (room.parentToRoom ? room.transform : null);
                    Instantiate(chosen.prefab, anchor.position, anchor.rotation, parent);

                    counts.TryGetValue(chosen, out int cur);
                    counts[chosen] = cur + 1;
                }
            }
        }
    }

    static bool AllowsPlacement(ItemCatalog.ItemEntry entry, ItemCatalog.PlacementCategory cat)
    {
        if (entry.allowedSpots == null || entry.allowedSpots.Length == 0) return true; // no restriction => allow everywhere
        for (int i = 0; i < entry.allowedSpots.Length; i++)
            if (entry.allowedSpots[i] == cat) return true;
        return false;
    }

    static ItemCatalog.ItemEntry PickWeighted(List<ItemCatalog.ItemEntry> options, System.Random rng)
    {
        if (options == null || options.Count == 0) return null;

        double total = 0.0;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == null || options[i].prefab == null) continue;
            total += Math.Max(0.0, options[i].weight);
        }
        if (total <= 0.0) return null;

        double roll = rng.NextDouble() * total;
        double acc = 0.0;

        for (int i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (o == null || o.prefab == null) continue;

            double w = Math.Max(0.0, o.weight);
            acc += w;
            if (roll <= acc) return o;
        }

        for (int i = options.Count - 1; i >= 0; i--)
            if (options[i] != null && options[i].prefab != null && options[i].weight > 0f)
                return options[i];

        return null;
    }

    // ----------------- Seeding helpers -----------------

    int DeriveRoomSeed(RoomRandomSpawner room, int globalSeed)
    {
        if (room.stableRoomId != 0)
            return HashCombine(globalSeed, room.stableRoomId);

        Vector3 p = room.transform.position;
        float q = Mathf.Max(0.0001f, room.positionQuantize);

        int px = Mathf.RoundToInt(p.x / q);
        int py = Mathf.RoundToInt(p.y / q);
        int pz = Mathf.RoundToInt(p.z / q);

        int nameHash = StableStringHash(room.gameObject.name);
        int posHash = HashCombine(px, HashCombine(py, pz));

        return HashCombine(globalSeed, HashCombine(nameHash, posHash));
    }

    static bool TryGetKartographSeed(out int seed)
    {
        seed = 0;

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null) continue;

            Type t = mb.GetType();
            string typeName = t.Name;
            if (!typeName.Contains("LevelGenerator", StringComparison.OrdinalIgnoreCase))
                continue;

            FieldInfo f = t.GetField("Seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? t.GetField("seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (f != null && f.FieldType == typeof(int))
            {
                seed = (int)f.GetValue(mb);
                return true;
            }

            PropertyInfo p = t.GetProperty("Seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? t.GetProperty("seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (p != null && p.PropertyType == typeof(int) && p.CanRead)
            {
                seed = (int)p.GetValue(mb);
                return true;
            }
        }

        return false;
    }

    // ----------------- Hash helpers (stable across runs) -----------------

    static int HashCombine(int a, int b)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + a;
            hash = hash * 31 + b;
            return hash;
        }
    }

    static int StableStringHash(string s)
    {
        unchecked
        {
            int hash = 23;
            if (string.IsNullOrEmpty(s)) return hash;
            for (int i = 0; i < s.Length; i++)
                hash = hash * 31 + s[i];
            return hash;
        }
    }
}

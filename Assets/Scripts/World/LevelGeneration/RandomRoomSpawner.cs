using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class RoomRandomSpawner : MonoBehaviour
{
    [Serializable]
    public class PrefabWeight
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public class SpawnPoint
    {
        [Tooltip("A transform inside the room that defines position + rotation for the spawn.")]
        public Transform anchor;

        [Range(0f, 1f)]
        [Tooltip("Chance that NOTHING spawns at this point.")]
        public float chanceNone = 0.25f;

        [Tooltip("One prefab will be chosen (weighted) if 'chanceNone' roll fails.")]
        public List<PrefabWeight> options = new List<PrefabWeight>();

        [Tooltip("Optional: overrides how many times this point can spawn. Usually leave at 1.")]
        [Min(1)] public int count = 1;
    }

    [Header("Spawn Points")]
    public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    [Header("Seeding")]
    [Tooltip("If true, will try to read the Seed from Kartograph's LevelGenerator component in the scene.")]
    public bool useKartographSeed = true;

    [Tooltip("If Kartograph seed can't be found, this is used instead.")]
    public int fallbackSeed = 12345;

    [Tooltip("If non-zero, this makes the room's spawns stable even if hierarchy order changes.")]
    public int stableRoomId = 0;

    [Tooltip("Quantizes room position for seed derivation when stableRoomId == 0 (helps determinism).")]
    public float positionQuantize = 0.5f;

    [Header("Parenting")]
    [Tooltip("If true, spawned objects are parented under this room object.")]
    public bool parentToRoom = true;

    [Tooltip("Optional container under the room to keep hierarchy clean (if null, uses this transform).")]
    public Transform spawnedContainer;

    [Header("Runtime")]
    [SerializeField] private bool spawnedAlready;

    void Awake()
    {
        if (spawnedContainer == null)
            spawnedContainer = transform;
    }

    void Start()
    {
        // Kartograph usually generates then enables/starts section objects; Start is a good moment.
        SpawnAll();
    }

    /// <summary>Call this manually if you prefer (e.g., from a generator event).</summary>
    public void SpawnAll()
    {
        if (spawnedAlready) return;
        spawnedAlready = true;

        int globalSeed = useKartographSeed ? TryGetKartographSeed(out int s) ? s : fallbackSeed : fallbackSeed;
        int roomSeed = DeriveRoomSeed(globalSeed);

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            SpawnPoint sp = spawnPoints[i];
            if (sp == null || sp.anchor == null) continue;

            // Each point gets its own deterministic RNG stream.
            int pointSeed = HashCombine(roomSeed, i);
            var rng = new System.Random(pointSeed);

            int spawnCount = Mathf.Max(1, sp.count);
            for (int n = 0; n < spawnCount; n++)
            {
                // Roll none
                if (rng.NextDouble() < sp.chanceNone)
                    continue;

                GameObject chosen = PickWeighted(sp.options, rng);
                if (chosen == null)
                    continue;

                Transform parent = parentToRoom ? spawnedContainer : null;

                // Spawn at anchor position + rotation
                Instantiate(chosen, sp.anchor.position, sp.anchor.rotation, parent);
            }
        }
    }

    // ----------------- Weighted selection -----------------

    static GameObject PickWeighted(List<PrefabWeight> options, System.Random rng)
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
            if (roll <= acc)
                return o.prefab;
        }

        // Fallback (floating error)
        for (int i = options.Count - 1; i >= 0; i--)
            if (options[i] != null && options[i].prefab != null && options[i].weight > 0f)
                return options[i].prefab;

        return null;
    }

    // ----------------- Seeding -----------------

    int DeriveRoomSeed(int globalSeed)
    {
        // If you set stableRoomId on the prefab, you get rock-solid determinism.
        if (stableRoomId != 0)
            return HashCombine(globalSeed, stableRoomId);

        // Otherwise derive from quantized position + name (works well when rooms land deterministically).
        Vector3 p = transform.position;
        float q = Mathf.Max(0.0001f, positionQuantize);

        int px = Mathf.RoundToInt(p.x / q);
        int py = Mathf.RoundToInt(p.y / q);
        int pz = Mathf.RoundToInt(p.z / q);

        int nameHash = StableStringHash(gameObject.name);
        int posHash = HashCombine(px, HashCombine(py, pz));

        return HashCombine(globalSeed, HashCombine(nameHash, posHash));
    }

    static bool TryGetKartographSeed(out int seed)
    {
        // Kartograph README shows the Level Generator has an int "Seed". :contentReference[oaicite:1]{index=1}
        // We don't depend on Kartograph assemblies directly; we use reflection.
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

            // Try field "Seed" or "seed"
            FieldInfo f = t.GetField("Seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? t.GetField("seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (f != null && f.FieldType == typeof(int))
            {
                seed = (int)f.GetValue(mb);
                return true;
            }

            // Try property "Seed" or "seed"
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
        // Deterministic across sessions (unlike .NET's string.GetHashCode in some runtimes)
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
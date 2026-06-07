using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry that maps prefab / asset names to their UnityEngine.Object
/// references. Assign this asset in the GameSaveManager inspector (same pattern
/// as MissionRegistry).
///
/// Lookups are O(n) on first call and O(1) after the dictionaries are built.
/// The dictionaries are rebuilt automatically when the game is restarted or
/// the asset is hot-reloaded in the editor.
/// </summary>
[CreateAssetMenu(menuName = "DungeonBroker/ItemRegistry", fileName = "ItemRegistry")]
public class ItemRegistry : ScriptableObject
{
    [Header("Item Prefabs")]
    [Tooltip("Every item prefab that can be picked up, dropped, or stored. " +
             "Key used for lookup is the GameObject name of the prefab asset.")]
    public List<GameObject> itemPrefabs = new List<GameObject>();

    [Header("Effect Carriers")]
    [Tooltip("Every EffectCarrier ScriptableObject that may be saved as part of " +
             "an active status effect. Key used for lookup is the asset name.")]
    public List<EffectCarrier> effectCarriers = new List<EffectCarrier>();

    // ── Internal lookup caches ────────────────────────────────────────────

    private Dictionary<string, GameObject> _prefabMap;
    private Dictionary<string, EffectCarrier> _carrierMap;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the prefab whose asset name matches <paramref name="prefabName"/>,
    /// or null if none is registered.
    /// </summary>
    public GameObject GetPrefabByName(string prefabName)
    {
        BuildPrefabMapIfNeeded();
        _prefabMap.TryGetValue(prefabName, out var prefab);
        return prefab;
    }

    /// <summary>
    /// Returns the EffectCarrier whose asset name matches
    /// <paramref name="carrierName"/>, or null if none is registered.
    /// </summary>
    public EffectCarrier GetCarrierByName(string carrierName)
    {
        BuildCarrierMapIfNeeded();
        _carrierMap.TryGetValue(carrierName, out var carrier);
        return carrier;
    }

    // ── Cache builders ────────────────────────────────────────────────────

    private void BuildPrefabMapIfNeeded()
    {
        if (_prefabMap != null) return;

        _prefabMap = new Dictionary<string, GameObject>();
        foreach (var prefab in itemPrefabs)
        {
            if (prefab == null) continue;
            if (!_prefabMap.ContainsKey(prefab.name))
                _prefabMap[prefab.name] = prefab;
            else
                Debug.LogWarning($"[ItemRegistry] Duplicate prefab name '{prefab.name}' — only the first entry is used.");
        }
    }

    private void BuildCarrierMapIfNeeded()
    {
        if (_carrierMap != null) return;

        _carrierMap = new Dictionary<string, EffectCarrier>();
        foreach (var carrier in effectCarriers)
        {
            if (carrier == null) continue;
            if (!_carrierMap.ContainsKey(carrier.name))
                _carrierMap[carrier.name] = carrier;
            else
                Debug.LogWarning($"[ItemRegistry] Duplicate carrier name '{carrier.name}' — only the first entry is used.");
        }
    }

    // Invalidate caches when the asset is modified in the editor so hot-reload
    // picks up any newly added entries without requiring a domain reload.
    private void OnValidate()
    {
        _prefabMap = null;
        _carrierMap = null;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that acts as the single source of truth for every MissionData
///
/// Drag every MissionData asset into the <c>allMissions</c> list in the Inspector.
/// GameSaveManager uses this to convert saved Id strings back into asset references
/// on load.
/// </summary>
[CreateAssetMenu(fileName = "MissionRegistry", menuName = "Dungeon/Mission Registry")]
public class MissionRegistry : ScriptableObject
{
    [Tooltip("Every MissionData asset that exists in the project.")]
    public List<MissionData> allMissions = new List<MissionData>();

    // Built lazily at runtime so we never pay the cost in the editor.
    private Dictionary<string, MissionData> _lookup;

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, MissionData>(allMissions.Count);
        foreach (var m in allMissions)
        {
            if (m == null) continue;
            if (_lookup.ContainsKey(m.Id))
            {
                Debug.LogWarning($"[MissionRegistry] Duplicate Id '{m.Id}' — only the first entry is kept.");
                continue;
            }
            _lookup[m.Id] = m;
        }
    }

    /// <summary>
    /// Returns the MissionData whose Id matche id or null if not found.
    /// </summary>
    public MissionData GetById(string id)
    {
        if (_lookup == null) BuildLookup();
        if (string.IsNullOrWhiteSpace(id)) return null;
        _lookup.TryGetValue(id.Trim(), out var result);
        return result;
    }

    /// <summary>Clears the runtime lookup cache (call if you change the list at runtime).</summary>
    public void InvalidateCache() => _lookup = null;
}
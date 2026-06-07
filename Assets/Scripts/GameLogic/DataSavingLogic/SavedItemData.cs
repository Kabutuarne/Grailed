using System;
using System.Collections.Generic;

/// <summary>
/// Serializable snapshot of one item instance — either lying in the world or
/// sitting in the player's inventory. GameSaveData holds a List of these.
/// </summary>
[Serializable]
public class SavedItemData
{
    // ── Identity ──────────────────────────────────────────────────────────

    /// <summary>
    /// Matches the name of the prefab asset in ItemRegistry.
    /// Used to look up the correct prefab when re-instantiating on load.
    /// </summary>
    public string prefabName;

    // ── Location at save time ─────────────────────────────────────────────

    /// <summary>Where this item lives. Mutually exclusive flags.</summary>
    public ItemSaveLocation location;

    // World position / rotation — only valid when location == World.
    public float worldX, worldY, worldZ;
    public float worldRotX, worldRotY, worldRotZ, worldRotW; // quaternion components

    // Backpack slot index — only valid when location == Backpack.
    public int backpackSlot;

    // ── Wand payload ──────────────────────────────────────────────────────

    /// <summary>
    /// Only populated when this item is a WandItem. Each entry is either the
    /// prefab name of the scroll in that slot, or an empty string for an
    /// empty slot. Length matches WandItem.SlotCount at save time.
    /// </summary>
    public List<string> wandSlotPrefabNames = new List<string>();

    /// <summary>Which wand slot was selected at save time. -1 = none.</summary>
    public int wandSelectedIndex = -1;
}

/// <summary>Where an item was stored when the save was written.</summary>
public enum ItemSaveLocation
{
    World,      // lying on the ground in CabinScene
    Hand,       // equipped in the player's right hand
    Backpack    // stored in a backpack slot
}
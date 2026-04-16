using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemCatalog : MonoBehaviour
{
    [Serializable]
    public class ItemEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        public PlacementCategory[] allowedSpots;
        [Min(1)] public int maxPerRoom = 1;
    }

    public enum PlacementCategory
    {
        InChest,
        InShelf,
        OnGround,
        OnWall,
        OnTable,
        OnCounter,
        OnOther
    }

    [Header("All placeable item prefabs (single central list)")]
    public List<ItemEntry> items = new List<ItemEntry>();

    public static ItemCatalog Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Debug.LogWarning("Multiple ItemCatalog instances found in scene.");
    }
}

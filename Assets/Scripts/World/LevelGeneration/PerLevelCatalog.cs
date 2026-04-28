using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevelCatalog", menuName = "Dungeon/Level Catalog")]
public class PerLevelCatalog : ScriptableObject
{
    [Serializable]
    public class ItemEntry
    {
        public GameObject prefab;
        public PlacementCategory[] allowedSpots;
    }

    [Serializable]
    public class EnemyEntry
    {
        public GameObject prefab;
        public PlacementCategory[] allowedSpots;
    }

    public enum PlacementCategory { InChest, InShelf, OnGround, OnWall, OnTable, OnCounter, OnOther }

    [Header("Generator Settings")]
    [Tooltip("Number of sections to generate")]
    public int sectionAmount = 10;

    [Header("Catalogs")]
    public List<ItemEntry> items = new List<ItemEntry>();
    public List<EnemyEntry> enemies = new List<EnemyEntry>();
}
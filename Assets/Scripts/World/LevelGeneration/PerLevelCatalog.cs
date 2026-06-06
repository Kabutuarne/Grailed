using UnityEngine;
using System;

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

        [Header("Stat Override (optional)")]
        public bool overrideStats = false;

        [Tooltip("Only used if overrideStats is true")]
        public float strength = 10f;

        [Tooltip("Only used if overrideStats is true")]
        public float agility = 10f;

        [Tooltip("Only used if overrideStats is true")]
        public float intelligence = 10f;

        [Tooltip("Only used if overrideStats is true")]
        public float stamina = 10f;
    }

    public enum PlacementCategory { InChest, InShelf, OnGround, OnWall, OnTable, OnCounter, OnOther }

    [Header("Generator Settings")]
    [Tooltip("Number of sections to generate")]
    public int sectionAmount = 10;

    [Header("Catalogs")]
    public ItemEntry[] items;
    public EnemyEntry[] enemies;
}
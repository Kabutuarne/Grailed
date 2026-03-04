using UnityEngine;
using Kartograph;
using Kartograph.Entities;
using System.Collections;

public class KartographPlayerPlacer : MonoBehaviour
{
    [Header("Generator")]
    public LevelGenerator3D generator;

    [Header("Player")]
    public GameObject playerPrefab;

    [Header("Spawn")]
    public string startSpawnTag = "StartSpawn";

    bool placingPlayer;

    void Awake()
    {
        if (generator == null)
            generator = FindFirstObjectByType<LevelGenerator3D>();
    }

    public void OnGenerationTriggered()
    {
        if (!placingPlayer)
            StartCoroutine(PlacePlayerWhenReady());
    }

    IEnumerator PlacePlayerWhenReady()
    {
        placingPlayer = true;

        // Wait until Kartograph has created rooms
        Transform spawn = null;

        while (spawn == null)
        {
            GameObject spawnObj = GameObject.FindGameObjectWithTag(startSpawnTag);
            if (spawnObj != null)
                spawn = spawnObj.transform;

            yield return null; // wait one frame
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        else if (playerPrefab != null)
        {
            Instantiate(playerPrefab, spawn.position, spawn.rotation);
        }

        placingPlayer = false;
    }
}
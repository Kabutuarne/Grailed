using System;
using System.Collections;
using UnityEngine;
using Kartograph;
using Kartograph.Entities;

public class KartographGeneratorInteract : MonoBehaviour, IInteractable
{
    public LevelGenerator3D levelGenerator;
    public KartographPlayerPlacer playerPlacer;
    public GameObject globalLightSource;
    public bool generateOnlyOnce = true;

    bool generating;

    void Awake()
    {
        if (levelGenerator == null)
            levelGenerator = FindFirstObjectByType<LevelGenerator3D>();

        if (playerPlacer == null)
            playerPlacer = FindFirstObjectByType<KartographPlayerPlacer>();
    }

    public void Interact()
    {
        if (generating) return;

        if (levelGenerator == null)
        {
            Debug.LogWarning("Kartograph LevelGenerator not found.");
            return;
        }

        generating = true;

        try
        {
            levelGenerator.Generate(OnGenerationFinished);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            generating = false;
        }
    }

    void OnGenerationFinished()
    {
        Debug.Log("Kartograph generation finished.");
        if (globalLightSource != null)
            globalLightSource.SetActive(false);
        // First spawn items in the generated rooms, then place the player.
        StartCoroutine(SpawnItemsThenPlacePlayer());

        generating = false;
    }

    IEnumerator SpawnItemsThenPlacePlayer()
    {
        // Try to find an item spawner in the scene and let it spawn deterministically.
        var itemSpawner = FindFirstObjectByType<ItemRandomSpawner>();
        if (itemSpawner != null)
        {
            yield return StartCoroutine(itemSpawner.SpawnWhenReady());
        }

        if (playerPlacer != null)
            playerPlacer.OnGenerationTriggered();
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        Interact();
    }
}
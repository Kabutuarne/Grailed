using UnityEngine;
using Kartograph;
using Kartograph.Entities;

public class KartographGeneratorInteract : MonoBehaviour, IInteractable
{
    public LevelGenerator3D levelGenerator;
    public KartographPlayerPlacer playerPlacer;

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

        levelGenerator.Generate(OnGenerationFinished);
    }

    void OnGenerationFinished()
    {
        Debug.Log("Kartograph generation finished.");

        if (playerPlacer != null)
            playerPlacer.OnGenerationTriggered();

        generating = false;
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
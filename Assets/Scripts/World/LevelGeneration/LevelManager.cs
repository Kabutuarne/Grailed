using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;
using Sydewa;
using Kartograph.Entities;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("References")]
    public LevelGenerator3D generator;
    public LightingManager lightingManager;

    private PerLevelCatalog currentCatalog;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Auto-find components if not assigned
        if (generator == null)
            generator = FindFirstObjectByType<LevelGenerator3D>();

        if (lightingManager == null)
            lightingManager = FindFirstObjectByType<LightingManager>();

        var catalog = GameManager.Instance?.ActiveLevel;
        if (catalog != null)
        {
            StartCoroutine(GenerateLevel(catalog));
        }
    }

    public IEnumerator GenerateLevel(PerLevelCatalog catalog)
    {
        currentCatalog = catalog;

        Debug.Log($"[LevelManager] Starting generation for catalog '{catalog.name}' ");

        // Set time to 9 AM
        if (lightingManager != null)
            lightingManager.SetTime(9f);

        // Lock player controls while generation is running
        PlayerController scenePlayerController = null;
        var playerObjBefore = GameObject.FindWithTag("Player");
        if (playerObjBefore != null)
            scenePlayerController = playerObjBefore.GetComponent<PlayerController>();
        if (scenePlayerController != null)
            scenePlayerController.SetControlLocked(true);

        // Configure and generate level
        if (generator != null)
        {
            // Use reflection to call the protected SetMaxLevelSize method
            var method = generator.GetType().BaseType?.GetMethod("SetMaxLevelSize",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(generator, new object[] { catalog.sectionAmount });
            }
            else
            {
                Debug.LogWarning("Could not find SetMaxLevelSize method on LevelGenerator3D");
            }

            bool generationDone = false;
            generator.Generate(() => generationDone = true);
            yield return new WaitUntil(() => generationDone);
        }

        // Allow the scene one frame to settle and for generated spawner objects to become available.
        yield return null;

        // Spawn items and enemies
        EntitySpawner.PopulateLevel(catalog);

        // Teleport player to spawn
        TeleportPlayerToSpawn();

        // Bake NavMesh after level is fully created
        yield return StartCoroutine(BakeNavMesh());

        Debug.Log("[LevelManager] Level generation, entity population, and NavMesh baking complete.");

        // Unlock player controls now that everything is complete
        if (scenePlayerController != null)
            scenePlayerController.SetControlLocked(false);
    }

    private IEnumerator BakeNavMesh()
    {
        // Find NavMeshSurface by tag
        GameObject navMeshObject = GameObject.FindWithTag("NavMeshSurface");

        if (navMeshObject == null)
        {
            Debug.LogWarning("[LevelManager] No GameObject with tag 'NavMeshSurface' found. Skipping NavMesh bake.");
            yield break;
        }

        NavMeshSurface surface = navMeshObject.GetComponent<NavMeshSurface>();

        if (surface == null)
        {
            Debug.LogWarning("[LevelManager] Found GameObject with tag 'NavMeshSurface' but it has no NavMeshSurface component. Skipping NavMesh bake.");
            yield break;
        }

        Debug.Log("[LevelManager] Starting NavMesh bake...");

        // Bake the NavMesh
        surface.BuildNavMesh();

        // Wait one frame to ensure the NavMesh is fully built
        yield return null;

        Debug.Log("[LevelManager] NavMesh bake complete.");
    }

    public void TeleportPlayerToSpawn()
    {
        var spawnPoint = GameObject.FindWithTag("StartTransform");
        var player = GameObject.FindWithTag("Player");

        if (!spawnPoint || !player)
            return;

        var charController = player.GetComponent<CharacterController>();
        if (charController != null)
            charController.enabled = false;

        player.transform.position = spawnPoint.transform.position;
        player.transform.rotation = spawnPoint.transform.rotation;

        if (charController != null)
            charController.enabled = true;
    }

    public void TeleportPlayerToRespawn()
    {
        var respawnPoint = GameObject.FindWithTag("RespawnTransform");
        var player = GameObject.FindWithTag("Player");

        if (!respawnPoint || !player)
            return;

        var charController = player.GetComponent<CharacterController>();
        if (charController != null)
            charController.enabled = false;

        player.transform.position = respawnPoint.transform.position;

        if (charController != null)
            charController.enabled = true;
    }

    public void AddTime(float hours)
    {
        if (lightingManager != null)
            lightingManager.AddTime(hours);
    }
}
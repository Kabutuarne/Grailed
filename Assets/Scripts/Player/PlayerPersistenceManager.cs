using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Persists the player GameObject across scene changes and positions it at
/// the correct spawn point when CabinScene loads.
///
/// Position priority when entering CabinScene:
///   1. New save (introHasPlayed == false) -- do not move the player at all.
///      The player is already placed correctly in the scene.
///   2. Existing save (hasSavedPosition == true) -- teleport to saved coords.
///   3. Returning from a mission with a spawn-point tag -- teleport to tag.
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private string spawnPointTag = "PlayerSpawnPoint";
    [SerializeField] private float spawnDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool logEvents = false;

    // =====================================================================
    // Cached component references
    // =====================================================================

    private PlayerStats playerStats;
    private PlayerInventory playerInventory;
    private PlayerController playerController;
    private StatusEffects statusEffects;
    private PlayerUI playerUI;

    // =====================================================================
    // Lifecycle
    // =====================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (logEvents)
                Debug.Log($"[PlayerPersistenceManager] Duplicate on {gameObject.name} destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => CacheComponents();
    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // =====================================================================
    // Public component accessors
    // =====================================================================

    public PlayerStats GetPlayerStats() => playerStats ??= GetComponent<PlayerStats>();
    public PlayerInventory GetPlayerInventory() => playerInventory ??= GetComponent<PlayerInventory>();
    public PlayerController GetPlayerController() => playerController ??= GetComponent<PlayerController>();
    public StatusEffects GetStatusEffects() => statusEffects ??= GetComponent<StatusEffects>();
    public PlayerUI GetPlayerUI() => playerUI ??= GetComponent<PlayerUI>();

    public void RefreshPlayerComponents() => CacheComponents();

    // =====================================================================
    // Public teleport API
    // =====================================================================

    /// <summary>
    /// Teleports the player to the first GameObject found with the given tag.
    /// Uses the default spawnPointTag when no custom tag is supplied.
    /// </summary>
    public void MovePlayerToSpawnPoint(string customTag = null)
    {
        StartCoroutine(TeleportToTagCoroutine(customTag ?? spawnPointTag));
    }

    /// <summary>
    /// Teleports the player to an explicit world position and yaw rotation.
    /// </summary>
    public void MovePlayerToPosition(Vector3 position, float yaw)
    {
        StartCoroutine(TeleportToPositionCoroutine(position, yaw));
    }

    // =====================================================================
    // Private -- scene loaded handler
    // =====================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenuScene" || scene.name == "MainTestScene")
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] Menu scene loaded. Destroying.");
            Instance = null;
            Destroy(gameObject);
            return;
        }

        if (logEvents)
            Debug.Log($"[PlayerPersistenceManager] Scene '{scene.name}' loaded.");

        if (scene.name == "CabinScene")
            HandleCabinSpawn();
    }

    /// <summary>
    /// Decides how to position the player when CabinScene loads.
    ///
    /// New save (introHasPlayed == false):
    ///   The player is already placed in the scene by Unity. Do nothing.
    ///
    /// Existing save (hasSavedPosition == true):
    ///   Restore the exact saved world position.
    ///
    /// No save active or no position on file:
    ///   Fall back to the spawn-point tag.
    /// </summary>
    private void HandleCabinSpawn()
    {
        var gsm = GameSaveManager.Instance;

        if (gsm == null || gsm.ActiveSave == null)
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] No active save -- using spawn-point tag.");
            MovePlayerToSpawnPoint();
            return;
        }

        // First-ever load of this save: player is already at the correct
        // in-scene position, so we must not teleport them anywhere.
        if (!gsm.ActiveSave.introHasPlayed)
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] New save -- skipping teleport, player stays in place.");
            return;
        }

        // Returning to cabin with a saved position (after a previous quit).
        if (gsm.ShouldSkipSpawnPoint)
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] Restoring saved position.");
            var save = gsm.ActiveSave;
            MovePlayerToPosition(new Vector3(save.posX, save.posY, save.posZ), save.rotY);
            return;
        }

        // Fallback: use spawn-point tag (e.g. returning from a mission before
        // any save-and-quit has ever been performed).
        if (logEvents)
            Debug.Log("[PlayerPersistenceManager] No saved position -- using spawn-point tag.");
        MovePlayerToSpawnPoint();
    }

    // =====================================================================
    // Private -- teleport coroutines
    // =====================================================================

    private IEnumerator TeleportToTagCoroutine(string tag)
    {
        yield return new WaitForSeconds(spawnDelay);

        GameObject spawnPoint = GameObject.FindGameObjectWithTag(tag);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[PlayerPersistenceManager] Spawn point with tag '{tag}' not found.");
            yield break;
        }

        TeleportPlayer(spawnPoint.transform.position, spawnPoint.transform.eulerAngles.y);

        if (logEvents)
            Debug.Log($"[PlayerPersistenceManager] Teleported to tag '{tag}'.");
    }

    private IEnumerator TeleportToPositionCoroutine(Vector3 position, float yaw)
    {
        yield return new WaitForSeconds(spawnDelay);

        TeleportPlayer(position, yaw);

        if (logEvents)
            Debug.Log($"[PlayerPersistenceManager] Teleported to saved position {position}.");
    }

    /// <summary>
    /// Disables the CharacterController, moves the player, then re-enables it
    /// so the controller does not fight the position assignment.
    /// </summary>
    private void TeleportPlayer(Vector3 position, float yaw)
    {
        var cc = playerController != null
            ? playerController.GetComponent<CharacterController>()
            : GetComponentInChildren<CharacterController>();

        GameObject playerObject = cc != null
            ? cc.gameObject
            : (playerController != null ? playerController.gameObject : gameObject);

        if (cc != null) cc.enabled = false;

        playerObject.transform.position = position;
        playerObject.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cc != null) cc.enabled = true;
    }

    // =====================================================================
    // Private -- component caching
    // =====================================================================

    private void CacheComponents()
    {
        playerStats = GetComponent<PlayerStats>();
        playerInventory = GetComponent<PlayerInventory>();
        playerController = GetComponent<PlayerController>();
        statusEffects = GetComponent<StatusEffects>();
        playerUI = GetComponent<PlayerUI>();

        if (!logEvents) return;
        if (playerStats == null) Debug.LogWarning("[PlayerPersistenceManager] PlayerStats not found.");
        if (playerInventory == null) Debug.LogWarning("[PlayerPersistenceManager] PlayerInventory not found.");
        if (playerController == null) Debug.LogWarning("[PlayerPersistenceManager] PlayerController not found.");
        if (statusEffects == null) Debug.LogWarning("[PlayerPersistenceManager] StatusEffects not found.");
        if (playerUI == null) Debug.LogWarning("[PlayerPersistenceManager] PlayerUI not found.");
    }
}
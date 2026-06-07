using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Persists the player GameObject across scene changes and positions it at
/// the correct spawn point when CabinScene loads.
///
/// Position priority when entering CabinScene:
///   1. Returning from a mission (LastSpawnPointTag PlayerPref is set) --
///      always wins, regardless of save state. Pref is deleted after use.
///   2. New save first load (introHasPlayed == false) -- do not move the
///      player. They are already placed for the intro cutscene.
///   3. Existing save (hasSavedPosition == true) -- teleport to saved coords.
///   4. Fallback -- teleport to the default spawnPointTag.
///
/// Camera snap is triggered here after every teleport so that CabinCameraSnap
/// does not need to read the PlayerPref (which is already deleted by then).
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    // Written by ReturnToLobbyInteractable, read and deleted here.
    private const string LastSpawnPointTagKey = "LastSpawnPointTag";

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

    private void Start()
    {
        CacheComponents();

        // OnSceneLoaded never fires for the scene this object was born in,
        // because the event is registered in OnEnable which runs after the
        // scene is already active. Call HandleCabinSpawn manually here so
        // the very first CabinScene load is handled correctly.
        if (SceneManager.GetActiveScene().name == "CabinScene")
            HandleCabinSpawn();
    }

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

        // The birth-scene case is handled in Start() instead, so OnSceneLoaded
        // only handles CabinScene when returning to it from another scene.
        if (scene.name == "CabinScene")
            HandleCabinSpawn();
    }

    /// <summary>
    /// Decides how to position the player when CabinScene becomes active.
    /// Called from Start() on first load and from OnSceneLoaded on return.
    /// </summary>
    private void HandleCabinSpawn()
    {
        // ------------------------------------------------------------------
        // PRIORITY 1 -- returning from a mission.
        // Checked before everything else so it overrides both the new-save
        // early-return and the saved-position restore.
        // ------------------------------------------------------------------
        if (PlayerPrefs.HasKey(LastSpawnPointTagKey))
        {
            string missionReturnTag = PlayerPrefs.GetString(LastSpawnPointTagKey);

            // Delete immediately so a crash never leaves a stale pref.
            PlayerPrefs.DeleteKey(LastSpawnPointTagKey);
            PlayerPrefs.Save();

            if (logEvents)
                Debug.Log($"[PlayerPersistenceManager] Mission return -- teleporting to '{missionReturnTag}'.");

            // snapCamera: true -- the cutscene is over, camera must follow.
            StartCoroutine(TeleportToTagCoroutine(missionReturnTag, snapCamera: true));
            return;
        }

        var gsm = GameSaveManager.Instance;

        // No active save -- editor direct-play without going through the menu.
        if (gsm == null || gsm.ActiveSave == null)
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] No active save -- using spawn-point tag.");
            StartCoroutine(TeleportToTagCoroutine(spawnPointTag, snapCamera: true));
            return;
        }

        // ------------------------------------------------------------------
        // PRIORITY 2 -- brand-new save, very first load.
        // Player is already at the correct intro position. Do not teleport.
        // Do not snap the camera either -- the intro cutscene owns it.
        // ------------------------------------------------------------------
        if (!gsm.ActiveSave.introHasPlayed)
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] New save -- skipping teleport and camera snap.");
            return;
        }

        // ------------------------------------------------------------------
        // PRIORITY 3 -- existing save with a recorded position.
        // ------------------------------------------------------------------
        if (gsm.ShouldSkipSpawnPoint)
        {
            if (logEvents)
                Debug.Log("[PlayerPersistenceManager] Restoring saved position.");
            var save = gsm.ActiveSave;
            StartCoroutine(TeleportToPositionCoroutine(
                new Vector3(save.posX, save.posY, save.posZ), save.rotY, snapCamera: true));
            return;
        }

        // ------------------------------------------------------------------
        // PRIORITY 4 -- fallback.
        // ------------------------------------------------------------------
        if (logEvents)
            Debug.Log("[PlayerPersistenceManager] No saved position -- using default spawn-point tag.");
        StartCoroutine(TeleportToTagCoroutine(spawnPointTag, snapCamera: true));
    }

    // =====================================================================
    // Private -- teleport coroutines
    // =====================================================================

    private IEnumerator TeleportToTagCoroutine(string tag, bool snapCamera = false)
    {
        yield return new WaitForSeconds(spawnDelay);

        GameObject spawnPoint = GameObject.FindGameObjectWithTag(tag);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[PlayerPersistenceManager] Spawn point with tag '{tag}' not found.");
            yield break;
        }

        TeleportPlayer(spawnPoint.transform.position, spawnPoint.transform.eulerAngles.y);

        if (snapCamera)
            SnapCameraToPlayer();

        if (logEvents)
            Debug.Log($"[PlayerPersistenceManager] Teleported to tag '{tag}'.");
    }

    private IEnumerator TeleportToPositionCoroutine(Vector3 position, float yaw, bool snapCamera = false)
    {
        yield return new WaitForSeconds(spawnDelay);

        TeleportPlayer(position, yaw);

        if (snapCamera)
            SnapCameraToPlayer();

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
    // Private -- camera snap
    // =====================================================================

    /// <summary>
    /// Snaps the main camera to the Cinemachine virtual camera tagged
    /// "PlayerCamera". Called immediately after every teleport except the
    /// new-save intro load. Centralised here so CabinCameraSnap no longer
    /// needs to read the (already-deleted) PlayerPref.
    /// </summary>
    private void SnapCameraToPlayer()
    {
        GameObject mainCamObj = GameObject.FindWithTag("MainCamera");
        if (mainCamObj == null)
        {
            Debug.LogWarning("[PlayerPersistenceManager] No GameObject tagged 'MainCamera' found.");
            return;
        }

        GameObject vcamObj = GameObject.FindWithTag("PlayerCamera");
        if (vcamObj == null)
        {
            Debug.LogWarning("[PlayerPersistenceManager] No GameObject tagged 'PlayerCamera' found.");
            return;
        }

        // Cinemachine has not yet driven the camera for this frame, so the
        // vcam's own transform already reflects where it will end up.
        mainCamObj.transform.SetPositionAndRotation(
            vcamObj.transform.position,
            vcamObj.transform.rotation
        );

        if (logEvents)
            Debug.Log("[PlayerPersistenceManager] Camera snapped to PlayerCamera.");
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
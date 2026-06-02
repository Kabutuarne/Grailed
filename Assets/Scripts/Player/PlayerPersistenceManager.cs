using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists player-related data across scene changes.
/// This script should be attached to a player GameObject or a dedicated manager GameObject.
/// It will not be destroyed when scenes change, allowing player state to survive transitions.
/// Also manages caching of scene-specific UI elements.
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Persistence Settings")]
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool logPersistenceEvents = false;

    // ── player references ─────────────────────────────────────────────────────
    private PlayerStats playerStats;
    private PlayerInventory playerInventory;
    private PlayerController playerController;
    private StatusEffects statusEffects;
    private PlayerUI playerUI;

    // ── Cached UI element references ──────────────────────────────────────────
    private Canvas cachedCanvas;
    private GameObject cachedHudRoot;
    private GameObject cachedBackpackRoot;
    private Transform cachedStatusEffectsRoot;
    private Transform cachedStatusEffectsHudRoot;
    private InventorySlotUI cachedRightHandSlot;
    private InventorySlotUI[] cachedBackpackSlots;
    private InventorySlotUI[] cachedAccessorySlots;
    private ItemDescriptionContainer cachedDescriptionContainer;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Singleton pattern: ensure only one instance exists
        if (Instance != null && Instance != this)
        {
            if (logPersistenceEvents)
                Debug.Log($"Duplicate PlayerPersistenceManager detected. Destroying duplicate on {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep this GameObject alive across scene changes
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
            if (logPersistenceEvents)
                Debug.Log($"PlayerPersistenceManager marked for persistence across scenes");
        }
    }

    void Start()
    {
        // Cache references to player components
        CachePlayerComponents();

        // Initial UI caching for the current scene
        CacheUIElements();
        BindUIToPlayerUI();
    }

    void OnEnable()
    {
        // Subscribe to scene load events to refresh UI references
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current PlayerStats component. Creates a reference if not already cached.
    /// </summary>
    public PlayerStats GetPlayerStats()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
        return playerStats;
    }

    /// <summary>
    /// Returns the current PlayerInventory component. Creates a reference if not already cached.
    /// </summary>
    public PlayerInventory GetPlayerInventory()
    {
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();
        return playerInventory;
    }

    /// <summary>
    /// Returns the current PlayerController component. Creates a reference if not already cached.
    /// </summary>
    public PlayerController GetPlayerController()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        return playerController;
    }

    /// <summary>
    /// Returns the current StatusEffects component. Creates a reference if not already cached.
    /// </summary>
    public StatusEffects GetStatusEffects()
    {
        if (statusEffects == null)
            statusEffects = GetComponent<StatusEffects>();
        return statusEffects;
    }

    /// <summary>
    /// Returns the current PlayerUI component. Creates a reference if not already cached.
    /// </summary>
    public PlayerUI GetPlayerUI()
    {
        if (playerUI == null)
            playerUI = GetComponent<PlayerUI>();
        return playerUI;
    }

    /// <summary>
    /// Refreshes all cached player component references.
    /// Call this after respawning or reloading the player.
    /// </summary>
    public void RefreshPlayerComponents()
    {
        CachePlayerComponents();
        if (logPersistenceEvents)
            Debug.Log("Player component references refreshed");
    }

    /// <summary>
    /// Enables or disables persistence for this manager.
    /// </summary>
    public void SetPersistence(bool shouldPersist)
    {
        persistAcrossScenes = shouldPersist;
        if (logPersistenceEvents)
            Debug.Log($"Player persistence set to: {shouldPersist}");
    }

    // ── Cached UI accessors ───────────────────────────────────────────────────

    public Canvas GetCachedCanvas() => cachedCanvas;
    public GameObject GetCachedHudRoot() => cachedHudRoot;
    public GameObject GetCachedBackpackRoot() => cachedBackpackRoot;
    public Transform GetCachedStatusEffectsRoot() => cachedStatusEffectsRoot;
    public Transform GetCachedStatusEffectsHudRoot() => cachedStatusEffectsHudRoot;
    public InventorySlotUI GetCachedRightHandSlot() => cachedRightHandSlot;
    public InventorySlotUI[] GetCachedBackpackSlots() => cachedBackpackSlots;
    public InventorySlotUI[] GetCachedAccessorySlots() => cachedAccessorySlots;
    public ItemDescriptionContainer GetCachedDescriptionContainer() => cachedDescriptionContainer;

    // ── private methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Called when a scene finishes loading. Caches all UI elements from the new scene
    /// and binds them to PlayerUI.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Destroy this persistent object when returning to the main menu
        if (scene.name == "MainMenuScene")
        {
            if (logPersistenceEvents)
                Debug.Log("MainMenuScene loaded. Destroying PlayerPersistenceManager.");

            Instance = null;
            Destroy(gameObject);
            return;
        }

        if (logPersistenceEvents)
            Debug.Log($"Scene '{scene.name}' loaded. Caching UI elements...");

        CacheUIElements();
        BindUIToPlayerUI();
    }

    /// <summary>
    /// Searches the current scene for all UI elements and caches them.
    /// </summary>
    private void CacheUIElements()
    {
        // Find Canvas
        cachedCanvas = FindFirstObjectByType<Canvas>();
        if (cachedCanvas == null && logPersistenceEvents)
            Debug.LogWarning("No Canvas found in scene");

        // Find root objects by searching for common parent GameObjects
        cachedHudRoot = GameObject.Find("hudRoot") ?? FindGameObjectByName("hudRoot");
        cachedBackpackRoot = GameObject.Find("backpackRoot") ?? FindGameObjectByName("backpackRoot");

        // Find status effect roots
        if (cachedBackpackRoot != null)
        {
            var statusRoot = cachedBackpackRoot.transform.Find("StatusEffectsRoot");
            if (statusRoot != null)
                cachedStatusEffectsRoot = statusRoot;
        }

        if (cachedHudRoot != null)
        {
            var statusHudRoot = cachedHudRoot.transform.Find("StatusEffectsHudRoot");
            if (statusHudRoot != null)
                cachedStatusEffectsHudRoot = statusHudRoot;
        }

        // If not found by hierarchy search, try to find any existing ones
        if (cachedStatusEffectsRoot == null)
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.gameObject.name == "StatusEffectsRoot")
                {
                    cachedStatusEffectsRoot = t;
                    break;
                }
            }
        }

        if (cachedStatusEffectsHudRoot == null)
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.gameObject.name == "StatusEffectsHudRoot")
                {
                    cachedStatusEffectsHudRoot = t;
                    break;
                }
            }
        }

        // Find inventory slots - search for InventorySlotUI components in the scene
        InventorySlotUI[] allSlots = FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // Separate slots by type
        System.Collections.Generic.List<InventorySlotUI> backpackList = new System.Collections.Generic.List<InventorySlotUI>();
        System.Collections.Generic.List<InventorySlotUI> accessoryList = new System.Collections.Generic.List<InventorySlotUI>();

        foreach (var slot in allSlots)
        {
            if (slot.slotType == InventorySlotUI.SlotType.Backpack)
                backpackList.Add(slot);
            else if (slot.slotType == InventorySlotUI.SlotType.Accessory)
                accessoryList.Add(slot);
            else if (slot.slotType == InventorySlotUI.SlotType.RightHand)
                cachedRightHandSlot = slot;
        }

        cachedBackpackSlots = backpackList.ToArray();
        cachedAccessorySlots = accessoryList.ToArray();

        // Find description container
        cachedDescriptionContainer = FindFirstObjectByType<ItemDescriptionContainer>();

        if (logPersistenceEvents)
        {
            Debug.Log($"Cached UI Elements: Canvas={cachedCanvas != null}, RightHandSlot={cachedRightHandSlot != null}, " +
                      $"BackpackSlots={cachedBackpackSlots.Length}, AccessorySlots={cachedAccessorySlots.Length}");
        }
    }

    /// <summary>
    /// Assigns cached UI elements to PlayerUI so it can use them.
    /// </summary>
    private void BindUIToPlayerUI()
    {
        playerUI = GetPlayerUI();
        if (playerUI == null)
        {
            if (logPersistenceEvents)
                Debug.LogWarning("PlayerUI component not found - cannot bind UI elements");
            return;
        }

        // Assign cached UI elements to PlayerUI
        playerUI.uiCanvas = cachedCanvas;
        playerUI.hudRoot = cachedHudRoot;
        playerUI.backpackRoot = cachedBackpackRoot;
        playerUI.statusEffectsRoot = cachedStatusEffectsRoot;
        playerUI.statusEffectsHudRoot = cachedStatusEffectsHudRoot;
        playerUI.rightHandSlot = cachedRightHandSlot;
        playerUI.backpackSlots = cachedBackpackSlots;
        playerUI.accessorySlots = cachedAccessorySlots;
        playerUI.descriptionContainerInstance = cachedDescriptionContainer;

        // Trigger UI update
        playerUI.RebindComplete();

        if (logPersistenceEvents)
            Debug.Log("UI elements bound to PlayerUI");
    }

    /// <summary>
    /// Caches references to all player-related components attached to this GameObject.
    /// </summary>
    private void CachePlayerComponents()
    {
        playerStats = GetComponent<PlayerStats>();
        playerInventory = GetComponent<PlayerInventory>();
        playerController = GetComponent<PlayerController>();
        statusEffects = GetComponent<StatusEffects>();
        playerUI = GetComponent<PlayerUI>();

        if (logPersistenceEvents)
        {
            if (playerStats == null) Debug.LogWarning("PlayerStats component not found");
            if (playerInventory == null) Debug.LogWarning("PlayerInventory component not found");
            if (playerController == null) Debug.LogWarning("PlayerController component not found");
            if (statusEffects == null) Debug.LogWarning("StatusEffects component not found");
            if (playerUI == null) Debug.LogWarning("PlayerUI component not found");
        }
    }

    /// <summary>
    /// Helper to find a GameObject by name in the current scene.
    /// </summary>
    private GameObject FindGameObjectByName(string name)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name == name && go.activeInHierarchy)
                return go;
        }
        return null;
    }
}

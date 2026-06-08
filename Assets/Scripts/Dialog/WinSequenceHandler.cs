using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to a DoorSequenceData that ends the game with a win.
/// Shows a "You Won" overlay and returns to main menu on button click.
/// Trigger this via Timeline Signal Receiver at the point of win.
/// </summary>
public class WinSequenceHandler : MonoBehaviour
{
    [Header("UI Prefab")]
    [Tooltip("Canvas prefab containing a Text ('You Won') and a Button ('To Main Menu').")]
    [SerializeField] private GameObject winCanvasPrefab;

    [Header("Optional Overrides")]
    [Tooltip("Leave empty to use the prefab's own text. Assign to override the message.")]
    [SerializeField] private Text messageTextOverride;

    private MissionDoorInteractable door;
    private GameObject instantiatedCanvas;
    private bool hasTriggered = false;

    private void Start()
    {
        // Walk up/down the hierarchy to find the door that owns this sequence.
        door = GetComponentInParent<MissionDoorInteractable>();
        if (door == null)
            door = GetComponentInChildren<MissionDoorInteractable>();

        if (door == null)
        {
            Debug.LogError($"[WinSequenceHandler] No MissionDoorInteractable found near {gameObject.name}.");
            return;
        }

        // Subscribe to sequence ended as fallback, but prefer Timeline signal
        door.OnAnySequenceEnded += OnSequenceEndedFallback;

        Debug.Log($"[WinSequenceHandler] Initialized on door '{door.name}'. Trigger via Timeline Signal or sequence end.");
    }

    private void OnDestroy()
    {
        if (door != null)
            door.OnAnySequenceEnded -= OnSequenceEndedFallback;

        // Clean up canvas if it exists
        if (instantiatedCanvas != null)
            Destroy(instantiatedCanvas);
    }

    /// <summary>
    /// Call this method from a Timeline Signal Receiver when the win condition is met.
    /// This is the primary trigger method.
    /// </summary>
    public void ShowWinOverlay()
    {
        if (hasTriggered)
        {
            Debug.LogWarning("[WinSequenceHandler] Win already triggered, ignoring duplicate.");
            return;
        }

        DoorSequenceData mySequence = GetComponent<DoorSequenceData>();
        if (mySequence == null)
        {
            Debug.LogError("[WinSequenceHandler] No DoorSequenceData found on same GameObject!");
            return;
        }

        // Verify this is the currently playing sequence
        if (door != null && door.GetCurrentSequence() != mySequence)
        {
            Debug.LogWarning($"[WinSequenceHandler] Sequence '{mySequence.name}' is not the current sequence. Current: {door?.GetCurrentSequence()?.name ?? "NULL"}");
            return;
        }

        Debug.Log($"[WinSequenceHandler] Win triggered for sequence: {mySequence.name}");
        hasTriggered = true;

        // Claim the sequence end to prevent any auto-quit behavior
        if (door != null)
            door.ClaimSequenceEnd();

        // Show the win UI
        DisplayWinUI();
    }

    /// <summary>
    /// Fallback for sequences without Timeline signals or legacy support.
    /// </summary>
    private void OnSequenceEndedFallback()
    {
        DoorSequenceData mySequence = GetComponent<DoorSequenceData>();

        Debug.Log($"[WinSequenceHandler] OnSequenceEnded fallback fired. mySequence={mySequence?.name ?? "NULL"}, " +
                  $"doorSequence={door?.GetCurrentSequence()?.name ?? "NULL"}, " +
                  $"hasTriggered={hasTriggered}");

        if (mySequence == null) return;
        if (hasTriggered) return;
        if (door.GetCurrentSequence() != mySequence) return;

        // Only use fallback if this sequence is meant to end the game
        // You can add a flag in DoorSequenceData if needed, or assume win/loss sequences have no next sequences
        if (mySequence.nextSequenceWithItem == null && mySequence.nextSequenceWithoutItem == null)
        {
            Debug.Log($"[WinSequenceHandler] No follow-up sequences, treating as terminal win via fallback.");
            hasTriggered = true;
            door.ClaimSequenceEnd();
            DisplayWinUI();
        }
    }

    private void DisplayWinUI()
    {
        if (winCanvasPrefab == null)
        {
            Debug.LogError("[WinSequenceHandler] winCanvasPrefab is not assigned!");
            return;
        }

        instantiatedCanvas = Instantiate(winCanvasPrefab);
        DontDestroyOnLoad(instantiatedCanvas); // Persist through scene changes if needed

        // Set the message text, preferring the override if supplied.
        if (messageTextOverride != null)
        {
            messageTextOverride.text = "You Won!";
        }
        else
        {
            Text prefabText = instantiatedCanvas.GetComponentInChildren<Text>();
            if (prefabText != null)
                prefabText.text = "You Won!";
        }

        // Wire the main-menu button.
        Button mainMenuButton = instantiatedCanvas.GetComponentInChildren<Button>();
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        else
            Debug.LogWarning("[WinSequenceHandler] No Button found in win canvas prefab.");

        // Lock player and show cursor so they can click the button.
        LockPlayer(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[WinSequenceHandler] Win UI displayed");
    }

    private void ReturnToMainMenu()
    {
        Debug.Log("[WinSequenceHandler] Returning to main menu...");

        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SaveAndQuitToMainMenu();
        else
            SceneManager.LoadScene("MainMenuScene");
    }

    private void LockPlayer(bool locked)
    {
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.SetControlLocked(locked);

        PlayerInteractor pi = FindObjectOfType<PlayerInteractor>();
        if (pi != null) pi.SetInteractionLocked(locked);

        // Also disable cast/consume components so no abilities fire on the win screen.
        var player = pc != null ? pc.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            foreach (var c in player.GetComponents<PlayerCast>()) if (c != null) c.enabled = !locked;
            foreach (var c in player.GetComponents<PlayerConsume>()) if (c != null) c.enabled = !locked;
        }
    }
}
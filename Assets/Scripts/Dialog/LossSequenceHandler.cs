using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to a DoorSequenceData that ends the game with a loss.
/// Enables an existing "You Failed" overlay UI and returns to main menu on button click.
/// Trigger this via Timeline Signal Receiver at the point of loss.
/// </summary>
public class LossSequenceHandler : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Canvas containing the loss UI (Text 'You Failed' and Button 'To Main Menu').")]
    [SerializeField] private GameObject lossCanvas;

    [Header("Optional Overrides")]
    [Tooltip("Leave empty to use the canvas's own text. Assign to override the message.")]
    [SerializeField] private Text messageTextOverride;

    private MissionDoorInteractable door;
    private bool hasTriggered = false;
    private Button mainMenuButton;

    private void Start()
    {
        door = GetComponentInParent<MissionDoorInteractable>();
        if (door == null)
            door = GetComponentInChildren<MissionDoorInteractable>();

        Debug.Log($"[LossHandler] Start - door found: {(door != null ? door.name : "NULL")}, " +
                  $"mySequence: {GetComponent<DoorSequenceData>()?.name ?? "NULL"}");

        if (door == null)
        {
            Debug.LogError($"[LossHandler] No MissionDoorInteractable found near {gameObject.name}.");
            return;
        }

        // Find UI references if not assigned
        if (lossCanvas == null)
        {
            lossCanvas = GameObject.FindGameObjectWithTag("LossCanvas");
            if (lossCanvas == null)
                Debug.LogWarning("[LossHandler] No loss canvas assigned or found with tag 'LossCanvas'");
        }

        if (lossCanvas != null)
        {
            // Ensure canvas starts disabled
            lossCanvas.SetActive(false);

            // Find button
            mainMenuButton = lossCanvas.GetComponentInChildren<Button>();
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        // Subscribe to sequence ended as fallback
        door.OnAnySequenceEnded += OnSequenceEndedFallback;
        Debug.Log($"[LossHandler] Successfully subscribed to door '{door.name}'");
    }

    private void OnDestroy()
    {
        if (door != null)
            door.OnAnySequenceEnded -= OnSequenceEndedFallback;

        // Clean up button listener
        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    /// <summary>
    /// Call this method from a Timeline Signal Receiver when the loss condition is met.
    /// This is the primary trigger method.
    /// </summary>
    public void ShowLossOverlay()
    {
        if (hasTriggered)
        {
            Debug.LogWarning("[LossHandler] Loss already triggered, ignoring duplicate.");
            return;
        }

        DoorSequenceData mySequence = GetComponent<DoorSequenceData>();
        if (mySequence == null)
        {
            Debug.LogError("[LossHandler] No DoorSequenceData found on same GameObject!");
            return;
        }

        // Verify this is the currently playing sequence
        if (door != null && door.GetCurrentSequence() != mySequence)
        {
            Debug.LogWarning($"[LossHandler] Sequence '{mySequence.name}' is not the current sequence. Current: {door?.GetCurrentSequence()?.name ?? "NULL"}");
            return;
        }

        Debug.Log($"[LossHandler] Loss triggered for sequence: {mySequence.name}");
        hasTriggered = true;

        // Claim the sequence end to prevent any auto-quit behavior
        if (door != null)
            door.ClaimSequenceEnd();

        // Enable the loss UI
        DisplayLossUI();
    }

    /// <summary>
    /// Fallback for sequences without Timeline signals or legacy support.
    /// </summary>
    private void OnSequenceEndedFallback()
    {
        DoorSequenceData mySequence = GetComponent<DoorSequenceData>();

        Debug.Log($"[LossHandler] OnSequenceEnded fallback fired. mySequence={mySequence?.name ?? "NULL"}, " +
                  $"doorSequence={door?.GetCurrentSequence()?.name ?? "NULL"}, " +
                  $"hasTriggered={hasTriggered}");

        if (mySequence == null) return;
        if (hasTriggered) return;
        if (door.GetCurrentSequence() != mySequence) return;

        // Only use fallback if this sequence is meant to end the game
        if (mySequence.nextSequenceWithItem == null && mySequence.nextSequenceWithoutItem == null)
        {
            Debug.Log($"[LossHandler] No follow-up sequences, treating as terminal loss via fallback.");
            hasTriggered = true;
            door.ClaimSequenceEnd();
            DisplayLossUI();
        }
    }

    private void DisplayLossUI()
    {
        if (lossCanvas == null)
        {
            Debug.LogError("[LossSequenceHandler] lossCanvas is not assigned!");
            return;
        }

        // Set the message text if override exists
        if (messageTextOverride != null)
        {
            messageTextOverride.text = "You Failed!";
        }
        else
        {
            Text canvasText = lossCanvas.GetComponentInChildren<Text>();
            if (canvasText != null && string.IsNullOrEmpty(canvasText.text))
                canvasText.text = "You Failed!";
        }

        // Enable the canvas
        lossCanvas.SetActive(true);

        // Lock player and show cursor so they can click the button.
        LockPlayer(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[LossHandler] Loss UI enabled");
    }

    private void ReturnToMainMenu()
    {
        Debug.Log("[LossHandler] Returning to main menu...");

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

        // Also disable cast/consume components so no abilities fire on the loss screen.
        var player = pc != null ? pc.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            foreach (var c in player.GetComponents<PlayerCast>()) if (c != null) c.enabled = !locked;
            foreach (var c in player.GetComponents<PlayerConsume>()) if (c != null) c.enabled = !locked;
        }
    }
}
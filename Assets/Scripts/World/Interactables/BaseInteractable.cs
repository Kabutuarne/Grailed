using UnityEngine;
using System.Collections;

/// <summary>
/// Base class for interactable objects that display CastUI and text via InteractableTextHUD.
/// Interaction counts down ONLY when Interact input is held. Releases cancel the interaction.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("Time to complete the interaction (in seconds)")]
    public float interactDuration = 1f;

    [Tooltip("Text to display via InteractableTextHUD")]
    public string interactionText = "Interact";

    [Header("Audio")]
    [Tooltip("Sound to play when interaction starts")]
    public AudioClip startSound;

    [Tooltip("Sound to play when interaction completes")]
    public AudioClip completeSound;

    [Tooltip("Optional AudioSource. Will auto-add if missing")]
    public AudioSource audioSource;

    private Coroutine interactionRoutine;
    private bool isInteracting;
    private PlayerInputActions inputActions;
    private float interactionProgress = 0f;

    protected virtual void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (inputActions == null)
            inputActions = new PlayerInputActions();
    }

    public virtual bool CanInteract(GameObject interactor)
    {
        return !isInteracting;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (interactionRoutine != null)
            StopCoroutine(interactionRoutine);

        interactionRoutine = StartCoroutine(InteractionRoutine(interactor));
    }

    private IEnumerator InteractionRoutine(GameObject interactor)
    {
        isInteracting = true;
        interactionProgress = 0f;

        // Enable input
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }

        // Show CastUI
        var castUI = FindFirstObjectByType<CastUI>();
        if (castUI != null)
        {
            castUI.Show(interactDuration, interactDuration);
        }

        // Play start sound
        PlaySound(startSound);

        // Hold-to-interact loop: only progress while input is held
        while (interactionProgress < interactDuration)
        {
            // Check if input is still held
            if (inputActions == null || !inputActions.Player.Interact.IsPressed())
            {
                // Input released - cancel interaction
                if (castUI != null)
                {
                    castUI.Complete();
                }
                isInteracting = false;
                interactionRoutine = null;
                yield break;
            }

            // Progress the interaction
            interactionProgress += Time.deltaTime;
            if (interactionProgress > interactDuration)
                interactionProgress = interactDuration;

            // Update CastUI with remaining time
            if (castUI != null)
            {
                castUI.UpdateRemaining(interactDuration, interactDuration - interactionProgress);
            }

            yield return null;
        }

        // Play complete sound
        PlaySound(completeSound);

        // Hide CastUI
        if (castUI != null)
        {
            castUI.Complete();
        }

        // Call the completion handler
        OnInteractComplete(interactor);

        isInteracting = false;
        interactionRoutine = null;
    }

    /// <summary>
    /// Override this to handle what happens when interaction completes
    /// </summary>
    protected abstract void OnInteractComplete(GameObject interactor);

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    protected virtual void OnValidate()
    {
        if (interactDuration < 0.1f)
            interactDuration = 0.1f;
    }
}

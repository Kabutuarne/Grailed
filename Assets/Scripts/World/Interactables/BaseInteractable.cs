using UnityEngine;
using System.Collections;

/// <summary>
/// Base class for interactable objects that display CastUI and text on EquippedItemTitleHUD.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("Time to complete the interaction (in seconds)")]
    public float interactDuration = 1f;

    [Tooltip("Text to display on EquippedItemTitleHUD")]
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

    protected virtual void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
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

        // Show CastUI
        var castUI = FindFirstObjectByType<CastUI>();
        if (castUI != null)
        {
            castUI.Show(interactDuration, interactDuration);
        }

        // Show interaction text on HUD
        UpdateHUDText(interactDuration);

        // Play start sound
        PlaySound(startSound);

        // Wait for duration
        float elapsed = 0f;
        while (elapsed < interactDuration)
        {
            elapsed += Time.deltaTime;
            if (castUI != null)
            {
                castUI.UpdateRemaining(interactDuration, interactDuration - elapsed);
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

    private void UpdateHUDText(float duration)
    {
        var playerUI = FindFirstObjectByType<PlayerUI>();
        if (playerUI != null)
        {
            // Update the EquippedItemTitleHUD to show this interaction text
            var hud = playerUI.GetComponent<EquippedItemTitleHUD>();
            if (hud != null)
            {
                hud.SetTitle(interactionText);
            }
        }
    }

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

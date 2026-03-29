using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Chest : MonoBehaviour, IInteractable
{
    [Header("Lid Rotation")]
    [Tooltip("The child transform that rotates (the lid).")]
    public Transform lid;

    [Tooltip("Local rotation offset applied when opened (commonly X axis, but you can set any).")]
    public Vector3 openEulerOffset = new Vector3(-75f, 0f, 0f);

    [Tooltip("Seconds to fully open.")]
    public float duration = 0.35f;

    [Header("Audio")]
    [Tooltip("Plays once immediately when you interact (click/handle sound).")]
    public AudioClip soundOnInteract;

    [Tooltip("Optional. If null, will auto-find/add one on this GameObject.")]
    public AudioSource audioSource;

    [Header("State (read-only)")]
    [SerializeField] private bool isOpen;

    private Quaternion closedLidLocalRot;
    private Quaternion openLidLocalRot;

    private bool isMoving;
    private Coroutine openRoutine;

    void Awake()
    {
        if (lid == null)
            Debug.LogError($"{nameof(Chest)} on '{name}' has no lid assigned.", this);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (lid != null)
        {
            closedLidLocalRot = lid.localRotation;
            openLidLocalRot = closedLidLocalRot * Quaternion.Euler(openEulerOffset);
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        return !isOpen && !isMoving && lid != null;
    }

    public void Interact(GameObject interactor)
    {
        Interact();
    }

    // Kept for backward compatibility with any existing direct calls.
    public void Interact()
    {
        if (!CanInteract(null))
            return;

        PlayOneShot(soundOnInteract);

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        isMoving = true;

        Quaternion startRot = lid.localRotation;
        Quaternion endRot = openLidLocalRot;

        float t = 0f;
        float dur = Mathf.Max(0.01f, duration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;

            float eased = EaseInOutCubic(Mathf.Clamp01(t));
            lid.localRotation = Quaternion.Slerp(startRot, endRot, eased);

            yield return null;
        }

        lid.localRotation = endRot;

        isOpen = true;
        isMoving = false;
        openRoutine = null;
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    static float EaseInOutCubic(float x)
    {
        return x < 0.5f
            ? 4f * x * x * x
            : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    void OnValidate()
    {
        if (lid != null)
            openLidLocalRot = lid.localRotation * Quaternion.Euler(openEulerOffset);
    }
}
using System.Collections;
using UnityEngine;

/// <summary>
/// Animated chest that rotates open with audio feedback.
/// </summary>
public class Chest : BaseInteractable
{
    [Header("Lid Animation")]
    [Tooltip("The child transform that rotates (the lid)")]
    public Transform lid;

    [Tooltip("Local rotation offset applied when opened (typically X axis rotation)")]
    public Vector3 openEulerOffset = new Vector3(-75f, 0f, 0f);

    [Header("Animation")]
    [Tooltip("Seconds to fully open")]
    public float animationDuration = 0.35f;

    private Quaternion closedLidRotation;
    private Quaternion openLidRotation;
    private bool isOpen;
    private Coroutine openRoutine;

    protected override void Awake()
    {
        base.Awake();

        if (lid == null)
            Debug.LogError($"Chest on '{name}' has no lid assigned!", this);

        if (lid != null)
        {
            closedLidRotation = lid.localRotation;
            openLidRotation = closedLidRotation * Quaternion.Euler(openEulerOffset);
        }

        // Set default interaction text
        interactionText = "Open Chest";
    }

    public override bool CanInteract(GameObject interactor)
    {
        return !isOpen && base.CanInteract(interactor) && lid != null;
    }

    protected override void OnInteractComplete(GameObject interactor)
    {
        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenChest());
    }

    private IEnumerator OpenChest()
    {
        if (lid == null)
            yield break;

        Quaternion startRot = lid.localRotation;
        Quaternion endRot = openLidRotation;
        float duration = Mathf.Max(0.01f, animationDuration);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = EaseInOutCubic(Mathf.Clamp01(t));
            lid.localRotation = Quaternion.Slerp(startRot, endRot, eased);
            yield return null;
        }

        lid.localRotation = endRot;
        isOpen = true;
        openRoutine = null;
    }

    private static float EaseInOutCubic(float x)
    {
        return x < 0.5f
            ? 4f * x * x * x
            : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (lid != null)
            openLidRotation = lid.localRotation * Quaternion.Euler(openEulerOffset);
    }
}
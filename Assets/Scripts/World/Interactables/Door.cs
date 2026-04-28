using System.Collections;
using UnityEngine;

/// <summary>
/// Animated door that rotates open/closed with audio feedback and physics pushing.
/// </summary>
public class Door : BaseInteractable
{
    [Header("Rotation")]
    [Tooltip("Degrees to rotate on local Y when opened")]
    public float openAngle = 90f;

    [Header("Animation")]
    [Tooltip("Seconds to fully open or close")]
    public float animationDuration = 0.45f;

    [Tooltip("Allow closing when interacting again")]
    public bool canToggle = true;

    [Header("Physics Push")]
    [Tooltip("How strongly the door pushes rigidbodies")]
    public float pushStrength = 2.5f;

    [Tooltip("Only these layers get pushed")]
    public LayerMask pushMask = ~0;

    private Collider doorCollider;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;
    private Coroutine doorRoutine;

    protected override void Awake()
    {
        base.Awake();
        doorCollider = GetComponent<Collider>();
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        // Set default interaction text
        interactionText = "Open Door";
    }

    protected override void OnInteractComplete(GameObject interactor)
    {
        bool targetOpen = canToggle ? !isOpen : true;

        if (doorRoutine != null)
            StopCoroutine(doorRoutine);

        doorRoutine = StartCoroutine(RotateDoor(targetOpen));
    }

    private IEnumerator RotateDoor(bool targetOpen)
    {
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = targetOpen ? openRotation : closedRotation;
        float duration = Mathf.Max(0.01f, animationDuration);

        // Play door move start sound
        if (startSound != null && audioSource != null)
            audioSource.PlayOneShot(startSound);

        float t = 0f;
        float prevYaw = transform.localEulerAngles.y;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = EaseInOutCubic(Mathf.Clamp01(t));
            transform.localRotation = Quaternion.Slerp(startRot, endRot, eased);

            float newYaw = transform.localEulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(prevYaw, newYaw);
            prevYaw = newYaw;

            PushOverlappingRigidbodies(deltaYaw);
            yield return null;
        }

        transform.localRotation = endRot;
        PushOverlappingRigidbodies(0f);

        isOpen = targetOpen;

        // Play door finish sound
        if (completeSound != null && audioSource != null)
            audioSource.PlayOneShot(completeSound);

        doorRoutine = null;
    }

    private void PushOverlappingRigidbodies(float deltaYawDegrees)
    {
        if (doorCollider == null || deltaYawDegrees == 0f)
            return;

        Bounds b = doorCollider.bounds;
        Collider[] hits = Physics.OverlapBox(
            b.center, b.extents, transform.rotation, pushMask, QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float omegaRadPerSec = (deltaYawDegrees / dt) * Mathf.Deg2Rad;
        Vector3 doorPos = transform.position;

        foreach (var c in hits)
        {
            if (c == null || c == doorCollider)
                continue;

            Rigidbody rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic)
                continue;

            Vector3 r = rb.worldCenterOfMass - doorPos;
            r.y = 0f;

            if (r.sqrMagnitude < 0.0001f)
                continue;

            Vector3 omega = Vector3.up * omegaRadPerSec;
            Vector3 tangential = Vector3.Cross(omega, r);

            if (tangential.sqrMagnitude < 0.000001f)
                tangential = r.normalized;

            Vector3 impulse = tangential.normalized * pushStrength;
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }
    }

    private static float EaseInOutCubic(float x)
    {
        return x < 0.5f
            ? 4f * x * x * x
            : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Bounds b = col.bounds;
        Gizmos.matrix = Matrix4x4.TRS(b.center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, b.extents * 2f);
    }
#endif
}
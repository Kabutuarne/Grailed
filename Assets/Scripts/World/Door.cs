using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Door : MonoBehaviour, IInteractable
{
    [Header("Rotation")]
    [Tooltip("Degrees to rotate on local Y when opened.")]
    public float openAngle = 90f;

    [Tooltip("Seconds to fully open or close.")]
    public float duration = 0.45f;

    [Tooltip("Allow closing when interacting again.")]
    public bool toggle = true;

    [Header("Audio (3 slots)")]
    [Tooltip("Plays immediately when you press Interact (handle/click).")]
    public AudioClip soundOnInteract;

    [Tooltip("Plays when the door starts moving (whoosh/creak).")]
    public AudioClip soundOnDoorMoveStart;

    [Tooltip("Plays when the door finishes moving (slam/thud).")]
    public AudioClip soundOnDoorFinished;

    [Tooltip("Optional. If null, will auto-find/add one on this GameObject.")]
    public AudioSource audioSource;

    [Header("Physics Push")]
    [Tooltip("How strongly the door pushes rigidbodies it overlaps while moving.")]
    public float pushStrength = 2.5f;

    [Tooltip("Only these layers get pushed. Use Everything if unsure.")]
    public LayerMask pushMask = ~0;

    private Collider doorCollider;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private bool isOpen;
    private bool isMoving;
    private Coroutine moveRoutine;

    void Awake()
    {
        doorCollider = GetComponent<Collider>();

        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public bool CanInteract(GameObject interactor)
    {
        if (isMoving)
            return false;

        if (!toggle && isOpen)
            return false;

        return true;
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

        bool targetOpen = toggle ? !isOpen : true;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(RotateDoor(targetOpen));
    }

    IEnumerator RotateDoor(bool targetOpen)
    {
        isMoving = true;

        PlayOneShot(soundOnDoorMoveStart);

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = targetOpen ? openRotation : closedRotation;

        float t = 0f;
        float dur = Mathf.Max(0.01f, duration);
        float prevYaw = transform.localEulerAngles.y;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;

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
        isMoving = false;
        moveRoutine = null;

        PlayOneShot(soundOnDoorFinished);
    }

    void PushOverlappingRigidbodies(float deltaYawDegrees)
    {
        if (doorCollider == null)
            return;

        Bounds b = doorCollider.bounds;

        Collider[] hits = Physics.OverlapBox(
            b.center,
            b.extents,
            transform.rotation,
            pushMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float omegaRadPerSec = (deltaYawDegrees / dt) * Mathf.Deg2Rad;

        Vector3 doorPos = transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
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
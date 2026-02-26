using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Door : MonoBehaviour
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

    // Called by PlayerInteractor (we ignore interactor; kept for compatibility)
    public void Interact()
    {
        if (isMoving) return;
        if (!toggle && isOpen) return;

        // Play "interact" sound immediately
        PlayOneShot(soundOnInteract);

        bool targetOpen = toggle ? !isOpen : true;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(RotateDoor(targetOpen));
    }

    IEnumerator RotateDoor(bool targetOpen)
    {
        isMoving = true;

        // Play "move start" sound once when movement begins
        PlayOneShot(soundOnDoorMoveStart);

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = targetOpen ? openRotation : closedRotation;

        float t = 0f;
        float dur = Mathf.Max(0.01f, duration);

        // Track yaw so we can estimate angular velocity and push bodies in the swing direction
        float prevYaw = transform.localEulerAngles.y;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;

            // slow -> fast mid -> slow end
            float eased = EaseInOutCubic(Mathf.Clamp01(t));

            transform.localRotation = Quaternion.Slerp(startRot, endRot, eased);

            float newYaw = transform.localEulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(prevYaw, newYaw); // degrees since last frame
            prevYaw = newYaw;

            PushOverlappingRigidbodies(deltaYaw);

            yield return null;
        }

        transform.localRotation = endRot;

        // One last push pass at the end (helps if something barely intersects)
        PushOverlappingRigidbodies(0f);

        isOpen = targetOpen;
        isMoving = false;
        moveRoutine = null;

        // Play "finished" sound (slam/thud)
        PlayOneShot(soundOnDoorFinished);
    }

    void PushOverlappingRigidbodies(float deltaYawDegrees)
    {
        if (doorCollider == null) return;

        Bounds b = doorCollider.bounds;

        // Conservative overlap volume: door collider AABB, queried as a box
        Collider[] hits = Physics.OverlapBox(
            b.center,
            b.extents,
            transform.rotation,
            pushMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float omegaRadPerSec = (deltaYawDegrees / dt) * Mathf.Deg2Rad; // angular velocity about Y

        Vector3 doorPos = transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;
            if (c == doorCollider) continue;

            Rigidbody rb = c.attachedRigidbody;
            if (rb == null) continue;
            if (rb.isKinematic) continue;

            // Vector from door to rigidbody (flattened to ground plane)
            Vector3 r = rb.worldCenterOfMass - doorPos;
            r.y = 0f;
            if (r.sqrMagnitude < 0.0001f) continue;

            // Tangential direction of a rotating door: v = omega x r
            Vector3 omega = Vector3.up * omegaRadPerSec;
            Vector3 tangential = Vector3.Cross(omega, r);

            // If we're at a frame with near-zero delta, still push outward a bit to prevent sticking
            if (tangential.sqrMagnitude < 0.000001f)
                tangential = r.normalized;

            // Strong, immediate nudge so bodies don't block the swing
            Vector3 impulse = tangential.normalized * pushStrength;
            rb.AddForce(impulse, ForceMode.VelocityChange);
        }
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    // Strong satisfying swing curve
    static float EaseInOutCubic(float x)
    {
        return x < 0.5f
            ? 4f * x * x * x
            : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

#if UNITY_EDITOR
    // Visualize the push overlap volume in editor while selected
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
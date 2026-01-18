using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [Header("Ragdoll Root")]
    public Transform ragdollRig; // drag the rig/root here

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 0.6f, -1.5f); // slight up and back
    public bool lookAtRagdoll = true;

    [Header("Activation Settings")]
    public bool setDefaultLayerOnActivate = true;

    bool isActive = false;

    void Update()
    {
        if (isActive)
        {
            var pc = GetComponent<PlayerController>();
            if (pc != null && pc.cameraPivot != null)
            {
                Vector3 center = GetApproxCenter(GetRig());
                Vector3 worldOffset = transform.TransformVector(cameraOffset);
                pc.cameraPivot.position = center + worldOffset;
                if (lookAtRagdoll)
                    pc.cameraPivot.rotation = Quaternion.LookRotation(center - pc.cameraPivot.position, Vector3.up);
            }
        }
    }

    Transform GetRig()
    {
        if (ragdollRig != null) return ragdollRig;
        // Try common child name "Rig"
        var t = transform.Find("Rig");
        if (t != null) return t;
        // Fallback to self
        return transform;
    }

    public void Activate()
    {
        // Choose rig or fallback to self
        Transform rig = GetRig();

        // Disable animator and character controller
        var animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Disable player controller to stop movement logic
        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            try { pc.enabled = false; } catch { }
        }

        // Enable physics on all rigidbodies in rig
        var rbs = rig.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // Ensure colliders are enabled and solid
        var cols = rig.GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        // Set rig and children to Default layer
        if (setDefaultLayerOnActivate)
            SetLayerRecursively(rig.gameObject, 0); // 0 = Default

        isActive = true;
    }

    // Restore control and remove ragdoll physics/colliders
    public void DeactivateAndCleanup()
    {
        Transform rig = GetRig();

        // Remove ragdoll physics
        var rbs = rig.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero; // Reset velocity
            rb.angularVelocity = Vector3.zero; // Reset angular velocity
        }

        var cols = rig.GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
        {
            // Disable colliders under rig so CharacterController drives collisions
            col.enabled = false;
        }

        // Re-enable animator and controller
        var animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = true;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            try { pc.enabled = true; } catch { }
            if (pc.cameraPivot != null)
            {
                // Reset camera pivot near head
                pc.cameraPivot.localPosition = pc.standingCamLocalPos;
                pc.cameraPivot.localRotation = Quaternion.identity;
            }
        }

        // Restore rig and children to PlayerBody layer (if defined)
        int playerBodyLayer = LayerMask.NameToLayer("PlayerBody");
        if (playerBodyLayer >= 0)
            SetLayerRecursively(rig.gameObject, playerBodyLayer);

        isActive = false;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    static Vector3 GetApproxCenter(Transform root)
    {
        var cols = root.GetComponentsInChildren<Collider>(true);
        if (cols.Length == 0) return root.position;
        Bounds b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        return b.center;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

// Component that enables preview and placement of DecorationItem when held in the right hand.
// Usage: attach to player GameObject (same object that has PlayerInventory). Assign `cam` or leave empty to use Camera.main.
public class PlayerDecorationPlacer : MonoBehaviour
{
    public Camera cam;
    public float placeRange = 5f;
    public LayerMask placeMask = ~0;
    public float rotationSpeed = 360f; // degrees per scroll unit

    private PlayerInputActions input;
    private PlayerInventory inventory;
    private GameObject preview;
    private DecorationItem currentDecoration;
    private float yRotation = 0f;

    void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        inventory = GetComponent<PlayerInventory>();
        if (cam == null) cam = Camera.main;
    }

    void OnEnable()
    {
        if (input != null) input.Player.Enable();
    }

    void OnDisable()
    {
        if (input != null) input.Player.Disable();
        DestroyPreview();
    }

    void Update()
    {
        if (inventory == null) return;

        GameObject held = inventory.rightHandItem;
        DecorationItem decor = null;
        if (held != null) decor = held.GetComponent<DecorationItem>();

        if (decor == null)
        {
            DestroyPreview();
            return;
        }

        // Ensure we have a preview for this decoration
        if (currentDecoration != decor || preview == null)
        {
            CreatePreview(decor);
        }

        // rotation with mouse scrollwheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            yRotation += scroll * rotationSpeed;
        }

        // position preview at raycast hit
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, placeRange, placeMask))
        {
            preview.SetActive(true);
            preview.transform.position = hit.point;
            // align upright and apply rotation around Y
            preview.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

            bool valid = !IsPreviewColliding(preview);
            UpdatePreviewVisual(valid);

            if (input.Player.Interact.WasPressedThisFrame())
            {
                if (valid)
                    PlaceHeldDecoration(preview.transform.position, preview.transform.rotation);
                else
                    Debug.Log("Cannot place decoration here (collision detected).");
            }
        }
        else
        {
            // hide preview when not pointing at a surface
            if (preview != null) preview.SetActive(false);
        }
    }

    void CreatePreview(DecorationItem decor)
    {
        DestroyPreview();
        currentDecoration = decor;
        if (decor.renderModel != null)
        {
            preview = Instantiate(decor.renderModel, transform);
            // Make preview copies look transparent and non-interactive
            foreach (var r in preview.GetComponentsInChildren<Renderer>())
            {
                var mat = new Material(Shader.Find("Standard"));
                Color c = Color.white; c.a = 0.5f;
                mat.color = c;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 0f);
                r.material = mat;
            }

            // disable colliders on the preview to avoid interfering with physics queries
            foreach (var col in preview.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }
        else
        {
            preview = new GameObject("DecorationPreview");
            preview.transform.SetParent(transform);
        }
        preview.SetActive(false);
    }

    void DestroyPreview()
    {
        if (preview != null)
        {
            Destroy(preview);
            preview = null;
        }
        currentDecoration = null;
    }

    bool IsPreviewColliding(GameObject pv)
    {
        // Build combined bounds from renderers
        var rends = pv.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return false;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        // Slightly shrink bounds to be more forgiving
        Vector3 ext = b.extents * 0.9f;
        Collider[] overlaps = Physics.OverlapBox(b.center, ext, pv.transform.rotation, ~0, QueryTriggerInteraction.Ignore);

        foreach (var col in overlaps)
        {
            if (col.transform.IsChildOf(pv.transform)) continue;
            // If collider belongs to an object tagged "Floor" we allow overlap.
            if (col.gameObject.CompareTag("Floor")) continue;
            return true;
        }

        return false;
    }

    void UpdatePreviewVisual(bool valid)
    {
        if (preview == null) return;
        Color emission = valid ? Color.white * 0f : Color.red * 1.2f;
        foreach (var r in preview.GetComponentsInChildren<Renderer>())
        {
            var m = r.material;
            m.SetColor("_EmissionColor", emission);
        }
    }

    void PlaceHeldDecoration(Vector3 pos, Quaternion rot)
    {
        if (inventory == null) return;
        var held = inventory.rightHandItem;
        if (held == null) return;

        // detach and position the actual held object into the world
        held.transform.SetParent(null, true);
        held.transform.position = pos;
        held.transform.rotation = rot;

        // enable colliders
        foreach (var col in held.GetComponentsInChildren<Collider>())
            col.enabled = true;

        var rb = held.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // make static after placing
            rb.detectCollisions = true;
        }

        // clear right hand reference and notify inventory UI by calling EquipRight(null) after clearing
        inventory.rightHandItem = null;
        // EquipRight(null) will simply invoke OnInventoryChanged when rightHandItem was already cleared
        inventory.EquipRight(null);

        DestroyPreview();
    }

}

using UnityEngine;
using UnityEngine.Rendering.Universal; // Required for URP settings

public class InventoryPreviewRenderer : MonoBehaviour
{
    public static InventoryPreviewRenderer Instance { get; private set; }

    [Header("Render Settings")]
    public int textureSize = 256;
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Slightly grey to see if it's working
    public float padding = 1.2f;

    private Camera cam;
    private Transform previewRoot;
    private Light previewLight;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        previewRoot = new GameObject("PreviewRoot").transform;
        previewRoot.position = new Vector3(9999, 9999, 9999);

        // Setup Camera
        GameObject camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(previewRoot);
        cam = camGO.AddComponent<Camera>();

        // URP Specific Setup
        var additionalCameraData = cam.GetUniversalAdditionalCameraData();
        additionalCameraData.renderType = CameraRenderType.Base; // Must be Base to render to RT

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.enabled = false; // We trigger it manually
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        // Setup Light (Crucial: 3D models need light to be visible)
        GameObject lightGO = new GameObject("PreviewLight");
        lightGO.transform.SetParent(previewRoot);
        previewLight = lightGO.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    public RenderTexture RenderPreview(IInventoryPreviewProvider provider)
    {
        if (provider == null || provider.PreviewPrefab == null)
            return null;

        RenderTexture rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        rt.Create();

        GameObject inst = Instantiate(provider.PreviewPrefab, previewRoot);
        inst.transform.localPosition = Vector3.zero;

        // Apply Per-Item Tweaks
        inst.transform.localRotation = Quaternion.Euler(provider.PreviewRotation);
        inst.transform.localScale = Vector3.one * provider.PreviewScale;

        // Set to a layer the camera can see (Default)
        inst.layer = 0;
        foreach (var t in inst.GetComponentsInChildren<Transform>()) t.gameObject.layer = 0;

        foreach (var rb in inst.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        foreach (var col in inst.GetComponentsInChildren<Collider>()) col.enabled = false;

        Bounds b = CalculateBounds(inst);
        cam.transform.position = b.center + new Vector3(0, 0, -b.extents.magnitude * padding);
        cam.transform.LookAt(b.center);

        cam.targetTexture = rt;
        cam.Render();

        cam.targetTexture = null;
        DestroyImmediate(inst);

        return rt;
    }
    private Bounds CalculateBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
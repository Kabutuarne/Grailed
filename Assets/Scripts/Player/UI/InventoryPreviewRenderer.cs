using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class InventoryPreviewRenderer : MonoBehaviour
{
    public static InventoryPreviewRenderer Instance { get; private set; }

    [Header("Render Settings")]
    public int textureSize = 256;
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    public float padding = 1.2f;

    private Camera cam;
    private Transform previewRoot;
    private Light previewLight;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        DontDestroyOnLoad(gameObject);

        InitializePreviewSystem();
    }

    void OnEnable()
    {
        if (cam == null || previewRoot == null)
        {
            InitializePreviewSystem();
        }
    }

    private void InitializePreviewSystem()
    {
        if (previewRoot != null)
        {
            DestroyImmediate(previewRoot.gameObject);
        }

        previewRoot = new GameObject("PreviewRoot").transform;
        previewRoot.SetParent(transform, false);
        previewRoot.position = new Vector3(9999, 9999, 9999);

        int previewLayer = LayerMask.NameToLayer("PreviewOnly");
        if (previewLayer == -1) previewLayer = 0;

        GameObject camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(previewRoot, false);
        cam = camGO.AddComponent<Camera>();

        var additionalCameraData = cam.GetUniversalAdditionalCameraData();
        additionalCameraData.renderType = CameraRenderType.Base;
        additionalCameraData.renderPostProcessing = false;
        additionalCameraData.requiresColorOption = CameraOverrideOption.Off;
        additionalCameraData.requiresDepthOption = CameraOverrideOption.Off;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.enabled = false;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;

        if (previewLayer != 0)
            cam.cullingMask = 1 << previewLayer;

        GameObject lightGO = new GameObject("PreviewLight");
        lightGO.transform.SetParent(previewRoot);
        previewLight = lightGO.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        if (previewLayer != 0)
        {
            lightGO.layer = previewLayer;
            previewLight.cullingMask = 1 << previewLayer;
        }

        GameObject fillLightGO = new GameObject("PreviewFillLight");
        fillLightGO.transform.SetParent(previewRoot);
        Light fillLight = fillLightGO.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.4f;
        fillLightGO.transform.rotation = Quaternion.Euler(-30, 150, 0);

        if (previewLayer != 0)
        {
            fillLightGO.layer = previewLayer;
            fillLight.cullingMask = 1 << previewLayer;
        }
    }

    public RenderTexture RenderPreview(IInventoryPreviewProvider provider)
    {
        if (provider == null || provider.PreviewPrefab == null)
            return null;

        if (cam == null || previewRoot == null)
            InitializePreviewSystem();

        if (cam == null)
        {
            Debug.LogWarning("InventoryPreviewRenderer: Failed to initialize preview camera");
            return null;
        }

        // Snapshot global render state
        AmbientMode prevAmbientMode = RenderSettings.ambientMode;
        Color prevAmbientLight = RenderSettings.ambientLight;
        bool prevFog = RenderSettings.fog;

        // Override to isolated, consistent state
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f);
        RenderSettings.fog = false;

        RenderTexture rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.Create();

        GameObject inst = Instantiate(provider.PreviewPrefab, previewRoot);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.Euler(provider.PreviewRotation);
        inst.transform.localScale = Vector3.one * provider.PreviewScale;

        int previewLayer = LayerMask.NameToLayer("PreviewOnly");
        if (previewLayer == -1) previewLayer = 0;

        inst.layer = previewLayer;
        foreach (var t in inst.GetComponentsInChildren<Transform>())
            t.gameObject.layer = previewLayer;

        foreach (var rb in inst.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        foreach (var col in inst.GetComponentsInChildren<Collider>()) col.enabled = false;

        Bounds b = CalculateBounds(inst);
        cam.transform.position = b.center + new Vector3(0, 0, -b.extents.magnitude * padding);
        cam.transform.LookAt(b.center);

        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;

        // Restore global render state
        RenderSettings.ambientMode = prevAmbientMode;
        RenderSettings.ambientLight = prevAmbientLight;
        RenderSettings.fog = prevFog;

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
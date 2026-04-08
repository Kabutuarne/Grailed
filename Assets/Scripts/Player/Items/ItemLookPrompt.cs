using UnityEngine;
using TMPro;

public class ItemLookPrompt : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float lookDotThreshold = 0.85f;
    [SerializeField] private Transform lookTarget;

    [Header("Visuals")]
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Text")]
    [SerializeField] private float textScale = 0.05f;
    [SerializeField] private bool billboardToCamera = true;

    private Camera mainCam;
    private GameObject textInstance;
    private TextMeshPro textMesh;

    private void Start()
    {
        mainCam = Camera.main;

        if (lookTarget == null)
            lookTarget = transform;

        CreateWorldText();
        SetVisualState(false);
    }

    private void Update()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null)
                return;
        }

        bool inRange = Vector3.Distance(mainCam.transform.position, transform.position) <= pickupRange;
        bool beingLookedAt = false;

        if (inRange)
        {
            Vector3 dirToItem = (lookTarget.position - mainCam.transform.position).normalized;
            float dot = Vector3.Dot(mainCam.transform.forward, dirToItem);
            beingLookedAt = dot >= lookDotThreshold;
        }

        bool shouldShow = inRange && beingLookedAt;

        SetVisualState(shouldShow);

        if (textInstance != null)
        {
            textInstance.transform.position = transform.position + textOffset;

            if (billboardToCamera && mainCam != null)
                textInstance.transform.forward = textInstance.transform.position - mainCam.transform.position;
        }
    }

    private void SetVisualState(bool active)
    {
        if (textInstance != null)
            textInstance.SetActive(active);
    }

    private void CreateWorldText()
    {
        textInstance = new GameObject("ItemPromptText");
        textInstance.transform.SetParent(null);
        textInstance.transform.position = transform.position + textOffset;
        textInstance.transform.localScale = Vector3.one * textScale;

        textMesh = textInstance.AddComponent<TextMeshPro>();
        textMesh.text = GetItemTitle();
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = 8f;
        textMesh.color = Color.white;
        textMesh.outlineWidth = 0.2f;

        textInstance.SetActive(false);
    }

    private string GetItemTitle()
    {
        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IItemDisplayName named &&
                !string.IsNullOrWhiteSpace(named.DisplayName))
            {
                return named.DisplayName;
            }
        }

        ItemPickup pickup = GetComponent<ItemPickup>();
        if (pickup == null)
            pickup = GetComponentInParent<ItemPickup>();

        if (pickup != null && !string.IsNullOrWhiteSpace(pickup.DisplayName))
            return pickup.DisplayName;

        return "Item";
    }

    private void OnDestroy()
    {
        if (textInstance != null)
            Destroy(textInstance);
    }
}
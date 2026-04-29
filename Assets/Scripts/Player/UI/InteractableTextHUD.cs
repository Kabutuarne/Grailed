using UnityEngine;
using TMPro;

public class InteractableTextHUD : MonoBehaviour
{
    [Header("Display Settings")]
    public TMP_Text targetText;
    public float raycastDistance = 100f;
    public LayerMask interactableLayer = -1;

    [Header("Timing")]
    public float fadeSeconds = 0.5f;

    private Camera mainCamera;
    private CanvasGroup canvasGroup;
    private IInteractable currentLookedAtInteractable;
    private float fadeElapsed = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var ui = FindFirstObjectByType<PlayerUI>();
        if (ui == null) return;

        var hud = ui.GetComponent<InteractableTextHUD>();
        if (hud == null) hud = ui.gameObject.AddComponent<InteractableTextHUD>();
        hud.Initialize();
    }

    public void Initialize()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();

        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>();

        HideImmediate();
    }

    private void Update()
    {
        if (mainCamera == null || targetText == null) return;

        IInteractable lookedAt = GetLookedAtInteractable();

        if (lookedAt != currentLookedAtInteractable)
        {
            currentLookedAtInteractable = lookedAt;

            if (lookedAt != null)
            {
                // Looking at something — show immediately, stop any fade
                targetText.text = ((BaseInteractable)lookedAt).interactionText;
                SetAlpha(1f);
                fadeElapsed = -1f;
            }
            else
            {
                // Looked away — start fade
                fadeElapsed = 0f;
            }
        }

        // fade when looked away
        if (fadeElapsed >= 0f)
        {
            fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeSeconds);
            SetAlpha(1f - t);

            if (t >= 1f)
                HideImmediate();
        }
    }

    private IInteractable GetLookedAtInteractable()
    {
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactableLayer))
            return hit.collider.GetComponent<IInteractable>();

        return null;
    }

    private void HideImmediate()
    {
        SetAlpha(0f);
        targetText.text = "";
        fadeElapsed = -1f;
        currentLookedAtInteractable = null;
    }

    private void SetAlpha(float a)
    {
        targetText.color = new Color(targetText.color.r, targetText.color.g, targetText.color.b, a);
    }
}
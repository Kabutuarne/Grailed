using UnityEngine;
using TMPro;

public class InteractableTextHUD : MonoBehaviour
{
    [Header("Display Settings")]
    public TMP_Text targetText;
    public LayerMask interactableLayer = -1;

    [Header("Interaction")]
    [Tooltip("Must match PlayerInteractor.interactRange")]
    public float interactRange = 3f;

    [Header("Timing")]
    public float fadeSeconds = 0.5f;

    private Camera mainCamera;
    private IInteractable currentLookedAtInteractable;
    private float fadeElapsed = -1f;
    private bool overrideTextActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var ui = FindFirstObjectByType<PlayerUI>();
        if (ui == null) return;

        var hud = ui.GetComponent<InteractableTextHUD>();
        if (hud == null)
            hud = ui.gameObject.AddComponent<InteractableTextHUD>();

        hud.Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        HideImmediate();
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
        if (overrideTextActive || mainCamera == null || targetText == null)
            return;

        IInteractable lookedAt = GetLookedAtInteractable();

        if (lookedAt != currentLookedAtInteractable)
        {
            currentLookedAtInteractable = lookedAt;

            if (lookedAt != null)
            {
                if (lookedAt is BaseInteractable baseInteractable)
                    targetText.text = baseInteractable.interactionText;
                else
                    targetText.text = "Interact";

                SetAlpha(1f);
                fadeElapsed = -1f;
            }
            else
            {
                // Immediately hide when out of range
                HideImmediate();
            }
        }
    }

    private IInteractable GetLookedAtInteractable()
    {
        Ray ray = mainCamera.ScreenPointToRay(
            new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));

        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, interactableLayer))
            return null;

        return hit.collider.GetComponentInParent<IInteractable>();
    }

    public void ShowCustomText(string text)
    {
        if (targetText == null)
            Initialize();

        if (targetText == null)
            return;

        targetText.text = text;
        SetAlpha(1f);
        fadeElapsed = -1f;
        overrideTextActive = true;
        currentLookedAtInteractable = null;
    }

    public void HideCustomText()
    {
        if (!overrideTextActive)
            return;

        HideImmediate();
    }

    private void HideImmediate()
    {
        if (targetText == null)
            return;

        SetAlpha(0f);
        targetText.text = "";
        fadeElapsed = -1f;
        currentLookedAtInteractable = null;
        overrideTextActive = false;
    }

    private void SetAlpha(float alpha)
    {
        Color c = targetText.color;
        c.a = alpha;
        targetText.color = c;
    }
}
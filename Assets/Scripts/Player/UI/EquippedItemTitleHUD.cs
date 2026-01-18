using UnityEngine;
using UnityEngine.UI;

// Displays the title of the currently equipped (right-hand) item
// when the player closes the backpack, holds it for a few seconds,
// then fades it away. Uses an existing Text component rather than
// creating a new one.
public class EquippedItemTitleHUD : MonoBehaviour
{
    [Header("Timing")]
    public float showSeconds = 3f;
    public float fadeSeconds = 1.5f;

    private PlayerUI playerUI;
    private CanvasGroup canvasGroup;
    [Header("Target Text")]
    public Text targetText; // Assign an existing Text via Inspector (preferred)
    public string targetTextName = "EquippedItemTitle"; // Fallback: find by name under hudRoot

    private bool prevBackpackOpen;
    private float elapsed = -1f;
    private GameObject prevRightHandItem;

#if UNITY_2023_1_OR_NEWER
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var ui = Object.FindFirstObjectByType<PlayerUI>();
        if (ui == null) return;
        var hud = ui.GetComponent<EquippedItemTitleHUD>();
        if (hud == null) hud = ui.gameObject.AddComponent<EquippedItemTitleHUD>();
        hud.Initialize(ui);
    }
#else
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var ui = Object.FindObjectOfType<PlayerUI>();
        if (ui == null) return;
        var hud = ui.GetComponent<EquippedItemTitleHUD>();
        if (hud == null) hud = ui.gameObject.AddComponent<EquippedItemTitleHUD>();
        hud.Initialize(ui);
    }
#endif

    public void Initialize(PlayerUI ui)
    {
        playerUI = ui;
        prevBackpackOpen = ui != null && ui.IsBackpackOpen;
        FindTargetTextIfNeeded();
        SubscribeInventoryEvents();
        prevRightHandItem = (playerUI != null && playerUI.inventory != null) ? playerUI.inventory.rightHandItem : null;
        HideImmediate();
    }

    private void Update()
    {
        if (playerUI == null)
        {
#if UNITY_2023_1_OR_NEWER
            var ui = Object.FindFirstObjectByType<PlayerUI>();
#else
            var ui = Object.FindObjectOfType<PlayerUI>();
#endif
            if (ui != null) Initialize(ui); else return;
        }

        bool nowOpen = playerUI.IsBackpackOpen;
        if (prevBackpackOpen && !nowOpen)
        {
            ShowCurrentEquippedTitle();
        }
        prevBackpackOpen = nowOpen;

        // If backpack is closed and equipped item changed, show title
        var currentRightHand = (playerUI.inventory != null) ? playerUI.inventory.rightHandItem : null;
        if (!playerUI.IsBackpackOpen && currentRightHand != prevRightHandItem)
        {
            prevRightHandItem = currentRightHand;
            if (currentRightHand != null)
            {
                ShowCurrentEquippedTitle();
            }
        }

        // Handle hold + fade
        if (elapsed >= 0f)
        {
            elapsed += Time.deltaTime;
            if (elapsed < showSeconds)
            {
                SetAlpha(1f);
            }
            else if (elapsed < showSeconds + fadeSeconds)
            {
                float t = Mathf.InverseLerp(showSeconds, showSeconds + fadeSeconds, elapsed);
                SetAlpha(1f - t);
            }
            else
            {
                HideImmediate();
            }
        }
    }

    private void ShowCurrentEquippedTitle()
    {
        if (playerUI == null || playerUI.inventory == null) return;
        var item = playerUI.inventory.rightHandItem;
        if (item == null) return;

        string title; Color color;
        GetItemTitleAndColor(item, out title, out color);
        if (string.IsNullOrEmpty(title)) return;

        FindTargetTextIfNeeded();
        if (targetText == null)
        {
            Debug.LogWarning("EquippedItemTitleHUD: No target Text found to display title.");
            return;
        }
        targetText.text = title;
        targetText.color = color;
        SetAlpha(1f);
        elapsed = 0f;
    }

    // Public trigger from external systems (e.g., WandItem on selection change)
    public void ShowEquippedTitle()
    {
        ShowCurrentEquippedTitle();
    }

    private void HideImmediate()
    {
        SetAlpha(0f);
        if (targetText != null) targetText.text = "";
        elapsed = -1f;
    }

    private void SetAlpha(float a)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = a;
        }
        else if (targetText != null)
        {
            var c = targetText.color; c.a = a; targetText.color = c;
        }
    }

    private void FindTargetTextIfNeeded()
    {
        if (targetText != null)
        {
            // Prefer a CanvasGroup on the same object or its parent to control fade
            canvasGroup = targetText.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = targetText.transform.GetComponentInParent<CanvasGroup>();
            return;
        }

        Transform searchRoot = (playerUI != null && playerUI.hudRoot != null)
            ? playerUI.hudRoot.transform
            : (playerUI != null ? playerUI.transform : transform);

        if (searchRoot == null) return;

        // Try find by name under the HUD root
        var t = searchRoot.Find(targetTextName);
        if (t != null)
        {
            targetText = t.GetComponent<Text>();
            if (targetText != null)
            {
                canvasGroup = targetText.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = targetText.transform.GetComponentInParent<CanvasGroup>();
                SetAlpha(0f);
            }
        }
    }

    private void SubscribeInventoryEvents()
    {
        if (playerUI != null && playerUI.inventory != null)
        {
            // Ensure we don't double-subscribe
            UnsubscribeInventoryEvents();
            playerUI.inventory.OnInventoryChanged += OnInventoryChanged;
        }
    }

    private void UnsubscribeInventoryEvents()
    {
        if (playerUI != null && playerUI.inventory != null)
        {
            playerUI.inventory.OnInventoryChanged -= OnInventoryChanged;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeInventoryEvents();
    }

    private void OnInventoryChanged()
    {
        if (playerUI == null || playerUI.inventory == null) return;
        var currentRight = playerUI.inventory.rightHandItem;
        bool changed = currentRight != prevRightHandItem;
        prevRightHandItem = currentRight;

        // Only trigger when backpack is closed and the equipped item changed
        if (!playerUI.IsBackpackOpen && changed && currentRight != null)
        {
            ShowCurrentEquippedTitle();
        }
    }

    private void GetItemTitleAndColor(GameObject item, out string title, out Color color)
    {
        title = null; color = Color.white;
        if (item == null) return;

        var scroll = item.GetComponent<ScrollItem>();
        if (scroll != null)
        {
            title = scroll.title;
            color = scroll.titleColor;
            return;
        }

        var wand = item.GetComponent<WandItem>();
        if (wand != null)
        {
            // Compose "WandTitle [SpellTitle]" if a selected scroll exists
            string wandTitle = wand.title;
            string spellTitle = null;
            var sel = wand.GetSelectedScroll();
            if (sel != null && !string.IsNullOrEmpty(sel.title)) spellTitle = sel.title;
            title = !string.IsNullOrEmpty(spellTitle) ? ($"{wandTitle} [{spellTitle}]") : wandTitle;
            color = wand.titleColor;
            return;
        }

        var cons = item.GetComponent<ConsumableItem>();
        if (cons != null)
        {
            title = cons.title;
            color = cons.titleColor;
            return;
        }

        // Fallback to object name
        title = item.name;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class EquippedItemTitleHUD : MonoBehaviour
{
    [Header("Timing")]
    public float showSeconds = 3f;
    public float fadeSeconds = 1.5f;

    private PlayerUI playerUI;
    private CanvasGroup canvasGroup;

    [Header("Target Text")]
    public Text targetText;
    public string targetTextName = "EquippedItemTitle";

    private bool prevBackpackOpen;
    private float elapsed = -1f;
    private GameObject prevRightHandItem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var ui = Object.FindFirstObjectByType<PlayerUI>();
        if (ui == null) return;

        var hud = ui.GetComponent<EquippedItemTitleHUD>();
        if (hud == null) hud = ui.gameObject.AddComponent<EquippedItemTitleHUD>();
        hud.Initialize(ui);
    }

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
            var ui = Object.FindFirstObjectByType<PlayerUI>();
            if (ui != null) Initialize(ui);
            else return;
        }

        bool nowOpen = playerUI.IsBackpackOpen;
        if (prevBackpackOpen && !nowOpen)
            ShowCurrentEquippedTitle();

        prevBackpackOpen = nowOpen;

        var currentRightHand = (playerUI.inventory != null) ? playerUI.inventory.rightHandItem : null;
        if (!playerUI.IsBackpackOpen && currentRightHand != prevRightHandItem)
        {
            prevRightHandItem = currentRightHand;
            if (currentRightHand != null)
                ShowCurrentEquippedTitle();
        }

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
        if (playerUI == null || playerUI.inventory == null)
            return;

        var item = playerUI.inventory.rightHandItem;
        if (item == null)
            return;

        string title = BuildEquippedTitle(item, out Color color);
        if (string.IsNullOrWhiteSpace(title))
            return;

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

    public void ShowEquippedTitle()
    {
        ShowCurrentEquippedTitle();
    }

    private string BuildEquippedTitle(GameObject item, out Color color)
    {
        color = Color.white;
        if (item == null)
            return null;

        WandItem wand = item.GetComponent<WandItem>();
        if (wand != null)
        {
            string wandTitle = wand.DisplayName;
            ScrollItem selectedScroll = wand.GetSelectedScroll();
            if (selectedScroll != null)
            {
                color = wand.titleColor;
                return $"{wandTitle} [{selectedScroll.DisplayName}]";
            }

            color = wand.titleColor;
            return wandTitle;
        }

        var pickup = item.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            color = pickup.TooltipTitleColor;
            return string.IsNullOrWhiteSpace(pickup.TooltipTitle)
                ? ItemTooltipDataUtility.GetDisplayName(item)
                : pickup.TooltipTitle;
        }

        return ItemTooltipDataUtility.GetDisplayName(item);
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
            var c = targetText.color;
            c.a = a;
            targetText.color = c;
        }
    }

    private void FindTargetTextIfNeeded()
    {
        if (targetText != null)
        {
            canvasGroup = targetText.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = targetText.transform.GetComponentInParent<CanvasGroup>();
            return;
        }

        Transform searchRoot = (playerUI != null && playerUI.hudRoot != null)
            ? playerUI.hudRoot.transform
            : (playerUI != null ? playerUI.transform : transform);

        if (searchRoot == null) return;

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
            UnsubscribeInventoryEvents();
            playerUI.inventory.OnInventoryChanged += OnInventoryChanged;
        }
    }

    private void UnsubscribeInventoryEvents()
    {
        if (playerUI != null && playerUI.inventory != null)
            playerUI.inventory.OnInventoryChanged -= OnInventoryChanged;
    }

    private void OnDestroy()
    {
        UnsubscribeInventoryEvents();
    }

    private void OnInventoryChanged()
    {
        if (playerUI == null || playerUI.inventory == null)
            return;

        var currentRight = playerUI.inventory.rightHandItem;
        bool changed = currentRight != prevRightHandItem;
        prevRightHandItem = currentRight;

        if (!playerUI.IsBackpackOpen && changed && currentRight != null)
            ShowCurrentEquippedTitle();
    }
}
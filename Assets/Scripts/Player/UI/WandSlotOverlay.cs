using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Controls the 3-frame overlay animation on a wand-internal slot.
public class WandSlotOverlay : MonoBehaviour
{
    public Image image;
    public Sprite frame1;
    public Sprite frame2;
    public Sprite frame3;
    public float frameDelay = 0.2f;

    private Coroutine animCoroutine;
    private bool isPermanent = false;
    private bool permanentRequested = false;

    void Awake()
    {
        if (image == null)
            image = GetComponent<Image>() ?? GetComponentInChildren<Image>(true);
        // start disabled
        gameObject.SetActive(false);
    }

    // PlayIn: start the entry animation. If `permanent` is true, the overlay will stay
    // visible after the animation until explicitly forced out (PlayOut(true)).
    public void PlayIn(bool permanent = false)
    {
        permanentRequested = permanent;
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        // If the object isn't active/enabled yet, activate and start the animation next frame to avoid
        // Unity complaint about starting coroutines on inactive game objects.
        if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
        {
            gameObject.SetActive(true);
            animCoroutine = StartCoroutine(AnimInDelayed());
        }
        else
        {
            gameObject.SetActive(true);
            animCoroutine = StartCoroutine(AnimIn());
        }
    }

    // PlayOut: play the reverse animation. If `force` is false and the overlay is currently
    // permanent (i.e. the slot contains an item), this call will be ignored. Use `force=true`
    // to override permanency (e.g. when starting to drag the item out of the slot).
    public void PlayOut(bool force = false)
    {
        if (isPermanent && !force)
            return;

        // If forcing out, clear permanency and immediately set to the initial frame and disable.
        if (force)
        {
            isPermanent = false;
            SetInitialFrameAndDisable();
            return;
        }

        // At the start of the reverse animation, remove the visible label and any selected visuals
        var slot = GetComponentInParent<InventorySlotUI>();
        if (slot != null)
        {
            if (slot.label != null)
                slot.label.gameObject.SetActive(false);

            // hide the per-slot selected indicator if present
            var sel = slot.transform.Find("SelectedIndicator");
            if (sel != null) sel.gameObject.SetActive(false);

            // hide any persistent selected marker
            var persistent = slot.transform.Find("PersistentSelectedMarker");
            if (persistent != null) persistent.gameObject.SetActive(false);
        }

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        // ensure active and start next frame if needed
        if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
        {
            gameObject.SetActive(true);
            animCoroutine = StartCoroutine(AnimOutDelayed());
        }
        else
        {
            gameObject.SetActive(true);
            animCoroutine = StartCoroutine(AnimOut());
        }
    }

    // Immediately set overlay to the final (third) frame and mark as permanent.
    public void SetFinalFrameAndPermanent()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = null;
        isPermanent = true;
        permanentRequested = false;
        if (image != null)
        {
            if (frame3 != null) image.sprite = frame3;
            else if (frame2 != null) image.sprite = frame2;
            else if (frame1 != null) image.sprite = frame1;
        }
        // show label if the slot has an item
        var slot = GetComponentInParent<InventorySlotUI>();
        if (slot != null)
        {
            if (slot.slotType == InventorySlotUI.SlotType.WandInternal && slot.wandOwner != null && slot.wandSlotIndex >= 0)
            {
                var item = slot.wandOwner.GetSlotItem(slot.wandSlotIndex);
                if (item != null && slot.label != null)
                {
                    slot.label.text = ItemTooltipDataUtility.GetDisplayName(item);
                    slot.label.gameObject.SetActive(true);
                }
            }
        }

        gameObject.SetActive(true);
    }

    // Immediately set the overlay to the initial (first) frame and disable it.
    public void SetInitialFrameAndDisable()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = null;
        isPermanent = false;
        permanentRequested = false;
        if (image != null)
        {
            if (frame1 != null) image.sprite = frame1;
            else if (frame2 != null) image.sprite = frame2;
            else if (frame3 != null) image.sprite = frame3;
        }
        var slot = GetComponentInParent<InventorySlotUI>();
        if (slot != null && slot.label != null)
            slot.label.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private IEnumerator AnimInDelayed()
    {
        yield return null;
        animCoroutine = StartCoroutine(AnimIn());
    }

    private IEnumerator AnimOutDelayed()
    {
        yield return null;
        animCoroutine = StartCoroutine(AnimOut());
    }

    private IEnumerator AnimIn()
    {
        if (image == null) yield break;
        if (frame1 != null) image.sprite = frame1;
        yield return new WaitForSeconds(frameDelay);
        if (frame2 != null) image.sprite = frame2;
        yield return new WaitForSeconds(frameDelay);
        if (frame3 != null) image.sprite = frame3;
        // After finishing the animation, decide whether the overlay should remain permanent.
        var slot = GetComponentInParent<InventorySlotUI>();
        bool hasItem = false;
        if (slot != null)
        {
            if (slot.slotType == InventorySlotUI.SlotType.WandInternal && slot.wandOwner != null && slot.wandSlotIndex >= 0)
            {
                var item = slot.wandOwner.GetSlotItem(slot.wandSlotIndex);
                hasItem = (item != null);
                if (hasItem && slot.label != null)
                {
                    slot.label.text = ItemTooltipDataUtility.GetDisplayName(item);
                    slot.label.gameObject.SetActive(true);
                }
            }
        }

        // If either the caller requested permanency or this slot currently contains an item,
        // mark the overlay as permanent so it won't be auto-closed by non-forced PlayOut calls.
        isPermanent = permanentRequested || hasItem;
        permanentRequested = false;

        animCoroutine = null;
    }

    private IEnumerator AnimOut()
    {
        if (image == null) yield break;
        if (frame3 != null) image.sprite = frame3;
        yield return new WaitForSeconds(frameDelay);
        if (frame2 != null) image.sprite = frame2;
        yield return new WaitForSeconds(frameDelay);
        if (frame1 != null) image.sprite = frame1;
        gameObject.SetActive(false);
        animCoroutine = null;
    }

    // Immediately stop animation and hide overlay
    public void ForceDisable()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = null;
        // Also hide any label associated with the parent slot
        var slot = GetComponentInParent<InventorySlotUI>();
        if (slot != null && slot.label != null)
            slot.label.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }
}

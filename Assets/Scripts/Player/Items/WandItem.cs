using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class WandItem : ItemPickup, IInventoryIconProvider, IInventoryPreviewProvider
{
    [Header("Presentation")]
    public GameObject renderModel;
    public Rigidbody rb;
    public Sprite inventoryIcon;
    [Header("UI Preview Tweaks")]
    public Vector3 previewRotation = new Vector3(0, 180, 0);
    public float previewScale = 1.0f;

    // Update/Add these interface implementations
    public GameObject PreviewPrefab => renderModel;
    public Vector3 PreviewRotation => previewRotation;
    public float PreviewScale => previewScale;
    [Header("Tooltip")]

    [Header("Wand Slots")]
    [Tooltip("Number of internal spell slots on the wand.")]
    public int slotCount = 4;

    [Tooltip("Initial spell items placed in the wand.")]
    public GameObject[] initialSpells;

    private GameObject[] spellSlots;
    private int selectedIndex = -1;
    private bool wasEquipped;
    private int filledCount;
    private PlayerInventory invCache;

    public Sprite InventoryIcon => inventoryIcon;

    // Use ItemPickup's title/description implementations

    void Awake()
    {
        int count = Mathf.Max(1, slotCount);
        spellSlots = new GameObject[count];

        if (initialSpells != null)
        {
            for (int i = 0; i < Mathf.Min(initialSpells.Length, count); i++)
            {
                if (initialSpells[i] == null)
                    continue;

                ScrollItem scroll = initialSpells[i].GetComponent<ScrollItem>();
                if (scroll != null)
                    SetSlotItem(i, initialSpells[i]);
            }
        }

        filledCount = 0;
        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] != null)
                filledCount++;
        }
    }

    public int SlotCount => spellSlots != null ? spellSlots.Length : 0;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (spellSlots == null || spellSlots.Length == 0)
            {
                selectedIndex = -1;
                return;
            }

            selectedIndex = Mathf.Clamp(value, -1, spellSlots.Length - 1);
        }
    }

    public GameObject GetSlotItem(int index)
    {
        if (spellSlots == null) return null;
        if (index < 0 || index >= spellSlots.Length) return null;
        return spellSlots[index];
    }

    public bool SetSlotItem(int index, GameObject item)
    {
        if (spellSlots == null) return false;
        if (index < 0 || index >= spellSlots.Length) return false;

        if (item == null)
        {
            if (spellSlots[index] != null)
            {
                spellSlots[index] = null;
                filledCount = Mathf.Max(0, filledCount - 1);
            }

            if (index == selectedIndex)
                selectedIndex = FindNextFilledSlotIndexStartingFrom(index);

            return true;
        }

        ScrollItem scroll = item.GetComponent<ScrollItem>();
        if (scroll == null)
            return false;

        bool wasEmpty = (filledCount == 0);

        item.transform.SetParent(transform, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.SetActive(false);

        Rigidbody rbItem = item.GetComponent<Rigidbody>();
        if (rbItem != null)
        {
            rbItem.isKinematic = true;
            rbItem.detectCollisions = false;
        }

        foreach (Collider col in item.GetComponentsInChildren<Collider>())
            col.enabled = false;

        bool wasNull = spellSlots[index] == null;
        spellSlots[index] = item;

        if (wasNull)
            filledCount++;

        if (wasEmpty && IsEquipped)
        {
            selectedIndex = index;
            NotifyEquippedTitle();
        }

        return true;
    }

    public GameObject RemoveSlotItem(int index)
    {
        if (spellSlots == null) return null;
        if (index < 0 || index >= spellSlots.Length) return null;

        GameObject item = spellSlots[index];
        spellSlots[index] = null;

        if (item == null)
            return null;

        item.transform.SetParent(null, true);

        if (index == selectedIndex)
            selectedIndex = FindNextFilledSlotIndexStartingFrom(index);

        filledCount = Mathf.Max(0, filledCount - 1);
        return item;
    }

    public void SwapInternal(int a, int b)
    {
        if (spellSlots == null) return;
        if (a < 0 || a >= spellSlots.Length || b < 0 || b >= spellSlots.Length) return;

        GameObject tmp = spellSlots[a];
        spellSlots[a] = spellSlots[b];
        spellSlots[b] = tmp;

        if (selectedIndex >= 0 &&
            selectedIndex < spellSlots.Length &&
            spellSlots[selectedIndex] == null)
        {
            selectedIndex = FindNextFilledSlotIndexStartingFrom(selectedIndex);
        }
    }

    void Update()
    {
        bool equipped = IsEquipped;

        if (equipped && !wasEquipped)
        {
            if (selectedIndex == -1)
            {
                int first = FindFirstFilledSlotIndex();
                if (first != -1)
                {
                    selectedIndex = first;
                    NotifyEquippedTitle();
                }
            }
        }
        else if (!equipped && wasEquipped)
        {
            selectedIndex = -1;
        }

        wasEquipped = equipped;

        if (!equipped)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float dy = mouse.scroll.ReadValue().y;
        if (dy > 0.01f) SelectPrevious();
        else if (dy < -0.01f) SelectNext();
    }

    public void SelectNext()
    {
        if (spellSlots == null || spellSlots.Length == 0)
            return;

        int next = FindNextFilledSlotIndexStartingFrom(selectedIndex);
        if (next != -1)
            selectedIndex = next;

        NotifyEquippedTitle();
    }

    public void SelectPrevious()
    {
        if (spellSlots == null || spellSlots.Length == 0)
            return;

        int prev = FindPrevFilledSlotIndexStartingFrom(selectedIndex);
        if (prev != -1)
            selectedIndex = prev;

        NotifyEquippedTitle();
    }

    public ScrollItem GetSelectedScroll()
    {
        GameObject go = GetSlotItem(selectedIndex);
        if (go == null) return null;
        return go.GetComponent<ScrollItem>();
    }

    private int FindFirstFilledSlotIndex()
    {
        if (spellSlots == null || spellSlots.Length == 0)
            return -1;

        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] != null)
                return i;
        }

        return -1;
    }

    private int FindNextFilledSlotIndexStartingFrom(int startIndex)
    {
        if (spellSlots == null || spellSlots.Length == 0)
            return -1;

        int n = spellSlots.Length;

        for (int step = 1; step <= n; step++)
        {
            int idx = (startIndex + step) % n;
            if (spellSlots[idx] != null)
                return idx;
        }

        return -1;
    }

    private int FindPrevFilledSlotIndexStartingFrom(int startIndex)
    {
        if (spellSlots == null || spellSlots.Length == 0)
            return -1;

        int n = spellSlots.Length;

        for (int step = 1; step <= n; step++)
        {
            int idx = (startIndex - step + n) % n;
            if (spellSlots[idx] != null)
                return idx;
        }

        return -1;
    }

    private PlayerInventory GetInventory()
    {
        if (invCache != null)
            return invCache;

        invCache = GetComponentInParent<PlayerInventory>();
        return invCache;
    }

    private bool IsEquipped => (GetInventory() != null && invCache.rightHandItem == gameObject);

    private void NotifyEquippedTitle()
    {
        PlayerUI ui = GetComponentInParent<PlayerUI>();
        if (ui == null)
            ui = Object.FindFirstObjectByType<PlayerUI>();

        EquippedItemTitleHUD hud = ui != null ? ui.GetComponent<EquippedItemTitleHUD>() : null;
        if (hud != null)
            hud.ShowEquippedTitle();
    }
}
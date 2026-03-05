using UnityEngine;
using UnityEngine.InputSystem;

// Represents a Wand item that can store a fixed number of spell scrolls.
// Behaves similarly to other items (3D model + tooltip metadata) but manages internal spell slots.
public class WandItem : ItemPickup
{
    [Header("Presentation")]
    public GameObject renderModel;
    public Rigidbody rb;
    public Sprite inventoryIcon;

    [Header("Tooltip")]
    public string title;
    public Color titleColor = Color.white;
    [TextArea]
    public string description;
    public Color descriptionColor = Color.white;

    [Header("Wand Slots")]
    [Tooltip("Number of internal spell slots on the wand.")]
    public int slotCount = 4;

    [Tooltip("Initial spell items placed in the wand.")]
    public GameObject[] initialSpells;

    private GameObject[] spellSlots;
    private int selectedIndex = -1; // -1 means no selection when not equipped
    private bool wasEquipped = false;
    private int filledCount = 0; // number of non-null spell slots
    private PlayerInventory invCache;
    void Awake()
    {
        int count = Mathf.Max(1, slotCount);
        spellSlots = new GameObject[count];

        if (initialSpells != null)
        {
            for (int i = 0; i < Mathf.Min(initialSpells.Length, count); i++)
            {
                if (initialSpells[i] != null)
                {
                    // Only accept ScrollItem types as wand-contained spells
                    var scroll = initialSpells[i].GetComponent<ScrollItem>();
                    if (scroll != null)
                    {
                        SetSlotItem(i, initialSpells[i]);
                    }
                }
            }
        }

        // Compute filled count after initial setup
        filledCount = 0;
        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] != null) filledCount++;
        }

        // Do not preselect a slot here. Selection happens on equip.
    }

    public int SlotCount => spellSlots != null ? spellSlots.Length : 0;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (spellSlots == null || spellSlots.Length == 0) { selectedIndex = -1; return; }
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
            {
                // Selected slot emptied; advance to next available or clear
                selectedIndex = FindNextFilledSlotIndexStartingFrom(index);
            }
            return true;
        }

        // Only allow ScrollItems
        var scroll = item.GetComponent<ScrollItem>();
        if (scroll == null) return false;

        // Check if wand was empty before adding this item
        bool wasEmpty = (filledCount == 0);

        // Parent to wand, disable in-world behaviours
        item.transform.SetParent(transform, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.SetActive(false);

        var rbItem = item.GetComponent<Rigidbody>();
        if (rbItem != null)
        {
            rbItem.isKinematic = true;
            rbItem.detectCollisions = false;
        }
        foreach (var col in item.GetComponentsInChildren<Collider>())
            col.enabled = false;

        bool wasNull = spellSlots[index] == null;
        spellSlots[index] = item;
        if (wasNull) filledCount++;

        // If wand just transitioned from empty->non-empty and is equipped, auto-select this slot
        if (wasEmpty)
        {
            if (IsEquipped)
            {
                selectedIndex = index;
                var sel = GetSelectedScroll();
                Debug.Log($"[WandItem] Auto-selected slot {selectedIndex}: '{(sel != null ? sel.title : "empty")}' on wand '{title}' after adding first spell while equipped");
                NotifyEquippedTitle();
            }
        }
        return true;
    }

    public GameObject RemoveSlotItem(int index)
    {
        if (spellSlots == null) return null;
        if (index < 0 || index >= spellSlots.Length) return null;

        var item = spellSlots[index];
        spellSlots[index] = null;
        if (item == null) return null;

        item.transform.SetParent(null, true);
        // Keep inactive until placed elsewhere by caller

        if (index == selectedIndex)
        {
            // Selected slot removed; advance selection
            selectedIndex = FindNextFilledSlotIndexStartingFrom(index);
        }

        filledCount = Mathf.Max(0, filledCount - 1);

        return item;
    }

    public void SwapInternal(int a, int b)
    {
        if (spellSlots == null) return;
        if (a < 0 || a >= spellSlots.Length || b < 0 || b >= spellSlots.Length) return;
        var tmp = spellSlots[a];
        spellSlots[a] = spellSlots[b];
        spellSlots[b] = tmp;

        // If the currently selected slot became empty after the swap, advance selection
        if (selectedIndex >= 0 && selectedIndex < spellSlots.Length && spellSlots[selectedIndex] == null)
        {
            selectedIndex = FindNextFilledSlotIndexStartingFrom(selectedIndex);
        }
    }

    // Wand does not subscribe to cast input; PlayerCast handles casting

    // No input subscriptions to disable

    void Update()
    {
        bool equipped = IsEquipped;

        // Handle equip/unequip transitions
        if (equipped && !wasEquipped)
        {
            // Auto-select first available spell when the wand becomes equipped
            if (selectedIndex == -1)
            {
                int first = FindFirstFilledSlotIndex();
                if (first != -1)
                {
                    selectedIndex = first;
                    var sel = GetSelectedScroll();
                    Debug.Log($"[WandItem] Equipped: auto-select slot {selectedIndex}: '{(sel != null ? sel.title : "empty")}' on wand '{title}'");
                    NotifyEquippedTitle();
                }
            }
        }
        else if (!equipped && wasEquipped)
        {
            // Clear selection when wand is no longer equipped
            selectedIndex = -1;
        }
        wasEquipped = equipped;

        if (equipped)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float dy = mouse.scroll.ReadValue().y;
                if (dy > 0.01f) SelectPrevious();
                else if (dy < -0.01f) SelectNext();
            }
        }
    }

    // Removed SpellScroll action: we poll mouse scroll in Update

    // Casting is driven by PlayerCast using the selected scroll

    public void SelectNext()
    {
        if (spellSlots == null || spellSlots.Length == 0) return;
        int next = FindNextFilledSlotIndexStartingFrom(selectedIndex);
        if (next != -1) selectedIndex = next;
        var sel = GetSelectedScroll();
        Debug.Log($"[WandItem] Switched to slot {selectedIndex}: '{(sel != null ? sel.title : "empty")}' on wand '{title}'");
        NotifyEquippedTitle();
    }

    public void SelectPrevious()
    {
        if (spellSlots == null || spellSlots.Length == 0) return;
        int prev = FindPrevFilledSlotIndexStartingFrom(selectedIndex);
        if (prev != -1) selectedIndex = prev;
        var sel = GetSelectedScroll();
        Debug.Log($"[WandItem] Switched to slot {selectedIndex}: '{(sel != null ? sel.title : "empty")}' on wand '{title}'");
        NotifyEquippedTitle();
    }

    public ScrollItem GetSelectedScroll()
    {
        var go = GetSlotItem(selectedIndex);
        if (go == null) return null;
        return go.GetComponent<ScrollItem>();
    }

    private int FindFirstFilledSlotIndex()
    {
        if (spellSlots == null || spellSlots.Length == 0) return -1;
        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] != null) return i;
        }
        return -1;
    }

    private int FindNextFilledSlotIndexStartingFrom(int startIndex)
    {
        if (spellSlots == null || spellSlots.Length == 0) return -1;
        int n = spellSlots.Length;
        for (int step = 1; step <= n; step++)
        {
            int idx = (startIndex + step) % n;
            if (spellSlots[idx] != null) return idx;
        }
        return -1; // none available
    }

    private int FindPrevFilledSlotIndexStartingFrom(int startIndex)
    {
        if (spellSlots == null || spellSlots.Length == 0) return -1;
        int n = spellSlots.Length;
        for (int step = 1; step <= n; step++)
        {
            int idx = (startIndex - step + n) % n;
            if (spellSlots[idx] != null) return idx;
        }
        return -1;
    }

    private PlayerInventory GetInventory()
    {
        if (invCache != null) return invCache;
        invCache = GetComponentInParent<PlayerInventory>();
        return invCache;
    }

    private bool IsEquipped => (GetInventory() != null && invCache.rightHandItem == gameObject);

    private void NotifyEquippedTitle()
    {
        var ui = GetComponentInParent<PlayerUI>();
        if (ui == null)
        {
#if UNITY_2023_1_OR_NEWER
            ui = Object.FindFirstObjectByType<PlayerUI>();
#else
            ui = Object.FindObjectOfType<PlayerUI>();
#endif
        }
        var hud = ui != null ? ui.GetComponent<EquippedItemTitleHUD>() : null;
        if (hud != null)
        {
            hud.ShowEquippedTitle();
        }
    }
}

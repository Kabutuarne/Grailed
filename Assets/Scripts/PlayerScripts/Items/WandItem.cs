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
    private int selectedIndex = 0;

    // Casting is handled by PlayerCast; wand only manages selection and title
    // private InputAction castAction;       // fallback local action
    // private InputAction assetCast;        // action from PlayerInputActions asset
    // private UnityEngine.InputSystem.PlayerInput playerInput;

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

        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] != null) { selectedIndex = i; break; }
        }
    }

    public int SlotCount => spellSlots != null ? spellSlots.Length : 0;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (spellSlots == null || spellSlots.Length == 0) { selectedIndex = 0; return; }
            selectedIndex = Mathf.Clamp(value, 0, spellSlots.Length - 1);
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
        if (item == null) { spellSlots[index] = null; return true; }

        // Only allow ScrollItems
        var scroll = item.GetComponent<ScrollItem>();
        if (scroll == null) return false;

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

        spellSlots[index] = item;
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
        return item;
    }

    public void SwapInternal(int a, int b)
    {
        if (spellSlots == null) return;
        if (a < 0 || a >= spellSlots.Length || b < 0 || b >= spellSlots.Length) return;
        var tmp = spellSlots[a];
        spellSlots[a] = spellSlots[b];
        spellSlots[b] = tmp;
    }

    // Wand does not subscribe to cast input; PlayerCast handles casting

    // No input subscriptions to disable

    void Update()
    {
        var inv = GetComponentInParent<PlayerInventory>();
        bool equipped = inv != null && inv.rightHandItem == gameObject;

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
        int start = selectedIndex;
        for (int i = 1; i <= spellSlots.Length; i++)
        {
            int idx = (start + i) % spellSlots.Length;
            if (spellSlots[idx] != null) { selectedIndex = idx; break; }
        }
        var sel = GetSelectedScroll();
        Debug.Log($"[WandItem] Switched to slot {selectedIndex}: '{(sel != null ? sel.title : "empty")}' on wand '{title}'");
        NotifyEquippedTitle();
    }

    public void SelectPrevious()
    {
        if (spellSlots == null || spellSlots.Length == 0) return;
        int start = selectedIndex;
        for (int i = 1; i <= spellSlots.Length; i++)
        {
            int idx = (start - i + spellSlots.Length) % spellSlots.Length;
            if (spellSlots[idx] != null) { selectedIndex = idx; break; }
        }
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

    public void CastSelected(GameObject user, Transform origin)
    {
        var scroll = GetSelectedScroll();
        if (scroll == null) return;
        scroll.Cast(user, origin);

        if (scroll.destroyOnCast)
        {
            RemoveSlotItem(selectedIndex);
        }
    }

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

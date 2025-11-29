using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    public PlayerStatusEffects statusEffects;
    public PlayerInventory inventory;

    [Header("HUD Root (Always-on HUD)")]
    // Assign a parent GameObject that contains the always-on HUD:
    // healthBarHUD, manaBarHUD, sprintBarHUD, etc.
    public GameObject hudRoot;

    [Header("HUD Bars (always-on HUD)")]
    public Slider healthBarHUD;
    public Slider manaBarHUD;
    public Slider sprintBarHUD;

    [Header("Backpack Bars")]
    public Slider healthBarBackpack;
    public Slider manaBarBackpack;
    // No stamina slider in backpack

    [Header("Status Effects UI (Backpack)")]
    public Transform statusEffectsRoot;     // parent transform inside backpack
    public GameObject statusEffectPrefab;   // prefab with a Text component

    [Header("Hand Slots UI (Backpack)")]
    public InventorySlotUI rightHandSlot;
    public InventorySlotUI leftHandSlot;

    [Header("Accessory Slots UI (Backpack)")]
    public InventorySlotUI[] accessorySlots = new InventorySlotUI[4];

    [Header("Backpack Inventory Slots (3x3)")]
    public InventorySlotUI[] backpackSlots;  // 9 slots, assign in Inspector

    [Header("Character Sheet Texts (Backpack)")]
    public Text healthText;       // shows: "current / max"
    public Text manaText;         // shows: "current / max"
    public Text staminaText;      // "Stamina: x / y"
    public Text intelligenceText; // "Intelligence: x"

    [Header("Backpack Root Panel")]
    public GameObject backpackRoot;      // panel you show/hide with Tab

    public bool IsBackpackOpen => backpackRoot != null && backpackRoot.activeSelf;

    void Start()
    {
        if (backpackRoot != null)
            backpackRoot.SetActive(false);

        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    void Update()
    {
        UpdateBars();
        UpdateStatusEffects();
        UpdateHands();
        UpdateBackpackSlots();
        UpdateAccessories();
        UpdateCharacterSheet();
    }

    void UpdateBars()
    {
        if (stats == null)
            return;

        float h = stats.Health01;
        float m = stats.Mana01;
        float s = stats.Stamina01;

        // HUD bars
        if (healthBarHUD != null)
            healthBarHUD.value = h;
        if (manaBarHUD != null)
            manaBarHUD.value = m;
        if (sprintBarHUD != null)
            sprintBarHUD.value = s;

        // Backpack bars (no stamina slider here)
        if (healthBarBackpack != null)
            healthBarBackpack.value = h;
        if (manaBarBackpack != null)
            manaBarBackpack.value = m;
    }

    void UpdateStatusEffects()
    {
        if (statusEffectsRoot == null || statusEffectPrefab == null || statusEffects == null)
            return;

        // Rebuild every frame, simple and dirty
        foreach (Transform child in statusEffectsRoot)
            Destroy(child.gameObject);

        foreach (var e in statusEffects.activeEffects)
        {
            GameObject go = Instantiate(statusEffectPrefab, statusEffectsRoot);
            Text t = go.GetComponentInChildren<Text>();
            if (t != null)
            {
                int seconds = Mathf.CeilToInt(e.timer);
                t.text = $"{e.id} ({seconds}s)";
            }
        }
    }

    void UpdateHands()
    {
        if (inventory == null)
            return;

        if (rightHandSlot != null)
            rightHandSlot.SetItem(inventory.rightHandItem);

        if (leftHandSlot != null)
            leftHandSlot.SetItem(inventory.leftHandItem);
    }

    void UpdateBackpackSlots()
    {
        if (inventory == null || backpackSlots == null)
            return;

        for (int i = 0; i < backpackSlots.Length; i++)
        {
            var slot = backpackSlots[i];
            if (slot == null)
                continue;

            GameObject item = null;
            if (inventory.backpack != null && i < inventory.backpack.Length)
                item = inventory.backpack[i];

            slot.SetItem(item);
        }
    }

    void UpdateAccessories()
    {
        if (inventory == null || accessorySlots == null || inventory.accessories == null)
            return;

        int len = Mathf.Min(accessorySlots.Length, inventory.accessories.Length);

        for (int i = 0; i < len; i++)
        {
            var slot = accessorySlots[i];
            if (slot == null)
                continue;

            GameObject item = inventory.accessories[i];
            slot.SetItem(item);
        }
    }

    void UpdateCharacterSheet()
    {
        if (stats == null)
            return;

        // Health & mana: plain "number / number"
        if (healthText != null)
            healthText.text = $"{Mathf.RoundToInt(stats.health)} / {Mathf.RoundToInt(stats.maxHealth)}";

        if (manaText != null)
            manaText.text = $"{Mathf.RoundToInt(stats.mana)} / {Mathf.RoundToInt(stats.maxMana)}";

        // Stamina & INT keep labels
        if (staminaText != null)
            staminaText.text = $"Stamina: {Mathf.RoundToInt(stats.stamina)} / {Mathf.RoundToInt(stats.maxStamina)}";

        if (intelligenceText != null)
            intelligenceText.text = $"Intelligence: {Mathf.RoundToInt(stats.intelligence)}";
    }

    void HandleInventoryChanged()
    {
        UpdateBackpackSlots();
        UpdateHands();
        UpdateAccessories();
    }

    public void ToggleBackpack()
    {
        if (backpackRoot == null)
            return;

        bool newState = !backpackRoot.activeSelf;
        backpackRoot.SetActive(newState);

        // Hide HUD when backpack is open, show HUD when backpack is closed
        if (hudRoot != null)
            hudRoot.SetActive(!newState);
    }
}

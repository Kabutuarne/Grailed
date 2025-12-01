using System;
using System.Collections.Generic;
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
    public Text staminaText;      // "Stamina: x / y" (resource)
    public Text intelligenceText; // "Intelligence: x" (attribute)
    public Text strengthText;     // "Strength: x" (attribute)
    public Text staminaAttrText;  // "Stamina: x" (attribute) - attribute vs resource name distinction
    public Text agilityText;      // "Agility: x" (attribute)

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

        // Group by carrier (EffectCarrier) where possible, show one entry per carrier using the longest remaining timer
        var entries = new Dictionary<object, float>(); // key: EffectCarrier or effect id string (for null carrier)

        foreach (var e in statusEffects.activeEffects)
        {
            if (e.carrier != null)
            {
                // Carrier key
                float current = entries.ContainsKey(e.carrier) ? entries[e.carrier] : 0f;
                if (e.duration < 0f)
                {
                    // toggle -> mark as -1 (ON)
                    entries[e.carrier] = -1f;
                }
                else
                {
                    float val = e.timer;
                    entries[e.carrier] = Mathf.Max(current, val);
                }
            }
            else
            {
                // no carrier - use id string as key and show each separately
                string key = e.id + "_" + System.Guid.NewGuid().ToString();
                entries[key] = e.duration < 0f ? -1f : e.timer;
            }
        }

        // Instantiate UI entries
        foreach (var kv in entries)
        {
            GameObject go = Instantiate(statusEffectPrefab, statusEffectsRoot);

            // set text (title and time-left)
            Text t = go.GetComponentInChildren<Text>();
            string titleText = "";
            float timeVal = kv.Value;

            if (kv.Key is EffectCarrier carrierKey)
            {
                titleText = carrierKey.title;
                if (timeVal < 0f)
                    titleText = $"{titleText} (ON)";
                else
                    titleText = $"{titleText} ({Mathf.CeilToInt(timeVal)}s)";

                // try set an Image if present
                var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
                if (img != null && carrierKey.icon != null)
                    img.sprite = carrierKey.icon;
            }
            else
            {
                // key is a string id mapping
                if (timeVal < 0f)
                    titleText = $"{kv.Key} (ON)";
                else
                    titleText = $"{kv.Key} ({Mathf.CeilToInt(timeVal)}s)";
            }

            if (t != null)
                t.text = titleText;
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

        // Resource labels
        if (staminaText != null)
            staminaText.text = $"Stamina: {Mathf.RoundToInt(stats.stamina)} / {Mathf.RoundToInt(stats.maxStamina)}";

        // Attribute labels
        if (intelligenceText != null)
            intelligenceText.text = $"Intelligence: {Mathf.RoundToInt(stats.effectiveIntelligence)}";

        if (strengthText != null)
            strengthText.text = $"Strength: {Mathf.RoundToInt(stats.effectiveStrength)}";

        if (staminaAttrText != null)
            staminaAttrText.text = $"Stamina (attr): {Mathf.RoundToInt(stats.effectiveStaminaAttr)}";

        if (agilityText != null)
            agilityText.text = $"Agility: {Mathf.RoundToInt(stats.effectiveAgility)}";
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

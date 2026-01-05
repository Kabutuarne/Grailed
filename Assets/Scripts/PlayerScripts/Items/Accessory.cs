using System.Collections.Generic;
using UnityEngine;

// Accessory items that can be placed in accessory slots. While equipped, they apply
// one or more PassiveEffect-like modifiers to the player, but these do not show in UI.
public class Accessory : ItemPickup
{
    [Header("Effects (applied while equipped)")]
    public PassiveEffect[] passiveEffects; // configured ScriptableObjects to read effect values from

    [Header("Presentation")]
    public Sprite inventoryIcon;
    public string title;
    public Color titleColor = Color.white;

    [System.Serializable]
    public class DescriptionRow
    {
        [TextArea]
        public string text;
        public Color color = Color.white;
    }

    [Header("Tooltip Description Rows")]
    public List<DescriptionRow> descriptionRows = new List<DescriptionRow>();

    private bool _equipped;
    private GameObject _owner;

    // Call when placed into an accessory slot.
    public void OnEquipped(GameObject user)
    {
        if (user == null || _equipped)
            return;

        _owner = user;
        var status = _owner.GetComponent<PlayerStatusEffects>();
        if (status == null)
            return;

        if (passiveEffects == null)
        {
            _equipped = true; // nothing to apply, but mark equipped to avoid repeat
            return;
        }

        foreach (var pe in passiveEffects)
        {
            if (pe == null) continue;

            // Build a unique id so removal only affects this accessory-applied instance
            string uniqueId = $"acc_{GetInstanceID()}_{(string.IsNullOrEmpty(pe.effectId) ? "effect" : pe.effectId)}";

            var e = new PlayerStatusEffects.Effect(uniqueId, -1f)
            {
                speedMultiplier = pe.speedMultiplier,
                healthRegenMultiplier = pe.healthRegenMultiplier,
                manaRegenMultiplier = pe.manaRegenMultiplier,
                healPerSecond = pe.healPerSecond,
                addStrength = pe.addStrength,
                addIntelligence = pe.addIntelligence,
                addStaminaAttr = pe.addStaminaAttr,
                addAgility = pe.addAgility,
                // Accessories should not appear in UI
                hideInUI = true
            };

            status.AddEffect(e);
        }

        _equipped = true;
    }

    // Call when removed from an accessory slot.
    public void OnUnequipped()
    {
        if (!_equipped || _owner == null)
        {
            _equipped = false;
            _owner = null;
            return;
        }

        var status = _owner.GetComponent<PlayerStatusEffects>();
        if (status != null && passiveEffects != null)
        {
            foreach (var pe in passiveEffects)
            {
                if (pe == null) continue;
                string uniqueId = $"acc_{GetInstanceID()}_{(string.IsNullOrEmpty(pe.effectId) ? "effect" : pe.effectId)}";
                status.RemoveEffect(uniqueId);
            }
        }

        _equipped = false;
        _owner = null;
    }

    void OnDestroy()
    {
        // Clean up effects if the object is destroyed while equipped
        OnUnequipped();
    }
}

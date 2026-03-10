using System.Collections.Generic;
using UnityEngine;

// Accessory items that can be placed in accessory slots. While equipped, they apply
// one or more passive modifiers to the player, but these do not show in the status UI.
public class Accessory : ItemPickup
{
    [Header("Effects (applied while equipped)")]
    public PassiveEffect[] passiveEffects;

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

    private bool equipped;
    private GameObject owner;

    public void OnEquipped(GameObject user)
    {
        if (user == null || equipped)
            return;

        owner = user;

        PlayerStatusEffects status = owner.GetComponent<PlayerStatusEffects>();
        if (status == null)
            return;

        if (passiveEffects != null)
        {
            foreach (PassiveEffect passiveEffect in passiveEffects)
            {
                if (passiveEffect == null)
                    continue;

                string uniqueId = GetUniqueEffectId(passiveEffect);

                PlayerStatusEffects.Effect effect = new PlayerStatusEffects.Effect(uniqueId, -1f)
                {
                    speedMultiplier = passiveEffect.speedMultiplier,
                    healthRegenMultiplier = passiveEffect.healthRegenMultiplier,
                    manaRegenMultiplier = passiveEffect.manaRegenMultiplier,
                    energyRegenMultiplier = passiveEffect.energyRegenMultiplier,
                    healthPerSecond = passiveEffect.healthPerSecond,
                    manaPerSecond = passiveEffect.manaPerSecond,
                    energyPerSecond = passiveEffect.energyPerSecond,
                    addStrength = passiveEffect.addStrength,
                    addIntelligence = passiveEffect.addIntelligence,
                    addStaminaAttr = passiveEffect.addStaminaAttr,
                    addAgility = passiveEffect.addAgility,
                    hideInUI = true
                };

                status.AddEffect(effect);
            }
        }

        equipped = true;
    }

    public void OnUnequipped()
    {
        if (!equipped || owner == null)
        {
            equipped = false;
            owner = null;
            return;
        }

        PlayerStatusEffects status = owner.GetComponent<PlayerStatusEffects>();
        if (status != null && passiveEffects != null)
        {
            foreach (PassiveEffect passiveEffect in passiveEffects)
            {
                if (passiveEffect == null)
                    continue;

                status.RemoveEffect(GetUniqueEffectId(passiveEffect));
            }
        }

        equipped = false;
        owner = null;
    }

    private string GetUniqueEffectId(PassiveEffect passiveEffect)
    {
        string baseId = passiveEffect != null && !string.IsNullOrEmpty(passiveEffect.effectId)
            ? passiveEffect.effectId
            : "effect";

        return $"acc_{GetInstanceID()}_{baseId}";
    }

    private void OnDestroy()
    {
        OnUnequipped();
    }
}
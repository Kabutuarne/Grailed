using UnityEngine;
using System;

/// <summary>
/// Applies stat modifications to an enemy when spawned.
/// </summary>
public class EnemyStatModifier : MonoBehaviour
{
    [Serializable]
    public class StatModificationData
    {
        public float intelligenceMultiplier = 1f;
        public float strengthMultiplier = 1f;
        public float staminaMultiplier = 1f;
        public float agilityMultiplier = 1f;

        public float? intelligenceOverride;
        public float? strengthOverride;
        public float? staminaOverride;
        public float? agilityOverride;

        public float? baseMaxHealthOverride;
        public float? baseMaxManaOverride;
        public float? baseMaxEnergyOverride;

        public float? baseHealthRegenOverride;
        public float? baseManaRegenOverride;
        public float? baseEnergyRegenOverride;

        public bool scaleHealthWithStrength = true;
        public bool scaleManaWithIntelligence = true;
        public bool scaleEnergyWithStamina = true;
    }

    public StatModificationData modification;

    /// <summary>
    /// Applies stat modifications to the EnemyStats component
    /// </summary>
    public void ApplyModifications()
    {
        EnemyStats stats = GetComponent<EnemyStats>();
        if (stats == null)
        {
            Debug.LogError($"[EnemyStatModifier] No EnemyStats component found on {gameObject.name}");
            return;
        }

        // Apply overrides first
        if (modification.intelligenceOverride.HasValue)
            stats.intelligence = modification.intelligenceOverride.Value;
        else if (modification.intelligenceMultiplier != 1f)
            stats.intelligence *= modification.intelligenceMultiplier;

        if (modification.strengthOverride.HasValue)
            stats.strength = modification.strengthOverride.Value;
        else if (modification.strengthMultiplier != 1f)
            stats.strength *= modification.strengthMultiplier;

        if (modification.staminaOverride.HasValue)
            stats.stamina = modification.staminaOverride.Value;
        else if (modification.staminaMultiplier != 1f)
            stats.stamina *= modification.staminaMultiplier;

        if (modification.agilityOverride.HasValue)
            stats.agility = modification.agilityOverride.Value;
        else if (modification.agilityMultiplier != 1f)
            stats.agility *= modification.agilityMultiplier;

        // Apply base stat overrides
        if (modification.baseMaxHealthOverride.HasValue)
            stats.baseMaxHealth = modification.baseMaxHealthOverride.Value;

        if (modification.baseMaxManaOverride.HasValue)
            stats.baseMaxMana = modification.baseMaxManaOverride.Value;

        if (modification.baseMaxEnergyOverride.HasValue)
            stats.baseMaxEnergy = modification.baseMaxEnergyOverride.Value;

        if (modification.baseHealthRegenOverride.HasValue)
            stats.baseHealthRegen = modification.baseHealthRegenOverride.Value;

        if (modification.baseManaRegenOverride.HasValue)
            stats.baseManaRegen = modification.baseManaRegenOverride.Value;

        if (modification.baseEnergyRegenOverride.HasValue)
            stats.baseEnergyRegen = modification.baseEnergyRegenOverride.Value;

        // Apply scaling flags (these would need to be exposed in EnemyStats)
        // You could extend EnemyStats to support these flags

        // Recalculate current resources based on new max values
        stats.ClampResources();

        Debug.Log($"[EnemyStatModifier] Applied modifications to {gameObject.name}: " +
                  $"STR={stats.strength:F1}, INT={stats.intelligence:F1}, " +
                  $"STA={stats.stamina:F1}, AGI={stats.agility:F1}, " +
                  $"MaxHealth={stats.MaxHealth:F0}");
    }
}
using UnityEngine;

public static class ManaUtility
{
    /// <summary>
    /// Shared mana spending path used by all spells.
    /// Works on any GameObject that has a PlayerStats or uses StatusEffects as fallback.
    /// </summary>
    public static bool TrySpendMana(GameObject caster, float amount, string fallbackEffectId)
    {
        if (caster == null || amount <= 0f)
            return true;

        // Try IResourceHandler first (works for both PlayerStats and EnemyStats)
        IResourceHandler resourceHandler = caster.GetComponent<IResourceHandler>();
        if (resourceHandler != null)
        {
            // For player, check if enough mana before spending
            PlayerStats playerStats = resourceHandler as PlayerStats;
            if (playerStats != null)
                return playerStats.TrySpendMana(amount);

            // For enemies, just spend it directly
            resourceHandler.ModifyMana(-amount);
            return true;
        }

        // Fallback: apply as a status effect
        StatusEffects status = caster.GetComponent<StatusEffects>();
        if (status != null)
        {
            status.AddEffect(new StatusEffectData(fallbackEffectId, 0f) { manaAmount = -amount });
            return true;
        }

        return true;
    }
}
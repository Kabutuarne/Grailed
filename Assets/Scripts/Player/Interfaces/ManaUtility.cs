using UnityEngine;

public static class ManaUtility
{
    // Shared mana spending path used by all spells.
    public static bool TrySpendMana(GameObject caster, float amount, string fallbackEffectId)
    {
        if (caster == null || amount <= 0f)
            return true;

        PlayerStats stats = caster.GetComponent<PlayerStats>();
        if (stats != null)
            return stats.TrySpendMana(amount);

        StatusEffects status = caster.GetComponent<StatusEffects>();
        if (status != null)
        {
            status.AddEffect(new StatusEffectData(fallbackEffectId, 0f) { manaAmount = -amount });
            return true;
        }

        return true;
    }
}
using System.Collections.Generic;
using UnityEngine;

public class StatusEffects : MonoBehaviour
{
    public IReadOnlyList<StatusEffectData> ActiveEffects => effects;
    public void ClearAllEffects() => effects.Clear();
    private List<StatusEffectData> effects = new List<StatusEffectData>();
    private PlayerStats stats;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            var e = effects[i];

            if (!e.IsInfinite)
            {
                e.timer -= Time.deltaTime;
                if (e.timer <= 0f)
                {
                    effects.RemoveAt(i);
                    continue;
                }
            }

            ApplyOverTime(e);
        }
    }

    public void AddEffect(StatusEffectData effect)
    {
        if (effect == null)
            return;

        if (effect.IsInstant)
        {
            ApplyInstant(effect);
            return;
        }

        effects.RemoveAll(e => e.id == effect.id);
        effects.Add(effect);
    }

    public void RemoveEffect(string id)
    {
        effects.RemoveAll(e => e.id == id);
    }

    private void ApplyInstant(StatusEffectData e)
    {
        if (stats == null) return;

        stats.Heal(e.healAmount);
        stats.RestoreMana(e.manaAmount);
        stats.RestoreEnergy(e.energyAmount);
    }

    private void ApplyOverTime(StatusEffectData e)
    {
        if (stats == null) return;

        stats.Heal(e.healthPerSecond * Time.deltaTime);
        stats.RestoreMana(e.manaPerSecond * Time.deltaTime);
        stats.RestoreEnergy(e.energyPerSecond * Time.deltaTime);
    }

    // ===== Aggregation =====

    public float GetSpeedMultiplier()
    {
        float m = 1f;
        foreach (var e in effects) m *= e.speedMultiplier;
        return m;
    }

    public float GetHealthRegenMultiplier()
    {
        float m = 1f;
        foreach (var e in effects) m *= e.healthRegenMultiplier;
        return m;
    }

    public float GetManaRegenMultiplier()
    {
        float m = 1f;
        foreach (var e in effects) m *= e.manaRegenMultiplier;
        return m;
    }

    public float GetEnergyRegenMultiplier()
    {
        float m = 1f;
        foreach (var e in effects) m *= e.energyRegenMultiplier;
        return m;
    }

    public float GetStrengthAdd()
    {
        float v = 0f;
        foreach (var e in effects) v += e.addStrength;
        return v;
    }

    public float GetIntelligenceAdd()
    {
        float v = 0f;
        foreach (var e in effects) v += e.addIntelligence;
        return v;
    }

    public float GetStaminaAdd()
    {
        float v = 0f;
        foreach (var e in effects) v += e.addStaminaAttr;
        return v;
    }

    public float GetAgilityAdd()
    {
        float v = 0f;
        foreach (var e in effects) v += e.addAgility;
        return v;
    }
}
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PlayerStatusEffects : MonoBehaviour
{
    private readonly Dictionary<string, GameObject> activeParticles = new Dictionary<string, GameObject>();

    [Serializable]
    public class Effect
    {
        public string id;
        public float duration;
        public float timer;
        public bool hideInUI = false;
        public float speedMultiplier = 1f;
        public float healthRegenMultiplier = 1f;
        public float manaRegenMultiplier = 1f;
        public float energyRegenMultiplier = 1f;
        public float healAmount = 0f;
        public float manaAmount = 0f;
        public float energyAmount = 0f;
        public float healthPerSecond = 0f;
        public float manaPerSecond = 0f;
        public float energyPerSecond = 0f;
        public float addStrength = 0f;
        public float addIntelligence = 0f;
        public float addStaminaAttr = 0f;
        public float addAgility = 0f;
        public EffectCarrier carrier;

        public Effect(string id)
        {
            this.id = id;
        }

        public Effect(string id, float duration)
        {
            this.id = id;
            this.duration = duration;
            this.timer = duration;
        }
    }

    private struct ResourceSnapshot
    {
        public bool valid;
        public float health;
        public float maxHealth;
        public float mana;
        public float maxMana;
        public float energy;
        public float maxEnergy;
    }

    public List<Effect> activeEffects = new List<Effect>();

    private PlayerStats stats;

    private void Start()
    {
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        bool removedAny = false;
        ResourceSnapshot beforeRemoval = default;
        bool removalSnapshotCaptured = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            Effect effect = activeEffects[i];

            if (effect.duration > 0f)
            {
                ApplyPerSecond(effect, Time.deltaTime);
                effect.timer -= Time.deltaTime;

                if (effect.timer <= 0f)
                {
                    if (!removalSnapshotCaptured)
                    {
                        beforeRemoval = CaptureResourceSnapshot();
                        removalSnapshotCaptured = true;
                    }
                    RemoveEffectAt(i, effect.id);
                    removedAny = true;
                }
            }
            else if (effect.duration == 0f)
            {
                if (!removalSnapshotCaptured)
                {
                    beforeRemoval = CaptureResourceSnapshot();
                    removalSnapshotCaptured = true;
                }
                RemoveEffectAt(i, effect.id);
                removedAny = true;
            }
            else
            {
                ApplyPerSecond(effect, Time.deltaTime);
            }
        }

        if (removedAny)
            SyncResourcesAfterAttributeChange(beforeRemoval);
    }

    public void AddEffect(Effect effect)
    {
        if (effect == null) return;

        if (effect.duration == 0f)
        {
            if (effect.healAmount != 0f)
                SafeHeal(effect.healAmount);

            if (effect.manaAmount != 0f)
                SafeRestoreMana(effect.manaAmount);

            if (effect.energyAmount != 0f)
                SafeRestoreEnergy(effect.energyAmount);

            return;
        }

        ResourceSnapshot beforeAdd = CaptureResourceSnapshot();

        if (effect.duration > 0f)
            effect.timer = effect.duration;

        activeEffects.Add(effect);

        if (effect.duration > 0f && effect.carrier != null && effect.carrier.particlePrefab != null && !activeParticles.ContainsKey(effect.id))
        {
            GameObject particle = Instantiate(effect.carrier.particlePrefab, transform);
            activeParticles[effect.id] = particle;
        }

        SyncResourcesAfterAttributeChange(beforeAdd);
    }

    public void AddHealthRegenEffect(string id, float multiplier, float duration)
        => AddEffect(new Effect(id, duration) { healthRegenMultiplier = multiplier });

    public void AddManaRegenEffect(string id, float multiplier, float duration)
        => AddEffect(new Effect(id, duration) { manaRegenMultiplier = multiplier });

    public void AddEnergyRegenEffect(string id, float multiplier, float duration)
        => AddEffect(new Effect(id, duration) { energyRegenMultiplier = multiplier });

    public void AddHealthEffect(string id, float amount)
        => AddEffect(new Effect(id) { duration = 0f, healAmount = amount });

    public void AddManaEffect(string id, float amount)
        => AddEffect(new Effect(id) { duration = 0f, manaAmount = amount });

    public void AddEnergyEffect(string id, float amount)
        => AddEffect(new Effect(id) { duration = 0f, energyAmount = amount });

    public void AddSprintEffect(string id, float multiplier, float duration)
        => AddEffect(new Effect(id, duration) { speedMultiplier = multiplier });

    public void AddStrengthEffect(string id, float amount, float duration)
        => AddEffect(new Effect(id, duration) { addStrength = amount });

    public void AddIntelligenceEffect(string id, float amount, float duration)
        => AddEffect(new Effect(id, duration) { addIntelligence = amount });

    public void AddStaminaAttributeEffect(string id, float amount, float duration)
        => AddEffect(new Effect(id, duration) { addStaminaAttr = amount });

    public void AddAgilityEffect(string id, float amount, float duration)
        => AddEffect(new Effect(id, duration) { addAgility = amount });

    public float GetSpeedMultiplier()
    {
        float multiplier = 1f;
        foreach (Effect effect in activeEffects)
            multiplier *= effect.speedMultiplier;
        return multiplier;
    }

    public float GetHealthRegenMultiplier()
    {
        float multiplier = 1f;
        foreach (Effect effect in activeEffects)
            multiplier *= effect.healthRegenMultiplier;
        return multiplier;
    }

    public float GetManaRegenMultiplier()
    {
        float multiplier = 1f;
        foreach (Effect effect in activeEffects)
            multiplier *= effect.manaRegenMultiplier;
        return multiplier;
    }

    public float GetEnergyRegenMultiplier()
    {
        float multiplier = 1f;
        foreach (Effect effect in activeEffects)
            multiplier *= effect.energyRegenMultiplier;
        return multiplier;
    }

    public float GetStrengthAdd()
    {
        float total = 0f;
        foreach (Effect effect in activeEffects)
            total += effect.addStrength;
        return total;
    }

    public float GetIntelligenceAdd()
    {
        float total = 0f;
        foreach (Effect effect in activeEffects)
            total += effect.addIntelligence;
        return total;
    }

    public float GetStaminaAttrAdd()
    {
        float total = 0f;
        foreach (Effect effect in activeEffects)
            total += effect.addStaminaAttr;
        return total;
    }

    public float GetAgilityAdd()
    {
        float total = 0f;
        foreach (Effect effect in activeEffects)
            total += effect.addAgility;
        return total;
    }

    public void RemoveEffect(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        ResourceSnapshot beforeRemoval = CaptureResourceSnapshot();
        bool removedAny = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i].id != id) continue;
            RemoveEffectAt(i, id);
            removedAny = true;
        }

        if (removedAny)
            SyncResourcesAfterAttributeChange(beforeRemoval);
    }

    public void ClearAllEffects()
    {
        if (activeEffects.Count == 0 && activeParticles.Count == 0)
            return;

        ResourceSnapshot beforeClear = CaptureResourceSnapshot();
        activeEffects.Clear();

        foreach (GameObject particle in activeParticles.Values)
        {
            if (particle != null)
                Destroy(particle);
        }

        activeParticles.Clear();
        SyncResourcesAfterAttributeChange(beforeClear);
    }

    private void ApplyPerSecond(Effect effect, float deltaTime)
    {
        if (effect.healthPerSecond != 0f)
            SafeHeal(effect.healthPerSecond * deltaTime);

        if (effect.manaPerSecond != 0f)
            SafeRestoreMana(effect.manaPerSecond * deltaTime);

        if (effect.energyPerSecond != 0f)
            SafeRestoreEnergy(effect.energyPerSecond * deltaTime);
    }

    private void RemoveEffectAt(int index, string id)
    {
        if (index < 0 || index >= activeEffects.Count) return;

        if (activeParticles.TryGetValue(id, out GameObject particle) && particle != null)
        {
            Destroy(particle);
            activeParticles.Remove(id);
        }

        activeEffects.RemoveAt(index);
    }

    private void SyncResourcesAfterAttributeChange(ResourceSnapshot before)
    {
        if (!before.valid)
        {
            SafeClampResourcesToMax();
            return;
        }

        SafeClampResourcesToMax();

        ResourceSnapshot after = CaptureResourceSnapshot();
        if (!after.valid) return;

        AdjustResourceForMaxChange("health", before.health, before.maxHealth, after.maxHealth);
        AdjustResourceForMaxChange("mana", before.mana, before.maxMana, after.maxMana);
        AdjustResourceForMaxChange("energy", before.energy, before.maxEnergy, after.maxEnergy);

        SafeClampResourcesToMax();
    }

    private void AdjustResourceForMaxChange(string resourceKey, float beforeCurrent, float beforeMax, float afterMax)
    {
        if (Mathf.Approximately(beforeMax, afterMax)) return;

        float desiredCurrent = afterMax > beforeMax
            ? beforeCurrent + (afterMax - beforeMax)
            : Mathf.Min(beforeCurrent, afterMax);

        desiredCurrent = Mathf.Clamp(desiredCurrent, 0f, afterMax);
        TrySetCurrentResourceValue(resourceKey, desiredCurrent);
    }

    private ResourceSnapshot CaptureResourceSnapshot()
    {
        object target = GetStatsTarget();
        if (target == null) return default;

        return new ResourceSnapshot
        {
            valid = true,
            health    = TryGetFirstFloat(target, "health", "currentHealth", "currentHP", "hp"),
            maxHealth = TryGetFirstFloat(target, "maxHealth", "maxHP"),
            mana      = TryGetFirstFloat(target, "mana", "currentMana"),
            maxMana   = TryGetFirstFloat(target, "maxMana"),
            energy    = TryGetFirstFloat(target, "energy", "currentEnergy", "sprint", "currentSprint"),
            maxEnergy = TryGetFirstFloat(target, "maxEnergy", "maxSprint")
        };
    }

    private object GetStatsTarget()
    {
        if (stats != null) return stats;
        stats = GetComponent<PlayerStats>();
        return stats;
    }

    private void SafeHeal(float amount)
    {
        try { if (stats != null) stats.Heal(amount); }
        catch (Exception ex) { Debug.LogError($"[StatusEffects] SafeHeal exception: {ex.Message}"); }
    }

    private void SafeRestoreMana(float amount)
    {
        try { if (stats != null) stats.RestoreMana(amount); }
        catch (Exception ex) { Debug.LogError($"[StatusEffects] SafeRestoreMana exception: {ex.Message}"); }
    }

    private void SafeRestoreEnergy(float amount)
    {
        object target = GetStatsTarget();
        if (target == null) return;

        try
        {
            MethodInfo method =
                target.GetType().GetMethod("RestoreEnergy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                target.GetType().GetMethod("RestoreSprint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                target.GetType().GetMethod("AddEnergy",     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                target.GetType().GetMethod("AddSprint",     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method != null) { method.Invoke(target, new object[] { amount }); return; }

            float current  = TryGetFirstFloat(target, "energy", "currentEnergy", "sprint", "currentSprint");
            float max      = TryGetFirstFloat(target, "maxEnergy", "maxSprint");
            float newValue = Mathf.Clamp(current + amount, 0f, max);
            TrySetFirstFloat(target, newValue, "energy", "currentEnergy", "sprint", "currentSprint");
        }
        catch (Exception ex) { Debug.LogError($"[StatusEffects] SafeRestoreEnergy exception: {ex.Message}"); }
    }

    private void SafeClampResourcesToMax()
    {
        try { if (stats != null) stats.ClampResourcesToMax(); } catch { }
    }

    private float TryGetFirstFloat(object target, params string[] memberNames)
    {
        foreach (string name in memberNames)
            if (TryGetFloatMember(target, name, out float value)) return value;
        return 0f;
    }

    private bool TrySetCurrentResourceValue(string resourceKey, float value)
    {
        object target = GetStatsTarget();
        if (target == null) return false;

        switch (resourceKey)
        {
            case "health": return TrySetFirstFloat(target, value, "health", "currentHealth", "currentHP", "hp");
            case "mana":   return TrySetFirstFloat(target, value, "mana", "currentMana");
            case "energy": return TrySetFirstFloat(target, value, "energy", "currentEnergy", "sprint", "currentSprint");
            default:       return false;
        }
    }

    private bool TrySetFirstFloat(object target, float value, params string[] memberNames)
    {
        foreach (string name in memberNames)
            if (TrySetFloatMember(target, name, value)) return true;
        return false;
    }

    private bool TryGetFloatMember(object target, string memberName, out float value)
    {
        value = 0f;
        if (target == null || string.IsNullOrEmpty(memberName)) return false;

        Type type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(float))
        {
            value = (float)field.GetValue(target);
            return true;
        }

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanRead && property.PropertyType == typeof(float))
        {
            value = (float)property.GetValue(target, null);
            return true;
        }

        return false;
    }

    private bool TrySetFloatMember(object target, string memberName, float value)
    {
        if (target == null || string.IsNullOrEmpty(memberName)) return false;

        Type type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(target, value);
            return true;
        }

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && property.PropertyType == typeof(float))
        {
            property.SetValue(target, value, null);
            return true;
        }

        return false;
    }
}
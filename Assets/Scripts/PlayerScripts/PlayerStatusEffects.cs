using UnityEngine;
using System.Collections.Generic;

public class PlayerStatusEffects : MonoBehaviour
{
    [System.Serializable]
    public class Effect
    {
        public string id;
        // Duration in seconds. duration > 0 -> timed, duration == 0 -> instant effect, duration < 0 -> toggle (on/off) effect
        public float duration;
        // running timer (if timed)
        public float timer;

        // If true, this effect should not be shown in the Status Effects UI
        public bool hideInUI = false;

        // Multiplicative / additive effect fields (set only what you need)
        public float speedMultiplier = 1f;             // multiplies movementSpeed
        public float healthRegenMultiplier = 1f;       // multiplies passive health regen
        public float manaRegenMultiplier = 1f;         // multiplies passive mana regen

        // Instant resource additions (applied once when added)
        public float healAmount = 0f;                  // instant health
        public float manaAmount = 0f;                  // instant mana

        // Per-second healing (optional)
        public float healPerSecond = 0f;

        // Attribute additive amounts (applied while active)
        public float addStrength = 0f;
        public float addIntelligence = 0f;
        public float addStaminaAttr = 0f;
        public float addAgility = 0f;
        // Reference to the runtime origin container (EffectCarrier) so UI can show title/icon/time-left
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

    public List<Effect> activeEffects = new List<Effect>();
    PlayerStats stats;

    void Start()
    {
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        bool removedAny = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var e = activeEffects[i];
            if (e.duration > 0f)
            {
                e.timer -= Time.deltaTime;
                if (e.healPerSecond != 0)
                    stats.Heal(e.healPerSecond * Time.deltaTime);

                if (e.timer <= 0f)
                {
                    Debug.Log($"[StatusEffects] Effect '{e.id}' expired");
                    activeEffects.RemoveAt(i);
                    removedAny = true;
                }
            }
            else if (e.duration == 0f)
            {
                // Instant effects should not be in the list, but just in case remove them
                activeEffects.RemoveAt(i);
                removedAny = true;
            }
            else // duration < 0: toggle / infinite effect
            {
                if (e.healPerSecond != 0)
                    stats.Heal(e.healPerSecond * Time.deltaTime);
                // don't remove
            }
        }

        // If any effects were removed or expired, ensure resources do not exceed new maximums
        if (removedAny && stats != null)
        {
            stats.ClampResourcesToMax();
        }
    }

    // Generic adder — callers can build an Effect and add it, or use helpers below.
    public void AddEffect(Effect e)
    {
        // Instant application
        if (e.duration == 0f)
        {
            if (e.healAmount != 0f)
                stats.Heal(e.healAmount);

            if (e.manaAmount != 0f)
                stats.RestoreMana(e.manaAmount);

            // if instant also had per-second healing, do a single tick
            if (e.healPerSecond != 0f)
                stats.Heal(e.healPerSecond * Time.deltaTime);

            Debug.Log($"[StatusEffects] Applied instant effect '{e.id}' (heal {e.healAmount}, mana {e.manaAmount})");
            // don't add to list
            return;
        }

        // For timed effects, set timer and add
        if (e.duration > 0f)
            e.timer = e.duration;

        // duration < 0 stays as infinite toggle (timer unused)
        activeEffects.Add(e);
        Debug.Log($"[StatusEffects] Added effect '{e.id}' duration={e.duration} speedMult={e.speedMultiplier} hRegenMult={e.healthRegenMultiplier} mRegenMult={e.manaRegenMultiplier} addStr={e.addStrength} addInt={e.addIntelligence} addStam={e.addStaminaAttr} addAgi={e.addAgility}");
    }

    // Convenience helpers for required effect types
    public void AddHealthRegenEffect(string id, float multiplier, float duration)
    {
        var e = new Effect(id, duration) { healthRegenMultiplier = multiplier };
        AddEffect(e);
    }

    public void AddManaRegenEffect(string id, float multiplier, float duration)
    {
        var e = new Effect(id, duration) { manaRegenMultiplier = multiplier };
        AddEffect(e);
    }

    public void AddHealthEffect(string id, float hpAmount)
    {
        var e = new Effect(id) { duration = 0f, healAmount = hpAmount };
        AddEffect(e);
    }

    public void AddManaEffect(string id, float manaAmount)
    {
        var e = new Effect(id) { duration = 0f, manaAmount = manaAmount };
        AddEffect(e);
    }

    public void AddSprintEffect(string id, float multiplier, float duration)
    {
        var e = new Effect(id, duration) { speedMultiplier = multiplier };
        AddEffect(e);
    }

    public void AddStrengthEffect(string id, float amount, float duration)
    {
        var e = new Effect(id, duration) { addStrength = amount };
        AddEffect(e);
    }

    public void AddIntelligenceEffect(string id, float amount, float duration)
    {
        var e = new Effect(id, duration) { addIntelligence = amount };
        AddEffect(e);
    }

    public void AddStaminaAttributeEffect(string id, float amount, float duration)
    {
        var e = new Effect(id, duration) { addStaminaAttr = amount };
        AddEffect(e);
    }

    public void AddAgilityEffect(string id, float amount, float duration)
    {
        var e = new Effect(id, duration) { addAgility = amount };
        AddEffect(e);
    }

    public float GetSpeedMultiplier()
    {
        float mult = 1f;
        foreach (var e in activeEffects)
            mult *= e.speedMultiplier;

        return mult;
    }

    // Aggregators for modifiers provided by active effects
    public float GetHealthRegenMultiplier()
    {
        float m = 1f;
        foreach (var e in activeEffects)
            m *= e.healthRegenMultiplier;
        return m;
    }

    public float GetManaRegenMultiplier()
    {
        float m = 1f;
        foreach (var e in activeEffects)
            m *= e.manaRegenMultiplier;
        return m;
    }

    public float GetStrengthAdd()
    {
        float add = 0f;
        foreach (var e in activeEffects)
            add += e.addStrength;
        return add;
    }

    public float GetIntelligenceAdd()
    {
        float add = 0f;
        foreach (var e in activeEffects)
            add += e.addIntelligence;
        return add;
    }

    public float GetStaminaAttrAdd()
    {
        float add = 0f;
        foreach (var e in activeEffects)
            add += e.addStaminaAttr;
        return add;
    }

    public float GetAgilityAdd()
    {
        float add = 0f;
        foreach (var e in activeEffects)
            add += e.addAgility;
        return add;
    }

    // Remove effect by id (removes all matching)
    public void RemoveEffect(string id)
    {
        int removed = activeEffects.RemoveAll(x => x.id == id);
        if (removed > 0)
        {
            Debug.Log($"[StatusEffects] Removed {removed} effect(s) with id '{id}'");
            if (stats != null)
                stats.ClampResourcesToMax();
        }
    }

    // Remove all active/toggle effects and clamp resources to new maxes
    public void ClearAllEffects()
    {
        int removed = activeEffects.Count;
        activeEffects.Clear();
        if (removed > 0)
        {
            Debug.Log($"[StatusEffects] Cleared all effects ({removed})");
            if (stats != null)
                stats.ClampResourcesToMax();
        }
    }
}
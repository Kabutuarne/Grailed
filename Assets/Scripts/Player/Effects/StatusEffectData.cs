using System;
using UnityEngine;

[Serializable]
public class StatusEffectData
{
    public string id;
    public float duration;
    public float timer;

    public EffectCarrier carrier;
    public bool hideInUI;
    // Multipliers
    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;
    public float energyRegenMultiplier = 1f;

    // Instant
    public float healAmount;
    public float manaAmount;
    public float energyAmount;

    // Over time
    public float healthPerSecond;
    public float manaPerSecond;
    public float energyPerSecond;

    // Attributes
    public float addStrength;
    public float addIntelligence;
    public float addStaminaAttr;
    public float addAgility;

    public bool IsInstant => duration == 0f;
    public bool IsInfinite => duration < 0f;

    public StatusEffectData(string id, float duration)
    {
        this.id = id;
        this.duration = duration;
        this.timer = duration;
    }

    [NonSerialized]
    public GameObject runtimeParticleInstance;
}
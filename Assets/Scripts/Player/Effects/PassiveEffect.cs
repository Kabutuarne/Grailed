using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/PassiveEffect")]
public class PassiveEffect : PlayerEffect
{
    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;
    public float energyRegenMultiplier = 1f;

    public float healthPerSecond;
    public float manaPerSecond;
    public float energyPerSecond;

    public float addStrength;
    public float addIntelligence;
    public float addStaminaAttr;
    public float addAgility;

    public override StatusEffectData CreateEffect(EffectCarrier carrier)
    {
        return new StatusEffectData(EffectTitle, -1f)
        {
            carrier = carrier,
            speedMultiplier = speedMultiplier,
            healthRegenMultiplier = healthRegenMultiplier,
            manaRegenMultiplier = manaRegenMultiplier,
            energyRegenMultiplier = energyRegenMultiplier,
            healthPerSecond = healthPerSecond,
            manaPerSecond = manaPerSecond,
            energyPerSecond = energyPerSecond,
            addStrength = addStrength,
            addIntelligence = addIntelligence,
            addStaminaAttr = addStaminaAttr,
            addAgility = addAgility
        };
    }
}
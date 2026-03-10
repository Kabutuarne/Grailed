using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/DurationEffect", fileName = "NewDurationEffect")]
public class DurationEffect : PlayerEffect
{
    [Header("Duration")]
    [Tooltip("> 0 = timed effect, < 0 = toggle / infinite effect")]
    public float duration = 5f;

    [Header("Multipliers")]
    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;
    public float energyRegenMultiplier = 1f;

    [Header("Per-second values")]
    [Tooltip("Health added every second while active (positive = heal, negative = damage).")]
    public float healthPerSecond = 0f;

    [Tooltip("Mana added every second while active (positive = restore, negative = drain).")]
    public float manaPerSecond = 0f;

    [Tooltip("Energy / sprint added every second while active (positive = restore, negative = drain).")]
    public float energyPerSecond = 0f;

    [Header("Temporary attribute adds (applied only while active)")]
    public float addStrength = 0f;
    public float addIntelligence = 0f;
    public float addStaminaAttr = 0f;
    public float addAgility = 0f;

    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null) return;

        var status = user.GetComponent<PlayerStatusEffects>();
        if (status == null)
        {
            Debug.LogWarning($"DurationEffect '{displayName}' cannot be applied: PlayerStatusEffects missing on {user.name}");
            return;
        }

        var effect = new PlayerStatusEffects.Effect(effectId, duration)
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

        status.AddEffect(effect);
    }
}
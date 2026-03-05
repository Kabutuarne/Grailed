using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/DurationEffect", fileName = "NewDurationEffect")]
public class DurationEffect : PlayerEffect
{
    [Header("Duration")]
    public float duration = 5f; // seconds (>0 timed, <0 for toggle if desired)

    [Header("Multipliers")]
    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;

    [Header("Per-second & instant")]
    [Tooltip("Health per second (positive = heal, negative = damage). Applied continuously for duration.")]
    public float healthPerSecond = 0f;
    [Tooltip("Health amount applied instantly when effect starts (positive = heal, negative = damage).")]
    public float healthAmount = 0f;
    [Tooltip("Mana amount applied instantly when effect starts (positive = restore, negative = drain).")]
    public float manaAmount = 0f;

    [Header("Temporary attribute adds (applied while active)")]
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

        Debug.Log($"[DurationEffect] Applying to {user.name}: duration={duration}s, health/sec={healthPerSecond}, instant health={healthAmount}, instant mana={manaAmount}");

        // If this effect includes instant resource changes, apply them instantly first
        if (healthAmount != 0f)
        {
            status.AddHealthEffect(effectId + "_insthealth", healthAmount);
            Debug.Log($"[DurationEffect] Instant health applied: {healthAmount}");
        }

        if (manaAmount != 0f)
        {
            status.AddManaEffect(effectId + "_instmana", manaAmount);
            Debug.Log($"[DurationEffect] Instant mana applied: {manaAmount}");
        }

        // Build a timed effect for the rest and add it
        var e = new PlayerStatusEffects.Effect(effectId, duration)
        {
            speedMultiplier = speedMultiplier,
            healthRegenMultiplier = healthRegenMultiplier,
            manaRegenMultiplier = manaRegenMultiplier,
            healPerSecond = healthPerSecond,
            addStrength = addStrength,
            addIntelligence = addIntelligence,
            addStaminaAttr = addStaminaAttr,
            addAgility = addAgility
        };

        // mark the effect as originating from this carrier (if provided)
        e.carrier = carrier;

        status.AddEffect(e);
        Debug.Log($"[DurationEffect] Timed effect applied: {duration}s with {healthPerSecond}/sec health");
    }
}

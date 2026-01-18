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
    public float healPerSecond = 0f;
    public float healAmount = 0f; // applied instantly on add if desired
    public float manaAmount = 0f; // applied instantly on add if desired

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

        // If this effect includes instant resource additions, apply them instantly first
        if (healAmount != 0f)
            status.AddHealthEffect(effectId + "_instheal", healAmount);

        if (manaAmount != 0f)
            status.AddManaEffect(effectId + "_instmana", manaAmount);

        // Build a timed effect for the rest and add it
        var e = new PlayerStatusEffects.Effect(effectId, duration)
        {
            speedMultiplier = speedMultiplier,
            healthRegenMultiplier = healthRegenMultiplier,
            manaRegenMultiplier = manaRegenMultiplier,
            healPerSecond = healPerSecond,
            addStrength = addStrength,
            addIntelligence = addIntelligence,
            addStaminaAttr = addStaminaAttr,
            addAgility = addAgility
        };

        // mark the effect as originating from this carrier (if provided)
        e.carrier = carrier;

        status.AddEffect(e);
    }
}

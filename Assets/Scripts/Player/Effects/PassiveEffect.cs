using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/PassiveEffect", fileName = "NewPassiveEffect")]
public class PassiveEffect : PlayerEffect
{
    [Header("Multipliers & attributes (applied while active)")]
    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;

    public float healPerSecond = 0f;

    public float addStrength = 0f;
    public float addIntelligence = 0f;
    public float addStaminaAttr = 0f;
    public float addAgility = 0f;

    // Apply by adding an infinite (toggle) effect — duration < 0 treated as toggle
    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null) return;
        var status = user.GetComponent<PlayerStatusEffects>();
        if (status == null)
        {
            Debug.LogWarning($"PassiveEffect '{displayName}' cannot be applied: PlayerStatusEffects missing on {user.name}");
            return;
        }

        var e = new PlayerStatusEffects.Effect(effectId, -1f)
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
        e.carrier = carrier;

        status.AddEffect(e);
    }

    public override void Remove(GameObject user)
    {
        if (user == null) return;
        var status = user.GetComponent<PlayerStatusEffects>();
        if (status == null) return;
        status.RemoveEffect(effectId);
    }
}

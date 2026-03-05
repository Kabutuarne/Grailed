using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/InstantEffect", fileName = "NewInstantEffect")]
public class InstantEffect : PlayerEffect
{
    [Header("Health & Mana")]
    [Tooltip("Amount to heal (+) or damage (-). Applied immediately.")]
    public float healthAmount = 0f;
    [Tooltip("Amount to restore (+) or drain (-). Applied immediately.")]
    public float manaAmount = 0f;

    // [Header("Speed/Sprint (optional duration for temporary sprint)")]
    // public float speedMultiplier = 1f;
    // public float sprintDuration = 0f; // duration > 0 for temporary speed boost, 0 for instant-only

    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null) return;
        var status = user.GetComponent<PlayerStatusEffects>();
        if (status == null)
        {
            Debug.LogWarning($"InstantEffect '{displayName}' cannot be applied: PlayerStatusEffects component missing on {user.name}");
            return;
        }

        Debug.Log($"[InstantEffect] Applying to {user.name}: health={healthAmount}, mana={manaAmount}");

        // Apply instant health change (positive = heal, negative = damage)
        if (healthAmount != 0f)
        {
            var healthEffect = new PlayerStatusEffects.Effect(effectId + "_health");
            healthEffect.duration = 0f;
            healthEffect.carrier = carrier;
            healthEffect.healAmount = healthAmount;  // Can be positive or negative
            status.AddEffect(healthEffect);
            Debug.Log($"[InstantEffect] Health effect applied: {healthAmount}");
        }

        // Apply instant mana change (positive = restore, negative = drain)
        if (manaAmount != 0f)
        {
            var manaEffect = new PlayerStatusEffects.Effect(effectId + "_mana");
            manaEffect.duration = 0f;
            manaEffect.carrier = carrier;
            manaEffect.manaAmount = manaAmount;  // Can be positive or negative
            status.AddEffect(manaEffect);
            Debug.Log($"[InstantEffect] Mana effect applied: {manaAmount}");
        }

        // Apply speed effect (instant or temporary based on sprintDuration)
        // if (speedMultiplier != 1f)
        // {
        //     var speedEffect = new PlayerStatusEffects.Effect(effectId + "_speed", sprintDuration);
        //     speedEffect.carrier = carrier;
        //     speedEffect.speedMultiplier = speedMultiplier;
        //     status.AddEffect(speedEffect);
        //     Debug.Log($"[InstantEffect] Speed effect applied: {speedMultiplier}x");
        // }
    }
}

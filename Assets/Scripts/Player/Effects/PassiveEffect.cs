using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/PassiveEffect", fileName = "NewPassiveEffect")]
public class PassiveEffect : PlayerEffect
{
    [Header("Multipliers & attributes (applied while active)")]
    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;
    public float energyRegenMultiplier = 1f;

    [Header("Per-second values (applied while active)")]
    public float healthPerSecond = 0f;
    public float manaPerSecond = 0f;
    public float energyPerSecond = 0f;

    [Header("Temporary attributes (applied while active)")]
    public float addStrength = 0f;
    public float addIntelligence = 0f;
    public float addStaminaAttr = 0f;
    public float addAgility = 0f;

    // Passive effects are applied as toggle / infinite effects (duration < 0)
    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null) return;

        PlayerStatusEffects playerStatus = user.GetComponent<PlayerStatusEffects>();
        if (playerStatus != null)
        {
            PlayerStatusEffects.Effect effect = new PlayerStatusEffects.Effect(effectId, -1f)
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

            playerStatus.AddEffect(effect);
            return;
        }

        EnemyStatusEffects enemyStatus = user.GetComponent<EnemyStatusEffects>();
        if (enemyStatus != null)
        {
            EnemyStatusEffects.Effect effect = new EnemyStatusEffects.Effect(effectId, -1f)
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

            enemyStatus.AddEffect(effect);
            return;
        }

        Debug.LogWarning($"PassiveEffect '{displayName}' cannot be applied: no compatible status effects found on {user.name}");
    }

    public override void Remove(GameObject user)
    {
        if (user == null) return;

        PlayerStatusEffects playerStatus = user.GetComponent<PlayerStatusEffects>();
        if (playerStatus != null)
        {
            playerStatus.RemoveEffect(effectId);
            return;
        }

        EnemyStatusEffects enemyStatus = user.GetComponent<EnemyStatusEffects>();
        if (enemyStatus != null)
        {
            enemyStatus.RemoveEffect(effectId);
        }
    }
}
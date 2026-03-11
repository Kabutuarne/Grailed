using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/InstantEffect", fileName = "NewInstantEffect")]
public class InstantEffect : PlayerEffect
{
    [Header("Instant resources")]
    [Tooltip("Amount to heal (+) or damage (-). Applied immediately.")]
    public float healthAmount = 0f;

    [Tooltip("Amount to restore (+) or drain (-). Applied immediately.")]
    public float manaAmount = 0f;

    [Tooltip("Amount of energy / sprint to restore (+) or drain (-). Applied immediately.")]
    public float energyAmount = 0f;

    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null) return;

        PlayerStatusEffects playerStatus = user.GetComponent<PlayerStatusEffects>();
        if (playerStatus != null)
        {
            ApplyToPlayer(playerStatus, carrier);
            return;
        }

        EnemyStatusEffects enemyStatus = user.GetComponent<EnemyStatusEffects>();
        if (enemyStatus != null)
        {
            ApplyToEnemy(enemyStatus, carrier);
            return;
        }

        Debug.LogWarning($"InstantEffect '{displayName}' cannot be applied: no compatible status effects component found on {user.name}");
    }

    private void ApplyToPlayer(PlayerStatusEffects status, EffectCarrier carrier)
    {
        if (healthAmount != 0f)
        {
            var effect = new PlayerStatusEffects.Effect(effectId + "_health")
            {
                duration = 0f,
                carrier = carrier,
                healAmount = healthAmount
            };
            status.AddEffect(effect);
        }

        if (manaAmount != 0f)
        {
            var effect = new PlayerStatusEffects.Effect(effectId + "_mana")
            {
                duration = 0f,
                carrier = carrier,
                manaAmount = manaAmount
            };
            status.AddEffect(effect);
        }

        if (energyAmount != 0f)
        {
            var effect = new PlayerStatusEffects.Effect(effectId + "_energy")
            {
                duration = 0f,
                carrier = carrier,
                energyAmount = energyAmount
            };
            status.AddEffect(effect);
        }
    }

    private void ApplyToEnemy(EnemyStatusEffects status, EffectCarrier carrier)
    {
        if (healthAmount != 0f)
        {
            var effect = new EnemyStatusEffects.Effect(effectId + "_health")
            {
                duration = 0f,
                carrier = carrier,
                healAmount = healthAmount
            };
            status.AddEffect(effect);
        }

        if (manaAmount != 0f)
        {
            var effect = new EnemyStatusEffects.Effect(effectId + "_mana")
            {
                duration = 0f,
                carrier = carrier,
                manaAmount = manaAmount
            };
            status.AddEffect(effect);
        }

        if (energyAmount != 0f)
        {
            var effect = new EnemyStatusEffects.Effect(effectId + "_energy")
            {
                duration = 0f,
                carrier = carrier,
                energyAmount = energyAmount
            };
            status.AddEffect(effect);
        }
    }
}
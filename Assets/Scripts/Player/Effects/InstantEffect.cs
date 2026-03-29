using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/InstantEffect")]
public class InstantEffect : PlayerEffect
{
    public float healthAmount;
    public float manaAmount;
    public float energyAmount;

    public bool breaksCast;

    public override StatusEffectData CreateEffect(EffectCarrier carrier)
    {
        return new StatusEffectData(EffectTitle, 0f)
        {
            carrier = carrier,
            healAmount = healthAmount,
            manaAmount = manaAmount,
            energyAmount = energyAmount
        };
    }

    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        base.Apply(user, carrier);

        if (breaksCast)
        {
            var cast = user.GetComponent<PlayerCast>();
            if (cast != null)
                cast.OnDamageTaken();
        }
    }
}
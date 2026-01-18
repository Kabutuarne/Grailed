using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Effects/InstantEffect", fileName = "NewInstantEffect")]
public class InstantEffect : PlayerEffect
{
    [Header("Immediate changes")]
    public float healAmount = 0f;
    public float manaAmount = 0f;
    public float healPerSecond = 0f; // applied once as a single quick tick

    public override void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null) return;
        var status = user.GetComponent<PlayerStatusEffects>();
        if (status == null)
        {
            Debug.LogWarning($"InstantEffect '{displayName}' cannot be applied: PlayerStatusEffects component missing on {user.name}");
            return;
        }

        // Create a temporary Effect instance treated as instant
        var e = new PlayerStatusEffects.Effect(effectId + "_instant");
        e.duration = 0f; // instant
        e.carrier = carrier;
        e.healAmount = healAmount;
        e.manaAmount = manaAmount;
        e.healPerSecond = healPerSecond;

        status.AddEffect(e);
    }
}

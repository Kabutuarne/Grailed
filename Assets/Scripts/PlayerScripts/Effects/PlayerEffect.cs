using UnityEngine;

// Base class for configurable player-affecting ScriptableObjects
[CreateAssetMenu(menuName = "DungeonBroker/Effects/PlayerEffect", fileName = "NewPlayerEffect")]
public abstract class PlayerEffect : ScriptableObject
{
    [Header("Metadata")]
    public string effectId = "effect"; // used to identify/remove effects
    public string displayName = "Player Effect";

    // Apply the effect to the user GameObject. Implementations should call into
    // PlayerStatusEffects on the user and/or PlayerStats for instant effects.
    // Caller can provide an EffectCarrier (optional) so runtime Effect instances
    // know which carrier (item) produced them for UI display.
    public abstract void Apply(GameObject user, EffectCarrier carrier = null);

    // Remove applied effect (used for passive / toggle effects)
    public virtual void Remove(GameObject user) { }
}

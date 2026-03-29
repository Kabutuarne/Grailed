using UnityEngine;

public abstract class PlayerEffect : ScriptableObject
{
    [Header("Metadata")]
    public string effectId = "effect";

    public string EffectTitle => string.IsNullOrWhiteSpace(effectId) ? name : effectId;

    protected virtual void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(effectId))
            effectId = name;
    }

    //unified runtime creation
    public abstract StatusEffectData CreateEffect(EffectCarrier carrier);

    // Centralized apply logic (NO duplication i hope)
    public virtual void Apply(GameObject user, EffectCarrier carrier = null)
    {
        if (user == null)
            return;

        var status = user.GetComponent<StatusEffects>();
        if (status == null)
        {
            Debug.LogWarning($"Effect '{EffectTitle}' cannot be applied: no StatusEffects on {user.name}");
            return;
        }

        status.AddEffect(CreateEffect(carrier));
    }

    public virtual void Remove(GameObject user)
    {
        if (user == null)
            return;

        var status = user.GetComponent<StatusEffects>();
        if (status != null)
            status.RemoveEffect(EffectTitle);
    }
}
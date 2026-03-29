public interface IEffectTarget
{
    void ApplyEffect(StatusEffects effect);
    bool HasEffect(string effectId);
    float GetRemainingDuration(string effectId);
}
using UnityEngine;

public interface ISpellCastDefinition
{
    float CastTime { get; }
}

public interface IInstantCastSpell : ISpellCastDefinition
{
    bool TryCast(GameObject caster);
}

public interface IChanneledCastSpell : ISpellCastDefinition
{
    IChannelCastRuntime StartChannel(GameObject caster);
}
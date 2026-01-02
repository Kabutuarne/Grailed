using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/SpellEffect/AOEHeal", fileName = "NewAOEHealSpell")]
public class AOEHealSpellEffect : SpellEffect
{
    public float radius = 3f;
    public float duration = 6f;
    public float tickInterval = 1f;
    public float healPerTick = 5f;

    public override void Trigger(GameObject caster)
    {
        var go = new GameObject($"AOEHeal_{title}");
        go.transform.position = caster.transform.position + caster.transform.forward * 1f;
        var aoe = go.AddComponent<AOEHealBehaviour>();
        aoe.radius = radius;
        aoe.duration = duration;
        aoe.tickInterval = tickInterval;
        aoe.healPerTick = healPerTick;
        aoe.Initialize(caster);
    }
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/AOE", fileName = "NewAOESpell")]
public class AOESpell : ScriptableObject, IInstantCastSpell
{
    [Header("Casting")]
    public float castTime = 1f;
    public float manaCost = 0f;

    [Header("Area")]
    public float radius = 3f;

    [Header("Effect")]
    public EffectCarrier effectCarrier;

    [Header("Impact Behaviours")]
    public SpellImpactBehaviour[] impactBehaviours;

    [Header("Visuals")]
    public GameObject castingParticlePrefab;
    public Vector3 effectOffset = Vector3.zero;

    public float CastTime => castTime;

    public bool TryCast(GameObject caster)
    {
        return TriggerCast(caster);
    }

    // Triggers a single AOE cast. Returns true if mana was spent and effect applied.
    public bool TriggerCast(GameObject caster)
    {
        if (caster == null)
            return false;

        if (!ManaUtility.TrySpendMana(caster, manaCost, "aoe_spell_cost"))
            return false;

        Vector3 effectCenter = caster.transform.position + effectOffset;

        if (castingParticlePrefab != null)
            Object.Instantiate(castingParticlePrefab, effectCenter, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(effectCenter, radius);
        HashSet<GameObject> appliedTargets = new HashSet<GameObject>();

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            GameObject target = EffectCarrier.ResolveEffectTarget(hit.gameObject);
            if (target == null || !appliedTargets.Add(target))
                continue;

            if (effectCarrier != null)
                effectCarrier.Apply(target);
        }

        ApplyImpactBehaviours(caster, effectCenter, hits);
        return true;
    }

    private void ApplyImpactBehaviours(GameObject caster, Vector3 effectCenter, Collider[] hits)
    {
        if (impactBehaviours == null || impactBehaviours.Length == 0)
            return;

        foreach (SpellImpactBehaviour behaviour in impactBehaviours)
        {
            if (behaviour == null)
                continue;

            try
            {
                behaviour.Apply(caster, effectCenter, hits, radius);
            }
            catch { }
        }
    }
}
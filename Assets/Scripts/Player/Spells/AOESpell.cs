using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/AOE", fileName = "NewAOESpell")]
public class AOESpell : ScriptableObject
{
    [Header("Casting")]
    public float castTime = 1f;
    public float manaCost = 0f;

    [Header("Area")]
    public float radius = 3f;

    [Header("Effect")]
    public EffectCarrier effectCarrier; // contains multiple effects + UI info

    [Header("Visuals")]
    public GameObject castingParticlePrefab; // shown ONLY when cast completes
    public Vector3 effectOffset = Vector3.zero; // offset from player position for the visual/effect center

    // Triggers a single AOE cast. Returns true if mana was spent and effect applied.
    public bool TriggerCast(GameObject caster)
    {
        if (caster == null || effectCarrier == null) return false;
        if (!TrySpendMana(caster, manaCost))
            return false;

        Vector3 effectCenter = caster.transform.position + effectOffset;

        // Spawn the visual ONLY when the cast actually completes
        if (castingParticlePrefab != null)
            Object.Instantiate(castingParticlePrefab, effectCenter, Quaternion.identity);

        // Apply all effects to all entities within radius
        Collider[] hits = Physics.OverlapSphere(effectCenter, radius);
        foreach (var c in hits)
        {
            ApplyEffects(c.gameObject);
        }

        return true;
    }

    // Attempts to spend mana from the caster using available systems.
    // Returns false if an explicit check fails (e.g., TrySpendMana says no).
    private bool TrySpendMana(GameObject caster, float amount)
    {
        if (amount <= 0f) return true;

        var stats = caster.GetComponent<PlayerStats>();
        if (stats != null)
        {
            var mTry = stats.GetType().GetMethod("TrySpendMana");
            if (mTry != null)
            {
                try
                {
                    object res = mTry.Invoke(stats, new object[] { amount });
                    if (res is bool b) return b;
                }
                catch { }
            }

            var mSpend = stats.GetType().GetMethod("SpendMana");
            if (mSpend != null)
            {
                try { mSpend.Invoke(stats, new object[] { amount }); return true; } catch { }
            }
        }

        var status = caster.GetComponent<PlayerStatusEffects>();
        if (status != null)
        {
            try { status.AddManaEffect("aoe_spell_cost", -amount); return true; } catch { }
        }

        return true;
    }

    void ApplyEffects(GameObject target)
    {
        if (effectCarrier == null)
        {
            Debug.Log($"[AOESpell] No effect carrier, skipping effect application");
            return;
        }
        if (effectCarrier.effects == null || effectCarrier.effects.Length == 0)
        {
            Debug.Log($"[AOESpell] Effect carrier has no effects");
            return;
        }

        Debug.Log($"[AOESpell] Applying {effectCarrier.effects.Length} effects to {target.name}");
        foreach (var eff in effectCarrier.effects)
        {
            if (eff != null)
            {
                Debug.Log($"[AOESpell] Applying effect: {eff.displayName} to {target.name}");
                try { eff.Apply(target, effectCarrier); }
                catch (System.Exception ex) { Debug.LogError($"[AOESpell] Exception applying effect: {ex.Message}"); }
            }
        }
    }
}
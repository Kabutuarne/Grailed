using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/AOE", fileName = "NewAOESpell")]
public class AOESpell : ScriptableObject
{
    [Header("Casting")]
    public float castTime = 1f;
    public float manaCostPerTick = 0f;

    [Header("Area")]
    public float radius = 3f;

    [Header("Effect")]
    public PlayerEffect effect; // can be DurationEffect or InstantEffect

    [Header("Visuals")]
    public GameObject castingParticlePrefab; // shown at caster position for each tick
    public Vector3 effectOffset = Vector3.zero;      // offset from player position for the visual/effect center

    [Header("Status UI")]
    public string uiTitle = "Area Effect";
    public Sprite uiIcon;
    [TextArea]
    public string uiDescription;

    private EffectCarrier runtimeCarrier; // created at runtime to feed UI (icon/title/description)

    // Call when casting begins to show a persistent UI entry while the player is affected
    public void BeginCasting(GameObject caster)
    {
        if (caster == null) return;
        EnsureRuntimeCarrier();
        var status = caster.GetComponent<PlayerStatusEffects>();
        if (status == null) return;
        // Add a toggle effect purely for UI display
        var e = new PlayerStatusEffects.Effect(GetUIEffectId(), -1f);
        e.carrier = runtimeCarrier;
        status.AddEffect(e);
    }

    // Call when casting ends to clear the persistent UI entry
    public void EndCasting(GameObject caster)
    {
        if (caster == null) return;
        var status = caster.GetComponent<PlayerStatusEffects>();
        if (status == null) return;
        status.RemoveEffect(GetUIEffectId());
    }

    private void EnsureRuntimeCarrier()
    {
        if (runtimeCarrier == null)
        {
            runtimeCarrier = ScriptableObject.CreateInstance<EffectCarrier>();
            runtimeCarrier.title = string.IsNullOrEmpty(uiTitle) ? name : uiTitle;
            runtimeCarrier.icon = uiIcon;
            runtimeCarrier.description = uiDescription;
        }
    }

    private string GetUIEffectId()
    {
        return (effect != null ? effect.effectId : name) + "_aoe_ui";
    }

    // Triggers one AOE tick while holding cast. Returns true if mana was spent and effect applied.
    public bool TriggerTick(GameObject caster, EffectCarrier carrier = null)
    {
        if (caster == null || effect == null) return false;

        if (!TrySpendMana(caster, manaCostPerTick))
            return false;

        // Visual is managed persistently by PlayerCast; do not spawn per tick

        // Prepare a carrier so UI can show icon/title/description for the applied effect
        EnsureRuntimeCarrier();

        // Apply effect to all entities within radius
        Collider[] hits = Physics.OverlapSphere(caster.transform.position + effectOffset, radius);
        foreach (var c in hits)
        {
            try
            {
                effect.Apply(c.gameObject, runtimeCarrier);
            }
            catch
            {
                // If target doesn't support PlayerEffect, just skip
            }
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
            // Prefer explicit API if available
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

        // Fallback: use status effects to subtract mana instantly
        var status = caster.GetComponent<PlayerStatusEffects>();
        if (status != null)
        {
            try { status.AddManaEffect("aoe_spell_cost", -amount); return true; } catch { }
        }

        // If we can't determine, allow cast
        return true;
    }
}

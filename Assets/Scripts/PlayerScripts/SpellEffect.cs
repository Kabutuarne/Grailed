using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/SpellEffect", fileName = "NewSpellEffect")]
public class SpellEffect : ScriptableObject
{
    public enum TriggerMode
    {
        AfterCast,      // triggers once after the cast finishes
        WhileHolding    // triggers repeatedly while the Cast action is held (every castTime)
    }

    [Header("Meta")]
    public string title;
    [TextArea]
    public string description;

    [Header("Timing")]
    public float castTime = 1f;
    public TriggerMode triggerMode = TriggerMode.AfterCast;
    [Tooltip("Mana cost paid when the spell triggers (spent on cast completion)")]
    public float manaCost = 0f;

    [Header("Behaviour")]
    // A prefab that implements the visual / gameplay behaviour for this spell.
    // When triggered we instantiate it and (optionally) send an Initialize message.
    public GameObject effectPrefab;

    // Called by the casting system when the effect should actually run.
    public virtual void Trigger(GameObject caster)
    {
        if (effectPrefab != null)
        {
            var go = Instantiate(effectPrefab, caster.transform.position + caster.transform.forward * 1f, Quaternion.identity);
            go.SendMessage("Initialize", caster, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.Log($"[SpellEffect] Triggered '{title}' but no effectPrefab is assigned.");
        }
    }
}

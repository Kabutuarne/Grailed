using UnityEngine;

// EffectCarrier is a small ScriptableObject that groups PlayerEffect assets and provides
// display metadata (title, icon). One EffectCarrier maps to a single UI entry when active.

[CreateAssetMenu(menuName = "DungeonBroker/EffectCarrier", fileName = "NewEffectCarrier")]
public class EffectCarrier : ScriptableObject
{
    // NOTE: This carrier only holds contained PlayerEffect assets and display metadata.
    // All effect configuration must be done on the PlayerEffect assets themselves.

    [Header("Effects composed by this carrier")]
    public PlayerEffect[] effects;

    [Header("Display / metadata")]
    public string title = "Effect";
    public Sprite icon;
    [TextArea]
    public string description;

    // The time left to display for the carrier when active (the longest duration among contained DurationEffects)
    // Note: at author-time this is calculated from configured DurationEffects; runtime UI will read the active effect timer from PlayerStatusEffects
    public float GetLongestDuration()
    {
        float longest = 0f;
        if (effects == null) return longest;
        foreach (var eff in effects)
        {
            if (eff is DurationEffect d)
            {
                longest = Mathf.Max(longest, Mathf.Abs(d.duration));
            }
        }
        return longest;
    }

    // Apply all contained PlayerEffects to the target user. Pass self as the carrier so runtime Effect instances can reference metadata.
    public void Apply(GameObject user)
    {
        if (user == null) return;
        if (effects == null) return;

        foreach (var eff in effects)
        {
            if (eff == null) continue;
            eff.Apply(user, this);
        }
    }
}

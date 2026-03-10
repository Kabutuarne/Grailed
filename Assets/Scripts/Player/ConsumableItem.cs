using UnityEngine;

// EffectCarrier is a small ScriptableObject that groups PlayerEffect assets and provides
// display metadata (title, icon). One EffectCarrier maps to a single UI entry when active.
[CreateAssetMenu(menuName = "DungeonBroker/EffectCarrier", fileName = "NewEffectCarrier")]
public class EffectCarrier : ScriptableObject
{
    [Header("Effects composed by this carrier")]
    public PlayerEffect[] effects;

    [Header("Display / metadata")]
    public string title = "Effect";
    public Sprite icon;
    [TextArea]
    public string description;

    [Header("Visuals")]
    [Tooltip("Optional: Particle prefab to spawn on the player while this effect is active (duration effects only)")]
    public GameObject particlePrefab;

    public float GetLongestDuration()
    {
        float longest = 0f;
        if (effects == null) return longest;

        foreach (var effect in effects)
        {
            if (effect is DurationEffect durationEffect)
            {
                longest = Mathf.Max(longest, Mathf.Abs(durationEffect.duration));
            }
        }

        return longest;
    }

    public void Apply(GameObject user)
    {
        if (user == null || effects == null) return;

        foreach (var effect in effects)
        {
            if (effect == null) continue;
            effect.Apply(user, this);
        }
    }
}
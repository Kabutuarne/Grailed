using UnityEngine;

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
    [Tooltip("Vertical Y offset (local) applied when the particle prefab is spawned on the player.")]
    public float particleYOffset = -3.2f;

    [Header("Cast Interaction")]
    [Tooltip("If true, applying this carrier to the player will interrupt any active spell cast, regardless of individual effect settings.")]
    public bool breaksCast = false;

    public float GetLongestDuration()
    {
        float longest = 0f;

        if (effects == null)
            return longest;

        foreach (PlayerEffect effect in effects)
        {
            if (effect is DurationEffect durationEffect && durationEffect.duration > 0f)
                longest = Mathf.Max(longest, durationEffect.duration);
        }

        return longest;
    }

    public void Apply(GameObject user)
    {
        if (user == null || effects == null)
            return;

        foreach (PlayerEffect effect in effects)
        {
            if (effect == null)
                continue;

            effect.Apply(user, this);
        }

        if (breaksCast)
        {
            PlayerCast playerCast = user.GetComponent<PlayerCast>();
            if (playerCast != null)
                playerCast.OnDamageTaken();
        }
    }
}
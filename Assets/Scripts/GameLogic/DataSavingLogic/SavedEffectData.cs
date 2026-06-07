using System;

/// <summary>
/// Serializable snapshot of one active DurationEffect on the player.
/// InstantEffect entries are never saved (they are fire-and-forget).
/// Infinite effects (duration &lt; 0) are also excluded — they are managed
/// by other systems and should not be re-applied blindly on load.
/// GameSaveData holds a List of these.
/// </summary>
[Serializable]
public class SavedEffectData
{
    // ── Identity ──────────────────────────────────────────────────────────

    /// <summary>
    /// Matches PlayerEffect.effectId (set in the ScriptableObject inspector).
    /// Used as a human-readable key and passed as the id to StatusEffectData.
    /// </summary>
    public string effectId;

    /// <summary>
    /// The name() of the EffectCarrier ScriptableObject asset.
    /// Used to look up the carrier via ItemRegistry.GetCarrierByName() on load
    /// so visuals (particle prefab, icon, etc.) are restored correctly.
    /// May be empty if the effect has no carrier.
    /// </summary>
    public string carrierName;

    // ── Timer ─────────────────────────────────────────────────────────────

    /// <summary>Seconds remaining when the save was written.</summary>
    public float remainingTimer;

    /// <summary>Original full duration (needed to re-construct StatusEffectData).</summary>
    public float originalDuration;

    // ── Multipliers ───────────────────────────────────────────────────────

    public float speedMultiplier = 1f;
    public float healthRegenMultiplier = 1f;
    public float manaRegenMultiplier = 1f;
    public float energyRegenMultiplier = 1f;

    // ── Per-second resources ──────────────────────────────────────────────

    public float healthPerSecond;
    public float manaPerSecond;
    public float energyPerSecond;

    // ── Attribute modifiers ───────────────────────────────────────────────

    public float addStrength;
    public float addIntelligence;
    public float addStaminaAttr;
    public float addAgility;
}
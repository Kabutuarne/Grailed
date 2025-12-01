using UnityEngine;
using UnityEngine.InputSystem;

// Simple debug helper: attach to player and assign a ConsumableItem to test effects via Input System's "Consume" action
public class TestConsumableUser : MonoBehaviour
{
    public EffectCarrier effectCarrier;

    private PlayerInputActions input;

    void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        // When the Consume action is performed, use the assigned consumable
        input.Player.Consume.performed += OnConsumePerformed;
    }

    void OnDestroy()
    {
        if (input != null)
        {
            input.Player.Consume.performed -= OnConsumePerformed;
            input.Player.Disable();
            input = null;
        }
    }

    void TryUse()
    {
        if (effectCarrier == null) return;

        effectCarrier.Apply(gameObject);
        Debug.Log($"Used effect carrier: {effectCarrier.title}");

        // Print a quick diagnostic snapshot of player effective stats so we can confirm modifiers applied
        var stats = GetComponent<PlayerStats>();
        var effects = GetComponent<PlayerStatusEffects>();
        if (stats != null)
        {
            Debug.Log($"[Diagnostic] player health {stats.health:F1}/{stats.maxHealth:F1} | healthRegen {stats.healthRegenPerSecond:F3} | mana {stats.mana:F1}/{stats.maxMana:F1} | manaRegen {stats.manaRegenPerSecond:F3} | stamina {stats.stamina:F1}/{stats.maxStamina:F1}");
            Debug.Log($"[Diagnostic] effective attributes STR {stats.effectiveStrength} INT {stats.effectiveIntelligence} STA {stats.effectiveStaminaAttr} AGI {stats.effectiveAgility} | castTimeMult {stats.castTimeMultiplier} consumableMult {stats.consumableConsumptionMultiplier}");
        }
        if (effects != null)
        {
            Debug.Log($"[Diagnostic] active effects: {effects.activeEffects.Count}");
            Debug.Log($"[Diagnostic] multipliers => speed {effects.GetSpeedMultiplier():F3} | hRegen {effects.GetHealthRegenMultiplier():F3} | mRegen {effects.GetManaRegenMultiplier():F3}");
        }
    }

    void OnConsumePerformed(InputAction.CallbackContext ctx)
    {
        TryUse();
    }
}

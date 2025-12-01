using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Attributes (affect resources & multipliers)")]
    // Attributes (these are the RPG-style stat points you should tune in inspector)
    public float intelligence = 10f; // affects max mana & mana regen
    public float strength = 10f;     // affects max health & health regen
    public float staminaAttr = 10f;  // affects max stamina & stamina regen (rename to avoid conflict with resource)
    public float agility = 10f;      // affects cast time multiplier & consumable consumption multiplier

    [Header("Base / scaling settings (editable)")]
    [Tooltip("Base max health before Strength scaling")]
    public float baseMaxHealth = 50f;
    public float healthPerStrength = 5f;
    public float baseHealthRegen = 0.5f;
    public float healthRegenPerStrength = 0.05f;

    [Tooltip("Base max mana before Intelligence scaling")]
    public float baseMaxMana = 30f;
    public float manaPerIntelligence = 5f;
    public float baseManaRegen = 0.5f;
    public float manaRegenPerIntelligence = 0.05f;

    // Current resource values
    public float health;
    public float mana;

    [Header("Sprint / Stamina")]
    [Tooltip("Base max stamina before Stamina attribute scaling")]
    public float baseMaxStamina = 50f;
    public float staminaPerPoint = 5f;
    public float baseStaminaRegen = 1f;
    public float staminaRegenPerPoint = 0.2f;

    public float stamina; // current stamina resource

    [Header("Other (agility multipliers)")]
    [Tooltip("How much each point of Agility reduces cast time (fraction per point, e.g. 0.01 = 1% per point)")]
    public float agilityCastTimeReductionPerPoint = 0.01f;
    [Tooltip("How much each point of Agility reduces consumable consumption (fraction per point)")]
    public float agilityConsumeReductionPerPoint = 0.005f;

    PlayerStatusEffects statusEffects;

    void Start()
    {
        statusEffects = GetComponent<PlayerStatusEffects>();

        // Initialize current resources to the derived maximums
        health = maxHealth;
        mana = maxMana;
        stamina = maxStamina;
    }

    void Update()
    {
        // Passive regen tick per second for health & mana
        if (mana < maxMana)
            RestoreMana(manaRegenPerSecond * Time.deltaTime);

        if (health < maxHealth)
            Heal(healthRegenPerSecond * Time.deltaTime);
    }

    // -------- HEALTH --------
    public void TakeDamage(float amount)
    {
        health = Mathf.Clamp(health - amount, 0f, maxHealth);

        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        health = Mathf.Clamp(health + amount, 0f, maxHealth);
    }

    void Die()
    {
        Debug.Log("Player died. Congrats.");
    }

    // -------- MANA --------
    public bool TrySpendMana(float amount)
    {
        if (mana < amount)
            return false;

        mana -= amount;
        mana = Mathf.Clamp(mana, 0f, maxMana);
        return true;
    }

    public void RestoreMana(float amount)
    {
        mana = Mathf.Clamp(mana + amount, 0f, maxMana);
    }

    // -------- STAMINA / SPRINT --------
    public void ConsumeStamina(float amount)
    {
        stamina = Mathf.Clamp(stamina - amount, 0f, maxStamina);
    }

    public void RegenStamina(float amount)
    {
        stamina = Mathf.Clamp(stamina + amount, 0f, maxStamina);
    }

    // Convenience for UI
    // Exposed, computed max values & regen rates (names kept to maintain compatibility)
    // Effective attributes (base + temporary adds from status effects)
    public float effectiveStrength => strength + (statusEffects != null ? statusEffects.GetStrengthAdd() : 0f);
    public float effectiveIntelligence => intelligence + (statusEffects != null ? statusEffects.GetIntelligenceAdd() : 0f);
    public float effectiveStaminaAttr => staminaAttr + (statusEffects != null ? statusEffects.GetStaminaAttrAdd() : 0f);
    public float effectiveAgility => agility + (statusEffects != null ? statusEffects.GetAgilityAdd() : 0f);

    public float maxHealth => Mathf.Max(1f, baseMaxHealth + effectiveStrength * healthPerStrength);
    public float healthRegenPerSecond => Mathf.Max(0f, (baseHealthRegen + effectiveStrength * healthRegenPerStrength) * (statusEffects != null ? statusEffects.GetHealthRegenMultiplier() : 1f));

    public float maxMana => Mathf.Max(1f, baseMaxMana + effectiveIntelligence * manaPerIntelligence);
    public float manaRegenPerSecond => Mathf.Max(0f, (baseManaRegen + effectiveIntelligence * manaRegenPerIntelligence) * (statusEffects != null ? statusEffects.GetManaRegenMultiplier() : 1f));

    public float maxStamina => Mathf.Max(1f, baseMaxStamina + effectiveStaminaAttr * staminaPerPoint);
    public float staminaRegenPerSecond => Mathf.Max(0f, baseStaminaRegen + effectiveStaminaAttr * staminaRegenPerPoint);

    // Multipliers derived from agility
    // Cast time multiplier: multiply spell base cast time by this (<= 1.0 means faster)
    public float castTimeMultiplier => Mathf.Clamp(1f - effectiveAgility * agilityCastTimeReductionPerPoint, 0.25f, 1f);
    // Consumable consumption multiplier: multiply a consumable's resource cost by this (<= 1.0 means you consume less)
    public float consumableConsumptionMultiplier => Mathf.Clamp(1f - effectiveAgility * agilityConsumeReductionPerPoint, 0.1f, 1f);

    public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
    public float Mana01 => maxMana > 0f ? mana / maxMana : 0f;
    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
}

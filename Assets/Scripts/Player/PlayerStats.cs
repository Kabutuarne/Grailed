using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Attributes (affect resources & multipliers)")]
    // Attributes (these are the RPG-style stat points you should tune in inspector)
    public float intelligence = 10f; // affects max mana & mana regen
    public float strength = 10f;     // affects max health & health regen
    public float staminaAttr = 10f;  // affects movement speeds & energy resource (sprint)
    public float agility = 10f;      // affects cast speed & consumable speed

    [Header("Base / scaling settings (editable)")]
    [Tooltip("Base max health before Strength scaling")]
    public float baseMaxHealth = 100f;
    public float healthPerStrength = 5f;
    public float baseHealthRegen = 1.0f;
    public float healthRegenPerStrength = 0.05f;

    [Tooltip("Base max mana before Intelligence scaling")]
    public float baseMaxMana = 80f;
    public float manaPerIntelligence = 5f;
    public float baseManaRegen = 1.0f;
    public float manaRegenPerIntelligence = 0.05f;

    // Current resource values
    public float health;
    public float mana;

    [Header("Movement & Energy (Stamina-based)")]
    [Tooltip("Base walk speed before Stamina scaling")]
    public float baseWalkSpeed = 3.5f;
    [Tooltip("Base sprint speed before Stamina scaling")]
    public float baseSprintSpeed = 6.0f;

    [Tooltip("Base max energy before Stamina scaling")] // replaces previous stamina-based resource
    public float baseMaxEnergy = 100f;
    [Tooltip("Base energy regen per second before Stamina scaling")]
    public float baseEnergyRegen = 3f;

    public float stamina; // current energy resource (kept name for compatibility)

    [Header("Other (agility multipliers)")]
    [Tooltip("Base cast speed multiplier before Agility scaling (1 = baseline)")]
    public float baseCastSpeed = 1f;
    [Tooltip("Base consume speed multiplier before Agility scaling (1 = baseline)")]
    public float baseConsumeSpeed = 1f;

    PlayerStatusEffects statusEffects;
    PlayerInventory inventory;
    PlayerController controller;

    [Header("Death & Respawn")]
    public float respawnDelay = 3f;
    public Transform respawnPoint;
    bool isDead = false;

    void Start()
    {
        statusEffects = GetComponent<PlayerStatusEffects>();
        inventory = GetComponent<PlayerInventory>();
        controller = GetComponent<PlayerController>();

        // Initialize current resources to the derived maximums
        health = maxHealth;
        mana = maxMana;
        stamina = maxStamina;
    }

    void Update()
    {
        // If something externally set health to zero, trigger death
        if (!isDead && health <= 0f)
        {
            Die();
            return;
        }

        if (isDead) return; // stop passive updates while dead

        // Passive regen tick per second for health & mana
        if (mana < maxMana)
            RestoreMana(manaRegenPerSecond * Time.deltaTime);

        if (health < maxHealth)
            Heal(healthRegenPerSecond * Time.deltaTime);

        // Energy regen (sprint stamina)
        if (stamina < maxStamina)
            RegenStamina(staminaRegenPerSecond * Time.deltaTime);
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
        // If negative "healing" is used as damage elsewhere, ensure death triggers
        if (!isDead && health <= 0f)
            Die();
    }

    void Die()
    {
        Debug.Log("Player died. Congrats.");
        if (isDead) return;
        isDead = true;

        // Stop status effects influencing regen and clear any ongoing effects
        if (statusEffects != null)
        {
            try { statusEffects.ClearAllEffects(); } catch { }
            statusEffects.enabled = false;
        }

        // Drop all items
        if (inventory != null)
        {
            inventory.DropAllItems(controller != null && controller.playerCamera != null ? controller.playerCamera : transform);
        }

        // Schedule respawn
        StartCoroutine(RespawnCoroutine());
    }

    System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Move to respawn point if set
        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
        }

        // Clear any remaining effects and reset resources
        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
            statusEffects.enabled = true;
        }

        // Restore resources
        health = maxHealth;
        mana = maxMana;
        stamina = maxStamina;

        isDead = false;
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

    // Clamp all current resources to their respective maximums.
    // Useful after temporary max increases are removed (e.g., accessories/effects).
    public void ClampResourcesToMax()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        mana = Mathf.Clamp(mana, 0f, maxMana);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    // Convenience for UI
    // Exposed, computed max values & regen rates (names kept to maintain compatibility)
    // Effective attributes (base + temporary adds from status effects)
    public float effectiveStrength => strength + (statusEffects != null ? statusEffects.GetStrengthAdd() : 0f);
    public float effectiveIntelligence => intelligence + (statusEffects != null ? statusEffects.GetIntelligenceAdd() : 0f);
    public float effectiveStaminaAttr => staminaAttr + (statusEffects != null ? statusEffects.GetStaminaAttrAdd() : 0f);
    public float effectiveAgility => agility + (statusEffects != null ? statusEffects.GetAgilityAdd() : 0f);

    // New scaling formulas driven by attributes (divided by 10) times base values
    public float maxHealth => Mathf.Max(1f, (effectiveStrength / 10f) * baseMaxHealth);
    public float healthRegenPerSecond => Mathf.Max(0f, (effectiveStrength / 10f) * baseHealthRegen * (statusEffects != null ? statusEffects.GetHealthRegenMultiplier() : 1f));

    public float maxMana => Mathf.Max(1f, (effectiveIntelligence / 10f) * baseMaxMana);
    public float manaRegenPerSecond => Mathf.Max(0f, (effectiveIntelligence / 10f) * baseManaRegen * (statusEffects != null ? statusEffects.GetManaRegenMultiplier() : 1f));

    // Energy (stamina) resource
    public float maxStamina => Mathf.Max(1f, (effectiveStaminaAttr / 10f) * baseMaxEnergy);
    public float staminaRegenPerSecond => Mathf.Max(0f, (effectiveStaminaAttr / 10f) * baseEnergyRegen);

    // Movement speeds (apply external speed multipliers from status effects)
    public float walkSpeed => Mathf.Max(0f, (effectiveStaminaAttr / 10f) * baseWalkSpeed * (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));
    public float sprintSpeed => Mathf.Max(0f, (effectiveStaminaAttr / 10f) * baseSprintSpeed * (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));

    // Speed-based multipliers (greater -> faster). Use to divide times: time = baseTime / speed
    public float castSpeedMultiplier => Mathf.Max(0.01f, (effectiveAgility / 10f) * baseCastSpeed);
    public float consumeSpeedMultiplier => Mathf.Max(0.01f, (effectiveAgility / 10f) * baseConsumeSpeed);

    public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
    public float Mana01 => maxMana > 0f ? mana / maxMana : 0f;
    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
}

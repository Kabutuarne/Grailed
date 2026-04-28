using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Attributes (affect resources & multipliers)")]
    public float intelligence = 10f;
    public float strength = 10f;
    public float staminaAttr = 10f;
    public float agility = 10f;

    [Header("Health scaling")]
    [Tooltip("Base max health before Strength scaling")]
    public float baseMaxHealth = 100f;
    public float healthPerStrength = 5f;
    public float baseHealthRegen = 1.0f;
    public float healthRegenPerStrength = 0.05f;

    [Header("Mana scaling")]
    [Tooltip("Base max mana before Intelligence scaling")]
    public float baseMaxMana = 80f;
    public float manaPerIntelligence = 5f;
    public float baseManaRegen = 1.0f;
    public float manaRegenPerIntelligence = 0.05f;

    [Header("Current resources (runtime)")]
    public float health;
    public float mana;
    public float stamina;

    [Header("Movement & Energy (Stamina-based)")]
    [Tooltip("Base walk speed before Stamina scaling")]
    public float baseWalkSpeed = 3.5f;
    [Tooltip("Base sprint speed before Stamina scaling")]
    public float baseSprintSpeed = 6.0f;
    [Tooltip("Base max energy before Stamina scaling")]
    public float baseMaxEnergy = 100f;
    [Tooltip("Base energy regen per second before Stamina scaling")]
    public float baseEnergyRegen = 3f;

    [Header("Agility multipliers")]
    [Tooltip("Base cast speed multiplier before Agility scaling (1 = baseline)")]
    public float baseCastSpeed = 1f;
    [Tooltip("Base consume speed multiplier before Agility scaling (1 = baseline)")]
    public float baseConsumeSpeed = 1f;

    [Header("Death & Respawn")]
    public float respawnDelay = 3f;
    public Transform respawnPoint;

    // ── private refs ──────────────────────────────────────────────────────────
    private StatusEffects statusEffects;
    private PlayerInventory inventory;
    private PlayerController controller;
    private bool isDead;
    // previous max values used to preserve resource percentages when maxima change
    private float prevMaxHealth;
    private float prevMaxMana;
    private float prevMaxStamina;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        statusEffects = GetComponent<StatusEffects>();
        inventory = GetComponent<PlayerInventory>();
        controller = GetComponent<PlayerController>();

        // If there is an active save, apply the saved attribute values before
        // calculating derived maxima so health/mana/stamina are correct.
        var save = SaveSlotContext.LoadActiveSave();
        if (save != null && !save.isEmpty)
        {
            intelligence = save.intelligence;
            strength = save.strength;
            staminaAttr = save.staminaAttr;
            agility = save.agility;
        }

        health = maxHealth;
        mana = maxMana;
        stamina = maxStamina;

        prevMaxHealth = maxHealth;
        prevMaxMana = maxMana;
        prevMaxStamina = maxStamina;
    }

    void Update()
    {
        if (!isDead && health <= 0f)
        {
            Die();
            return;
        }

        if (isDead)
            return;

        // Passive regen
        if (mana < maxMana) RestoreMana(manaRegenPerSecond * Time.deltaTime);
        if (health < maxHealth) Heal(healthRegenPerSecond * Time.deltaTime);
        if (stamina < maxStamina) RestoreEnergy(staminaRegenPerSecond * Time.deltaTime);
    }

    // ── damage / heal ─────────────────────────────────────────────────────────

    public void TakeDamage(float amount)
    {
        health = Mathf.Clamp(health - amount, 0f, maxHealth);
        if (health <= 0f) Die();
    }

    public void Heal(float amount)
    {
        health = Mathf.Clamp(health + amount, 0f, maxHealth);
    }

    public void RestoreMana(float amount)
    {
        mana = Mathf.Clamp(mana + amount, 0f, maxMana);
    }

    public void RestoreEnergy(float amount)
    {
        stamina = Mathf.Clamp(stamina + amount, 0f, maxStamina);
    }

    public void ConsumeStamina(float amount)
    {
        stamina = Mathf.Clamp(stamina - amount, 0f, maxStamina);
    }

    // Alias kept for legacy call sites.
    public void RegenStamina(float amount) => RestoreEnergy(amount);

    public bool TrySpendMana(float amount)
    {
        if (mana < amount) return false;
        mana = Mathf.Clamp(mana - amount, 0f, maxMana);
        return true;
    }

    public void SpendMana(float amount) => TrySpendMana(amount);

    public void ClampResourcesToMax()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        mana = Mathf.Clamp(mana, 0f, maxMana);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    // Called by StatusEffects when effects are added/removed so derived maxima
    // can be recalculated while preserving current resource percentages.
    public void OnStatusEffectsChanged()
    {
        float newMaxHealth = maxHealth;
        float newMaxMana = maxMana;
        float newMaxStamina = maxStamina;

        if (prevMaxHealth > 0f)
        {
            float frac = health / prevMaxHealth;
            health = frac * newMaxHealth;
        }
        else
        {
            health = Mathf.Clamp(health, 0f, newMaxHealth);
        }

        if (prevMaxMana > 0f)
        {
            float frac = mana / prevMaxMana;
            mana = frac * newMaxMana;
        }
        else
        {
            mana = Mathf.Clamp(mana, 0f, newMaxMana);
        }

        if (prevMaxStamina > 0f)
        {
            float frac = stamina / prevMaxStamina;
            stamina = frac * newMaxStamina;
        }
        else
        {
            stamina = Mathf.Clamp(stamina, 0f, newMaxStamina);
        }

        ClampResourcesToMax();

        prevMaxHealth = newMaxHealth;
        prevMaxMana = newMaxMana;
        prevMaxStamina = newMaxStamina;
    }

    // ── death / respawn ───────────────────────────────────────────────────────

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Player died.");

        if (statusEffects != null)
        {
            try { statusEffects.ClearAllEffects(); } catch { }
            statusEffects.enabled = false;
        }

        Transform dropOrigin = (controller != null && controller.playerCamera != null)
            ? controller.playerCamera
            : transform;

        if (inventory != null)
            inventory.DropAllItems(dropOrigin);

        StartCoroutine(RespawnCoroutine());
    }

    System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Add 4 hours to the time
        if (LevelManager.Instance != null)
            LevelManager.Instance.AddTime(4f);

        // Teleport to respawn point
        if (LevelManager.Instance != null)
            LevelManager.Instance.TeleportPlayerToRespawn();
        else if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
        }

        // Clear status effects
        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
            statusEffects.enabled = true;
        }

        // Set stats to 10% of max
        health = maxHealth * 0.1f;
        mana = maxMana * 0.1f;
        stamina = maxStamina * 0.1f;
        isDead = false;
    }

    // ── effective attributes (base + status bonuses) ──────────────────────────

    public float effectiveStrength => strength + (statusEffects != null ? statusEffects.GetStrengthAdd() : 0f);
    public float effectiveIntelligence => intelligence + (statusEffects != null ? statusEffects.GetIntelligenceAdd() : 0f);
    public float effectiveStaminaAttr => staminaAttr + (statusEffects != null ? statusEffects.GetStaminaAdd() : 0f);
    public float effectiveAgility => agility + (statusEffects != null ? statusEffects.GetAgilityAdd() : 0f);

    // ── derived maximums ──────────────────────────────────────────────────────

    public float maxHealth => Mathf.Max(1f, (effectiveStrength / 10f) * baseMaxHealth);
    public float maxMana => Mathf.Max(1f, (effectiveIntelligence / 10f) * baseMaxMana);
    public float maxStamina => Mathf.Max(1f, (effectiveStaminaAttr / 10f) * baseMaxEnergy);

    // ── derived regen ─────────────────────────────────────────────────────────

    public float healthRegenPerSecond => Mathf.Max(0f,
        (effectiveStrength / 10f) * baseHealthRegen *
        (statusEffects != null ? statusEffects.GetHealthRegenMultiplier() : 1f));

    public float manaRegenPerSecond => Mathf.Max(0f,
        (effectiveIntelligence / 10f) * baseManaRegen *
        (statusEffects != null ? statusEffects.GetManaRegenMultiplier() : 1f));

    public float staminaRegenPerSecond => Mathf.Max(0f,
        (effectiveStaminaAttr / 10f) * baseEnergyRegen *
        (statusEffects != null ? statusEffects.GetEnergyRegenMultiplier() : 1f));

    // ── derived movement ──────────────────────────────────────────────────────

    public float walkSpeed => Mathf.Max(0f,
        (effectiveStaminaAttr / 10f) * baseWalkSpeed *
        (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));

    public float sprintSpeed => Mathf.Max(0f,
        (effectiveStaminaAttr / 10f) * baseSprintSpeed *
        (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));

    public float castSpeedMultiplier => Mathf.Max(0.01f, (effectiveAgility / 10f) * baseCastSpeed);
    public float consumeSpeedMultiplier => Mathf.Max(0.01f, (effectiveAgility / 10f) * baseConsumeSpeed);

    // ── normalised 0-1 helpers ────────────────────────────────────────────────

    public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
    public float Mana01 => maxMana > 0f ? mana / maxMana : 0f;
    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
}
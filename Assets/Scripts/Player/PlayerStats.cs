using UnityEngine;
using Sydewa;

public class PlayerStats : MonoBehaviour, IResourceHandler
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

    // private refs
    private StatusEffects statusEffects;
    private PlayerInventory inventory;
    private PlayerController controller;
    private bool isDead;
    private float prevMaxHealth;
    private float prevMaxMana;
    private float prevMaxStamina;

    // =====================================================================
    // Lifecycle
    // =====================================================================

    void Start()
    {
        statusEffects = GetComponent<StatusEffects>();
        inventory = GetComponent<PlayerInventory>();
        controller = GetComponent<PlayerController>();

        // Apply saved attributes BEFORE computing derived maxima so that
        // maxHealth / maxMana / maxStamina are correct from the very first frame.
        // CabinQuitSave also calls TryApplyToPlayer() for resources, but
        // attributes must be set here so the derived properties are right
        // when health/mana/stamina are initialised below.
        var gsm = GameSaveManager.Instance;
        if (gsm != null && gsm.ActiveSave != null && !gsm.ActiveSave.isEmpty)
        {
            intelligence = gsm.ActiveSave.intelligence;
            strength = gsm.ActiveSave.strength;
            staminaAttr = gsm.ActiveSave.staminaAttr;
            agility = gsm.ActiveSave.agility;
        }

        // Initialise resources to max. CabinQuitSave will overwrite these
        // with saved values (health >= 0) on the same frame via its own Start().
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

        if (mana < maxMana) ModifyMana(manaRegenPerSecond * Time.deltaTime);
        if (health < maxHealth) ModifyHealth(healthRegenPerSecond * Time.deltaTime);
        if (stamina < maxStamina) ModifyEnergy(staminaRegenPerSecond * Time.deltaTime);
    }

    // =====================================================================
    // IResourceHandler
    // =====================================================================

    public void ModifyHealth(float amount)
    {
        health = Mathf.Clamp(health + amount, 0f, maxHealth);
        if (health <= 0f && !isDead) Die();
    }

    public void ModifyMana(float amount)
    {
        mana = Mathf.Clamp(mana + amount, 0f, maxMana);
    }

    public void ModifyEnergy(float amount)
    {
        stamina = Mathf.Clamp(stamina + amount, 0f, maxStamina);
    }

    public void ClampResources()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        mana = Mathf.Clamp(mana, 0f, maxMana);
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    /// <summary>Attempts to spend mana. Returns true if sufficient mana was available.</summary>
    public bool TrySpendMana(float amount)
    {
        if (mana < amount) return false;
        ModifyMana(-amount);
        return true;
    }

    // Called by StatusEffects when effects are added/removed so derived maxima
    // can be recalculated while preserving current resource percentages.
    public void OnStatusEffectsChanged()
    {
        float newMaxHealth = maxHealth;
        float newMaxMana = maxMana;
        float newMaxStamina = maxStamina;

        if (prevMaxHealth > 0f)
            health = (health / prevMaxHealth) * newMaxHealth;
        else
            health = Mathf.Clamp(health, 0f, newMaxHealth);

        if (prevMaxMana > 0f)
            mana = (mana / prevMaxMana) * newMaxMana;
        else
            mana = Mathf.Clamp(mana, 0f, newMaxMana);

        if (prevMaxStamina > 0f)
            stamina = (stamina / prevMaxStamina) * newMaxStamina;
        else
            stamina = Mathf.Clamp(stamina, 0f, newMaxStamina);

        ClampResources();

        prevMaxHealth = newMaxHealth;
        prevMaxMana = newMaxMana;
        prevMaxStamina = newMaxStamina;
    }

    // =====================================================================
    // Death / respawn
    // =====================================================================

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

        GameObject lightingManager = GameObject.Find("GlobalIllumination");
        if (lightingManager != null)
            lightingManager.SetActive(true);

        StartCoroutine(RespawnCoroutine());
    }

    System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (LevelManager.Instance != null)
            LevelManager.Instance.AddTime(4f);

        if (LevelManager.Instance != null)
            LevelManager.Instance.TeleportPlayerToRespawn();
        else if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
        }

        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
            statusEffects.enabled = true;
        }

        health = maxHealth * 0.1f;
        mana = maxMana * 0.1f;
        stamina = maxStamina * 0.1f;
        isDead = false;
    }

    // =====================================================================
    // Effective attributes (base + status bonuses)
    // =====================================================================

    public float effectiveStrength => strength + (statusEffects != null ? statusEffects.GetStrengthAdd() : 0f);
    public float effectiveIntelligence => intelligence + (statusEffects != null ? statusEffects.GetIntelligenceAdd() : 0f);
    public float effectiveStaminaAttr => staminaAttr + (statusEffects != null ? statusEffects.GetStaminaAdd() : 0f);
    public float effectiveAgility => agility + (statusEffects != null ? statusEffects.GetAgilityAdd() : 0f);

    // =====================================================================
    // Derived maximums
    // =====================================================================

    public float maxHealth => Mathf.Max(1f, (effectiveStrength / 10f) * baseMaxHealth);
    public float maxMana => Mathf.Max(1f, (effectiveIntelligence / 10f) * baseMaxMana);
    public float maxStamina => Mathf.Max(1f, (effectiveStaminaAttr / 10f) * baseMaxEnergy);

    // =====================================================================
    // Derived regen
    // =====================================================================

    public float healthRegenPerSecond => Mathf.Max(0f,
        (effectiveStrength / 10f) * baseHealthRegen *
        (statusEffects != null ? statusEffects.GetHealthRegenMultiplier() : 1f));

    public float manaRegenPerSecond => Mathf.Max(0f,
        (effectiveIntelligence / 10f) * baseManaRegen *
        (statusEffects != null ? statusEffects.GetManaRegenMultiplier() : 1f));

    public float staminaRegenPerSecond => Mathf.Max(0f,
        (effectiveStaminaAttr / 10f) * baseEnergyRegen *
        (statusEffects != null ? statusEffects.GetEnergyRegenMultiplier() : 1f));

    // =====================================================================
    // Derived movement
    // =====================================================================

    public float walkSpeed => Mathf.Max(0f,
        (effectiveStaminaAttr / 10f) * baseWalkSpeed *
        (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));

    public float sprintSpeed => Mathf.Max(0f,
        (effectiveStaminaAttr / 10f) * baseSprintSpeed *
        (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));

    public float castSpeedMultiplier => Mathf.Max(0.01f, (effectiveAgility / 10f) * baseCastSpeed);
    public float consumeSpeedMultiplier => Mathf.Max(0.01f, (effectiveAgility / 10f) * baseConsumeSpeed);

    // =====================================================================
    // Normalised 0-1 helpers
    // =====================================================================

    public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
    public float Mana01 => maxMana > 0f ? mana / maxMana : 0f;
    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
}
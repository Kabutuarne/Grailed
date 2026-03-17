using UnityEngine;

[DisallowMultipleComponent]
public class EnemyStats : MonoBehaviour
{
    [Header("Core Attributes")]
    [Tooltip("Intelligence - Used for magical damage and mana calculations")]
    public float intelligence = 10f;

    [Tooltip("Strength - Used for health and health regeneration calculations")]
    public float strength = 10f;

    [Tooltip("Stamina - Used for movement speed and energy calculations")]
    public float stamina = 10f;

    [Tooltip("Agility - Used for attack speed calculations")]
    public float agility = 10f;

    [Header("Stat Randomization")]
    [Tooltip("Randomize attributes on Awake within the specified variance ranges")]
    public bool randomizeOnSpawn = false;

    [Min(0f)] public float intelligenceVariance = 2f;
    [Min(0f)] public float strengthVariance = 2f;
    [Min(0f)] public float staminaVariance = 2f;
    [Min(0f)] public float agilityVariance = 2f;

    [Header("Base Scaling Values")]
    [Tooltip("Base max health before strength scaling")]
    public float baseMaxHealth = 100f;
    [Tooltip("Base health regen per second before strength scaling")]
    public float baseHealthRegen = 0.5f;
    [Tooltip("Base max mana before intelligence scaling")]
    public float baseMaxMana = 20f;
    [Tooltip("Base mana regen per second before intelligence scaling")]
    public float baseManaRegen = 0f;
    [Tooltip("Base max energy before stamina scaling")]
    public float baseMaxEnergy = 100f;
    [Tooltip("Base energy regen per second before stamina scaling")]
    public float baseEnergyRegen = 5f;

    [Header("Current Resources (Auto-Managed)")]
    [Tooltip("Current health - managed automatically by status effects")]
    public float health;
    [Tooltip("Current mana - managed automatically by status effects")]
    public float mana;
    [Tooltip("Current energy - managed automatically by status effects")]
    public float energy;

    [Header("Death Drops")]
    [Tooltip("Prefabs to spawn at this position when the enemy dies")]
    public GameObject[] deathDropPrefabs;

    [Tooltip("Random scatter radius for dropped items")]
    public float dropScatterRadius = 1f;

    private EnemyStatusEffects statusEffects;

    // Effective attributes include base stats + status effect modifiers
    public float EffectiveIntelligence => Mathf.Max(0.1f, intelligence + GetStatusEffectAttributeAdd("intelligence"));
    public float EffectiveStrength => Mathf.Max(0.1f, strength + GetStatusEffectAttributeAdd("strength"));
    public float EffectiveStamina => Mathf.Max(0.1f, stamina + GetStatusEffectAttributeAdd("stamina"));
    public float EffectiveAgility => Mathf.Max(0.1f, agility + GetStatusEffectAttributeAdd("agility"));

    // Max resources calculated from attributes
    public float MaxHealth => Mathf.Max(1f, (EffectiveStrength / 10f) * baseMaxHealth);
    public float MaxMana => Mathf.Max(0f, (EffectiveIntelligence / 10f) * baseMaxMana);
    public float MaxEnergy => Mathf.Max(1f, (EffectiveStamina / 10f) * baseMaxEnergy);

    // Regen rates calculated from attributes and status effect multipliers
    public float HealthRegenPerSecond => Mathf.Max(0f, (EffectiveStrength / 10f) * baseHealthRegen * GetHealthRegenMultiplier());
    public float ManaRegenPerSecond => Mathf.Max(0f, (EffectiveIntelligence / 10f) * baseManaRegen * GetManaRegenMultiplier());
    public float EnergyRegenPerSecond => Mathf.Max(0f, (EffectiveStamina / 10f) * baseEnergyRegen * GetEnergyRegenMultiplier());

    // Normalized resource values (0-1)
    public float Health01 => MaxHealth > 0f ? health / MaxHealth : 0f;
    public float Mana01 => MaxMana > 0f ? mana / MaxMana : 0f;
    public float Energy01 => MaxEnergy > 0f ? energy / MaxEnergy : 0f;

    // Legacy property names (for compatibility)
    public float maxHealth => MaxHealth;
    public float maxMana => MaxMana;
    public float maxEnergy => MaxEnergy;

    private void Awake()
    {
        statusEffects = GetComponent<EnemyStatusEffects>();

        if (randomizeOnSpawn)
            RandomizeAttributes();

        // Initialize resources to max
        health = MaxHealth;
        mana = MaxMana;
        energy = MaxEnergy;
    }

    /// <summary>
    /// Randomizes all attributes within their variance ranges
    /// </summary>
    public void RandomizeAttributes()
    {
        intelligence = RollAttribute(intelligence, intelligenceVariance);
        strength = RollAttribute(strength, strengthVariance);
        stamina = RollAttribute(stamina, staminaVariance);
        agility = RollAttribute(agility, agilityVariance);
    }

    /// <summary>
    /// Spawns death drop prefabs at this enemy's position
    /// </summary>
    public void SpawnDeathDrops()
    {
        if (deathDropPrefabs == null || deathDropPrefabs.Length == 0)
            return;

        foreach (GameObject prefab in deathDropPrefabs)
        {
            if (prefab == null)
                continue;

            Vector3 dropPosition = transform.position;

            if (dropScatterRadius > 0f)
            {
                Vector2 randomCircle = Random.insideUnitCircle * dropScatterRadius;
                dropPosition += new Vector3(randomCircle.x, 0f, randomCircle.y);
            }

            Instantiate(prefab, dropPosition, Quaternion.identity);
        }
    }

    // ===== Resource Management Methods (Used by EnemyStatusEffects) =====

    public void Heal(float amount)
    {
        // if (amount <= 0f)
        //     return;
        health = Mathf.Clamp(health + amount, 0f, MaxHealth);
    }

    public void RestoreMana(float amount)
    {
        if (amount <= 0f)
            return;

        mana = Mathf.Clamp(mana + amount, 0f, MaxMana);
    }

    public void RestoreEnergy(float amount)
    {
        if (amount <= 0f)
            return;

        energy = Mathf.Clamp(energy + amount, 0f, MaxEnergy);
    }

    public bool TrySpendMana(float amount)
    {
        if (amount <= 0f)
            return true;

        if (mana < amount)
            return false;

        mana = Mathf.Clamp(mana - amount, 0f, MaxMana);
        return true;
    }

    public bool TrySpendEnergy(float amount)
    {
        if (amount <= 0f)
            return true;

        if (energy < amount)
            return false;

        energy = Mathf.Clamp(energy - amount, 0f, MaxEnergy);
        return true;
    }

    public void ClampResourcesToMax()
    {
        health = Mathf.Clamp(health, 0f, MaxHealth);
        mana = Mathf.Clamp(mana, 0f, MaxMana);
        energy = Mathf.Clamp(energy, 0f, MaxEnergy);
    }

    // ===== Private Helper Methods =====

    private float RollAttribute(float baseValue, float variance)
    {
        if (variance <= 0f)
            return baseValue;

        return Mathf.Max(0.1f, baseValue + Random.Range(-variance, variance));
    }

    private float GetStatusEffectAttributeAdd(string attributeName)
    {
        if (statusEffects == null)
            return 0f;

        return attributeName switch
        {
            "intelligence" => statusEffects.GetIntelligenceAdd(),
            "strength" => statusEffects.GetStrengthAdd(),
            "stamina" => statusEffects.GetStaminaAttrAdd(),
            "agility" => statusEffects.GetAgilityAdd(),
            _ => 0f
        };
    }

    private float GetHealthRegenMultiplier()
    {
        return statusEffects != null ? statusEffects.GetHealthRegenMultiplier() : 1f;
    }

    private float GetManaRegenMultiplier()
    {
        return statusEffects != null ? statusEffects.GetManaRegenMultiplier() : 1f;
    }

    private float GetEnergyRegenMultiplier()
    {
        return statusEffects != null ? statusEffects.GetEnergyRegenMultiplier() : 1f;
    }
}
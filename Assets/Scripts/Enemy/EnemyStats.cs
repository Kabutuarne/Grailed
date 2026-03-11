using UnityEngine;

[DisallowMultipleComponent]
public class EnemyStats : MonoBehaviour
{
    [Header("Attributes (same model as player)")]
    public float intelligence = 10f;
    public float strength = 10f;
    public float staminaAttr = 10f;
    public float agility = 10f;

    [Header("Base / scaling settings")]
    public float baseMaxHealth = 100f;
    public float baseHealthRegen = 0.5f;
    public float baseMaxMana = 20f;
    public float baseManaRegen = 0f;
    public float baseMaxEnergy = 100f;
    public float baseEnergyRegen = 5f;

    [Header("Movement")]
    public float baseWalkSpeed = 2.5f;
    public float baseSprintSpeed = 4f;

    [Header("Current resources")]
    public float health;
    public float mana;
    public float energy;

    [Header("Combat")]
    public float contactDamage = 10f;
    public float attackCooldown = 1f;
    public bool destroyOnDeath = true;
    public float destroyDelay = 0f;

    [Header("Optional death effects")]
    public EffectCarrier[] onDeathEffects;

    private EnemyStatusEffects statusEffects;
    private bool isDead;

    public bool IsDead => isDead;

    public float effectiveStrength => strength + (statusEffects != null ? statusEffects.GetStrengthAdd() : 0f);
    public float effectiveIntelligence => intelligence + (statusEffects != null ? statusEffects.GetIntelligenceAdd() : 0f);
    public float effectiveStaminaAttr => staminaAttr + (statusEffects != null ? statusEffects.GetStaminaAttrAdd() : 0f);
    public float effectiveAgility => agility + (statusEffects != null ? statusEffects.GetAgilityAdd() : 0f);

    public float maxHealth => Mathf.Max(1f, (effectiveStrength / 10f) * baseMaxHealth);
    public float healthRegenPerSecond => Mathf.Max(0f, (effectiveStrength / 10f) * baseHealthRegen * (statusEffects != null ? statusEffects.GetHealthRegenMultiplier() : 1f));
    public float maxMana => Mathf.Max(0f, (effectiveIntelligence / 10f) * baseMaxMana);
    public float manaRegenPerSecond => Mathf.Max(0f, (effectiveIntelligence / 10f) * baseManaRegen * (statusEffects != null ? statusEffects.GetManaRegenMultiplier() : 1f));
    public float maxEnergy => Mathf.Max(1f, (effectiveStaminaAttr / 10f) * baseMaxEnergy);
    public float energyRegenPerSecond => Mathf.Max(0f, (effectiveStaminaAttr / 10f) * baseEnergyRegen * (statusEffects != null ? statusEffects.GetEnergyRegenMultiplier() : 1f));
    public float walkSpeed => Mathf.Max(0f, (effectiveStaminaAttr / 10f) * baseWalkSpeed * (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));
    public float sprintSpeed => Mathf.Max(0f, (effectiveStaminaAttr / 10f) * baseSprintSpeed * (statusEffects != null ? statusEffects.GetSpeedMultiplier() : 1f));

    public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
    public float Mana01 => maxMana > 0f ? mana / maxMana : 0f;
    public float Energy01 => maxEnergy > 0f ? energy / maxEnergy : 0f;

    private void Awake()
    {
        statusEffects = GetComponent<EnemyStatusEffects>();
        health = maxHealth;
        mana = maxMana;
        energy = maxEnergy;
    }

    private void Update()
    {
        if (isDead)
            return;

        if (health <= 0f)
        {
            Die();
            return;
        }

        if (health < maxHealth)
            Heal(healthRegenPerSecond * Time.deltaTime);

        if (maxMana > 0f && mana < maxMana)
            RestoreMana(manaRegenPerSecond * Time.deltaTime);

        if (energy < maxEnergy)
            RestoreEnergy(energyRegenPerSecond * Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        health = Mathf.Clamp(health - amount, 0f, maxHealth);
        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead)
            return;

        health = Mathf.Clamp(health + amount, 0f, maxHealth);
        if (health <= 0f)
            Die();
    }

    public bool TrySpendMana(float amount)
    {
        if (mana < amount)
            return false;

        mana = Mathf.Clamp(mana - amount, 0f, maxMana);
        return true;
    }

    public void RestoreMana(float amount)
    {
        mana = Mathf.Clamp(mana + amount, 0f, maxMana);
    }

    public bool TrySpendEnergy(float amount)
    {
        if (energy < amount)
            return false;

        energy = Mathf.Clamp(energy - amount, 0f, maxEnergy);
        return true;
    }

    public void RestoreEnergy(float amount)
    {
        energy = Mathf.Clamp(energy + amount, 0f, maxEnergy);
    }

    public void ClampResourcesToMax()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        mana = Mathf.Clamp(mana, 0f, maxMana);
        energy = Mathf.Clamp(energy, 0f, maxEnergy);
    }

    public void Kill()
    {
        if (!isDead)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (statusEffects != null)
        {
            statusEffects.ClearAllEffects();
            statusEffects.enabled = false;
        }

        if (onDeathEffects != null)
        {
            foreach (EffectCarrier carrier in onDeathEffects)
            {
                if (carrier != null)
                    carrier.Apply(gameObject);
            }
        }

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}

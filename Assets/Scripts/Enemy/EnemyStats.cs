using UnityEngine;

[DisallowMultipleComponent]
public class EnemyStats : MonoBehaviour, IResourceHandler
{
    [Header("Attributes")]
    public float intelligence = 10f;
    public float strength = 10f;
    public float stamina = 10f;
    public float agility = 10f;

    [Header("Base Values")]
    public float baseMaxHealth = 100f;
    public float baseMaxMana = 20f;
    public float baseMaxEnergy = 100f;

    [Header("Regeneration")]
    public float baseHealthRegen = 0f;
    public float baseManaRegen = 0f;
    public float baseEnergyRegen = 0f;

    [Header("Current")]
    public float health;
    public float mana;
    public float energy;

    [Header("Drops")]
    public GameObject[] deathDropPrefabs;
    public float dropScatterRadius = 1f;

    private StatusEffects effects;
    private bool isDead;

    public float EffectiveStrength => strength + (effects?.GetStrengthAdd() ?? 0f);
    public float EffectiveStamina => stamina + (effects?.GetStaminaAdd() ?? 0f);
    public float EffectiveIntelligence => intelligence + (effects?.GetIntelligenceAdd() ?? 0f);
    public float EffectiveAgility => agility + (effects?.GetAgilityAdd() ?? 0f);

    public float MaxHealth => (EffectiveStrength / 10f) * baseMaxHealth;
    public float MaxMana => (EffectiveIntelligence / 10f) * baseMaxMana;
    public float MaxEnergy => (EffectiveStamina / 10f) * baseMaxEnergy;

    public float Health01 => MaxHealth > 0f ? health / MaxHealth : 0f;
    public bool IsDead => isDead;

    // Derived regeneration rates
    public float HealthRegenPerSecond => Mathf.Max(0f,
        (EffectiveStrength / 10f) * baseHealthRegen *
        (effects != null ? effects.GetHealthRegenMultiplier() : 1f));

    public float ManaRegenPerSecond => Mathf.Max(0f,
        (EffectiveIntelligence / 10f) * baseManaRegen *
        (effects != null ? effects.GetManaRegenMultiplier() : 1f));

    public float EnergyRegenPerSecond => Mathf.Max(0f,
        (EffectiveStamina / 10f) * baseEnergyRegen *
        (effects != null ? effects.GetEnergyRegenMultiplier() : 1f));

    private void Awake()
    {
        effects = GetComponent<StatusEffects>();

        health = MaxHealth;
        mana = MaxMana;
        energy = MaxEnergy;
    }

    private void Update()
    {
        if (isDead) return;

        // Check for death
        if (health <= 0f)
        {
            isDead = true;
            health = 0f;
            return;
        }

        // Passive regeneration based on effective stats
        if (health < MaxHealth) ModifyHealth(HealthRegenPerSecond * Time.deltaTime);
        if (mana < MaxMana) ModifyMana(ManaRegenPerSecond * Time.deltaTime);
        if (energy < MaxEnergy) ModifyEnergy(EnergyRegenPerSecond * Time.deltaTime);
    }

    // ── unified resource modification (IResourceHandler) ──────────────────────

    public void ModifyHealth(float amount)
    {
        if (isDead && amount <= 0f) return;
        health = Mathf.Clamp(health + amount, 0f, MaxHealth);
        if (health <= 0f && !isDead)
        {
            isDead = true;
            health = 0f;
        }
    }

    public void ModifyMana(float amount)
    {
        if (isDead) return;
        mana = Mathf.Clamp(mana + amount, 0f, MaxMana);
    }

    public void ModifyEnergy(float amount)
    {
        if (isDead) return;
        energy = Mathf.Clamp(energy + amount, 0f, MaxEnergy);
    }

    public void ClampResources()
    {
        health = Mathf.Clamp(health, 0f, MaxHealth);
        mana = Mathf.Clamp(mana, 0f, MaxMana);
        energy = Mathf.Clamp(energy, 0f, MaxEnergy);
    }

    // ── legacy aliases ────────────────────────────────────────────────────────

    public void TakeDamage(float amount) => ModifyHealth(-amount);
    public void Heal(float amount) => ModifyHealth(amount);
    public void RestoreMana(float amount) => ModifyMana(amount);
    public void RestoreEnergy(float amount) => ModifyEnergy(amount);

    public void SpawnDeathDrops()
    {
        for (int i = 0; i < deathDropPrefabs.Length; i++)
        {
            GameObject prefab = deathDropPrefabs[i];
            if (!prefab) continue;

            Vector3 pos = transform.position + (Vector3)(Random.insideUnitCircle * dropScatterRadius);
            Instantiate(prefab, pos, Quaternion.identity);
        }
    }
}
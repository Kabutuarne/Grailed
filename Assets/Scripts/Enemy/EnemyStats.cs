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

    [Header("Current")]
    public float health;
    public float mana;
    public float energy;

    [Header("Drops")]
    public GameObject[] deathDropPrefabs;
    public float dropScatterRadius = 1f;

    private StatusEffects effects;

    public float EffectiveStrength => strength + (effects?.GetStrengthAdd() ?? 0f);
    public float EffectiveStamina => stamina + (effects?.GetStaminaAdd() ?? 0f);
    public float EffectiveIntelligence => intelligence + (effects?.GetIntelligenceAdd() ?? 0f);
    public float EffectiveAgility => agility + (effects?.GetAgilityAdd() ?? 0f);

    public float MaxHealth => (EffectiveStrength / 10f) * baseMaxHealth;
    public float MaxMana => (EffectiveIntelligence / 10f) * baseMaxMana;
    public float MaxEnergy => (EffectiveStamina / 10f) * baseMaxEnergy;

    public float Health01 => health / MaxHealth;

    private void Awake()
    {
        effects = GetComponent<StatusEffects>();

        health = MaxHealth;
        mana = MaxMana;
        energy = MaxEnergy;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        health = Mathf.Clamp(health + amount, 0f, MaxHealth);
    }

    public void RestoreMana(float amount)
    {
        if (amount <= 0f) return;
        mana = Mathf.Clamp(mana + amount, 0f, MaxMana);
    }

    public void RestoreEnergy(float amount)
    {
        if (amount <= 0f) return;
        energy = Mathf.Clamp(energy + amount, 0f, MaxEnergy);
    }

    public void ClampResources()
    {
        health = Mathf.Clamp(health, 0f, MaxHealth);
        mana = Mathf.Clamp(mana, 0f, MaxMana);
        energy = Mathf.Clamp(energy, 0f, MaxEnergy);
    }

    public void SpawnDeathDrops()
    {
        foreach (var prefab in deathDropPrefabs)
        {
            if (!prefab) continue;

            Vector3 pos = transform.position + (Vector3)(Random.insideUnitCircle * dropScatterRadius);
            Instantiate(prefab, pos, Quaternion.identity);
        }
    }
}
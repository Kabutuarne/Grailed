using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float health;

    [Header("Mana")]
    public float maxMana = 100f;
    public float mana;

    [Header("Sprint / Stamina")]
    public float maxStamina = 100f;
    public float stamina;

    [Header("Other")]
    public float intelligence = 10f;

    void Start()
    {
        health = maxHealth;
        mana = maxMana;
        stamina = maxStamina;
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
    public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
    public float Mana01 => maxMana > 0f ? mana / maxMana : 0f;
    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
}

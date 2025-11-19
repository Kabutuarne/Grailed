using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public float maxHealth = 100;
    public float health;

    public float maxStamina = 100;
    public float stamina;

    public float intelligence = 10;

    void Start()
    {
        health = maxHealth;
        stamina = maxStamina;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0) Die();
    }

    public void Heal(float amount)
    {
        health = Mathf.Clamp(health + amount, 0, maxHealth);
    }

    void Die()
    {
        Debug.Log("Player died. Congrats.");
    }
}

using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Identity")]
    public string monsterName = "Enemy";

    [Header("Vitals")]
    public float maxHealth = 50f;
    public float health = 50f;

    [Header("Drops")]
    public GameObject dropOnDeathPrefab;
    [Range(0f, 1f)] public float dropChance = 0.25f;

    [Header("References (optional)")]
    public PlayerStatusEffects statusEffects; // optional reuse of player effect system
    [Tooltip("Drag the visual/model root here (optional). Used for rotating visuals or ragdoll control.")]
    public Transform modelRoot;

    [Tooltip("Rigidbodies that make up the ragdoll. Leave empty if not using ragdoll.")]
    public Rigidbody[] ragdollRigidbodies;

    [Tooltip("Colliders associated with the ragdoll rigidbodies. Leave empty to let Unity use child colliders automatically.")]
    public Collider[] ragdollColliders;

    [Tooltip("If true, enable ragdoll automatically when the enemy dies.")]
    public bool ragdollOnDeath = false;

    [HideInInspector] public bool isDead = false;

    void Start()
    {
        health = Mathf.Clamp(health <= 0f ? maxHealth : health, 0f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        health = Mathf.Clamp(health - Mathf.Max(0f, amount), 0f, maxHealth);
        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        health = Mathf.Clamp(health + Mathf.Max(0f, amount), 0f, maxHealth);
    }

    // Compatibility stub for systems that call this on living entities
    public void RestoreMana(float amount) { }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (statusEffects != null)
            statusEffects.enabled = false;

        if (ragdollOnDeath && ragdollRigidbodies != null && ragdollRigidbodies.Length > 0)
        {
            try { SetRagdoll(true); } catch { }
            if (dropOnDeathPrefab != null && Random.value <= dropChance)
            {
                try { Instantiate(dropOnDeathPrefab, transform.position, Quaternion.identity); } catch { }
            }
            Destroy(gameObject, 10f);
            return;
        }

        if (dropOnDeathPrefab != null && Random.value <= dropChance)
        {
            try { Instantiate(dropOnDeathPrefab, transform.position, Quaternion.identity); } catch { }
        }

        try
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders) c.enabled = false;
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = false;
        }
        catch { }

        Destroy(gameObject, 2f);
    }

    // Toggle ragdoll state for the assigned rigidbodies/colliders. When enabling ragdoll,
    // this will make rigidbodies non-kinematic and enable colliders; it will also
    // disable any Animator on the `modelRoot` so the physics can take over.
    public void SetRagdoll(bool enabled)
    {
        if (ragdollRigidbodies != null)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = !enabled;
            }
        }

        if (ragdollColliders != null)
        {
            foreach (var c in ragdollColliders)
            {
                if (c == null) continue;
                c.enabled = enabled;
            }
        }

        Animator anim = null;
        if (modelRoot != null) anim = modelRoot.GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = !enabled;
    }

    // Compatibility: clamp current resources to their max values
    public void ClampResourcesToMax()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
    }
}

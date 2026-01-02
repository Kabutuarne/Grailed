using System.Collections.Generic;
using UnityEngine;

public class AOEHealBehaviour : MonoBehaviour
{
    public float radius = 3f;
    public float tickInterval = 1f;
    public float duration = 6f;
    public float healPerTick = 5f;
    GameObject caster;
    float elapsed = 0f;
    float tickElapsed = 0f;

    public void Initialize(GameObject caster)
    {
        this.caster = caster;
        // optional visual setup
        Destroy(gameObject, duration);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        tickElapsed += Time.deltaTime;

        if (tickElapsed >= tickInterval)
        {
            tickElapsed = 0f;
            HealTick();
        }
    }

    void HealTick()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var c in hits)
        {
            var stats = c.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.Heal(healPerTick);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

using UnityEngine;

public class FireballBehaviour : MonoBehaviour
{
    public float speed = 8f;
    public float lifeTime = 5f;
    public float damage = 20f;
    public float aoeRadius = 0f;
    public GameObject groundFirePrefab;
    GameObject caster;

    public void Initialize(GameObject caster)
    {
        this.caster = caster;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == caster) return;

        Vector3 hitPos = transform.position;

        if (aoeRadius > 0f)
        {
            Collider[] hits = Physics.OverlapSphere(hitPos, aoeRadius);
            foreach (var c in hits)
            {
                if (c.gameObject == caster) continue;
                var s = c.GetComponent<PlayerStats>();
                if (s != null)
                {
                    s.TakeDamage(damage);
                }
            }
        }
        else
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(damage);
            }
        }

        if (groundFirePrefab != null)
        {
            Instantiate(groundFirePrefab, hitPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (aoeRadius > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
    }
}

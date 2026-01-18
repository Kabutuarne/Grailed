using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/Projectile", fileName = "NewProjectileSpell")]
public class ProjectileSpell : ScriptableObject
{
    [Header("Casting")]
    public float castTime = 1f;
    public float manaCost = 0f;

    [Header("Projectile")]
    public float speed = 8f;
    public float lifetime = 5f;
    public float impactRadius = 0f;
    public GameObject projectileModelPrefab; // optional visual model for projectile
    public float despawnDelay = 1.0f;         // how long the entity remains after impact

    [Header("Effect")]
    public PlayerEffect effect; // can be DurationEffect or InstantEffect

    [Header("Particles")]
    public GameObject castStartParticlePrefab;
    public GameObject flyingParticlePrefab;
    public GameObject hitParticlePrefab;

    [Header("Impact Behaviours")]
    public ProjectileImpactBehaviour[] impactBehaviours; // optional extra behaviours applied on impact

    // Triggers once after cast finishes. Returns true if spawned.
    public bool TriggerOnce(GameObject caster, EffectCarrier carrier = null)
    {
        if (caster == null || effect == null) return false;

        if (!TrySpendMana(caster, manaCost))
            return false;

        // Determine aim direction from camera if available
        Vector3 aimDir = caster.transform.forward;
        if (Camera.main != null)
            aimDir = Camera.main.transform.forward;

        Vector3 spawnPos = caster.transform.position + aimDir.normalized * 1f + Vector3.up * 0.5f;

        // Start particle
        if (castStartParticlePrefab != null)
            Object.Instantiate(castStartParticlePrefab, spawnPos, Quaternion.identity);

        // Build projectile GO
        GameObject proj = null;
        if (projectileModelPrefab != null)
        {
            proj = Object.Instantiate(projectileModelPrefab, spawnPos, Quaternion.LookRotation(aimDir));
        }
        else
        {
            proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proj.transform.position = spawnPos;
            proj.transform.rotation = Quaternion.LookRotation(aimDir);
            var col = proj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        var runtime = proj.AddComponent<ProjectileRuntime>();
        runtime.speed = speed;
        runtime.lifeTime = lifetime;
        runtime.impactRadius = impactRadius;
        runtime.effect = effect;
        runtime.flyingParticlePrefab = flyingParticlePrefab;
        runtime.hitParticlePrefab = hitParticlePrefab;
        runtime.caster = caster;
        runtime.carrier = carrier;
        runtime.despawnDelay = despawnDelay;
        runtime.parentSpell = this;

        proj.transform.forward = aimDir;
        return true;
    }

    private bool TrySpendMana(GameObject caster, float amount)
    {
        if (amount <= 0f) return true;

        var stats = caster.GetComponent<PlayerStats>();
        if (stats != null)
        {
            var mTry = stats.GetType().GetMethod("TrySpendMana");
            if (mTry != null)
            {
                try
                {
                    object res = mTry.Invoke(stats, new object[] { amount });
                    if (res is bool b) return b;
                }
                catch { }
            }
            var mSpend = stats.GetType().GetMethod("SpendMana");
            if (mSpend != null)
            {
                try { mSpend.Invoke(stats, new object[] { amount }); return true; } catch { }
            }
        }

        var status = caster.GetComponent<PlayerStatusEffects>();
        if (status != null)
        {
            try { status.AddManaEffect("projectile_spell_cost", -amount); return true; } catch { }
        }

        return true;
    }

    // Runtime behaviour nested in same file to avoid extra scripts
    public class ProjectileRuntime : MonoBehaviour
    {
        public float speed;
        public float lifeTime;
        public float impactRadius;
        public PlayerEffect effect;
        public GameObject flyingParticlePrefab;
        public GameObject hitParticlePrefab;
        public GameObject caster;
        public EffectCarrier carrier;
        public float despawnDelay = 1.0f;
        public ProjectileSpell parentSpell;

        GameObject flyingParticleInstance;
        bool hasHit = false;

        void Start()
        {
            if (lifeTime > 0f)
                Destroy(gameObject, lifeTime);

            if (flyingParticlePrefab != null)
            {
                flyingParticleInstance = Instantiate(flyingParticlePrefab, transform.position, transform.rotation, transform);
            }

            // Ensure we have a trigger collider and a Rigidbody so trigger events fire
            var colliders = GetComponentsInChildren<Collider>(true);
            bool hasAnyCollider = colliders != null && colliders.Length > 0;
            if (!hasAnyCollider)
            {
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.15f;
            }
            else
            {
                foreach (var col in colliders)
                    col.isTrigger = true;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void Update()
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            Vector3 hitPos = transform.position;

            // Gather hits for both area and single-target
            Collider[] hits;
            if (impactRadius > 0f)
            {
                hits = Physics.OverlapSphere(hitPos, impactRadius);
                var applied = new HashSet<GameObject>();
                foreach (var c in hits)
                {
                    var go = c.gameObject;
                    if (applied.Contains(go)) continue;
                    try { effect.Apply(go, carrier); } catch { }
                    applied.Add(go);
                }

                // Also apply effect to players within radius (CharacterController isn't a Collider)
                var players = Object.FindObjectsOfType<PlayerController>();
                foreach (var pc in players)
                {
                    if (pc == null) continue;
                    var go = pc.gameObject;
                    if (applied.Contains(go)) continue;
                    var cc = go.GetComponent<CharacterController>();
                    Vector3 center = cc != null ? cc.bounds.center : go.transform.position;
                    if (Vector3.Distance(center, hitPos) <= impactRadius)
                    {
                        try { effect.Apply(go, carrier); } catch { }
                        applied.Add(go);
                    }
                }
            }
            else
            {
                hits = new Collider[] { other };
                try { effect.Apply(other.gameObject, carrier); } catch { }
            }

            // Apply any extra impact behaviours
            if (parentSpell != null && parentSpell.impactBehaviours != null)
            {
                foreach (var beh in parentSpell.impactBehaviours)
                {
                    if (beh == null) continue;
                    try { beh.Apply(caster, hitPos, hits, impactRadius); } catch { }
                }
            }

            // Mark as hit, stop movement, disable visuals and collider
            hasHit = true;
            speed = 0f;
            if (flyingParticleInstance != null)
            {
                Destroy(flyingParticleInstance);
                flyingParticleInstance = null;
            }

            // Hide model visuals
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = false;

            // Disable colliders to prevent further triggers
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
                col.enabled = false;

            // Play hit particle
            if (hitParticlePrefab != null)
            {
                var hitFx = Object.Instantiate(hitParticlePrefab, hitPos, Quaternion.identity);
                hitFx.transform.SetParent(transform, true);
            }

            // Despawn after delay
            Destroy(gameObject, despawnDelay);
        }
    }
}

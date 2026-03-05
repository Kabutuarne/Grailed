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

    [Header("Spawn From Camera (Projectiles)")]
    [Tooltip("Offset in CAMERA LOCAL SPACE (x=right, y=up, z=forward). Use this to nudge the spawn away from the player.")]
    public Vector3 cameraLocalSpawnOffset = new Vector3(0.0f, -0.15f, 0.25f);

    [Tooltip("Extra push forward from camera to ensure we don't start inside any collider.")]
    public float cameraForwardSpawnPush = 0.35f;

    [Tooltip("If true, projectile ignores all colliders on the caster (recommended).")]
    public bool ignoreCasterColliders = true;

    [Tooltip("Optional: if enabled, raycast from camera to pick a point, then aim from spawnPos to that point (still 'where camera is facing', but avoids near-wall issues).")]
    public bool aimViaCameraRaycast = true;

    [Tooltip("Max distance for the camera raycast when aimViaCameraRaycast is on.")]
    public float aimRayDistance = 200f;

    [Tooltip("Layers considered for the aim raycast. Leave as Everything if unsure.")]
    public LayerMask aimRayMask = ~0;

    [Header("Effect")]
    public EffectCarrier effectCarrier; // contains multiple effects + UI info

    [Header("Particles")]
    public GameObject castStartParticlePrefab;
    public GameObject flyingParticlePrefab;
    public GameObject hitParticlePrefab;

    [Header("Impact Behaviours")]
    public ProjectileImpactBehaviour[] impactBehaviours; // optional extra behaviours applied on impact

    // Triggers once after cast finishes. Returns true if spawned.
    public bool TriggerOnce(GameObject caster)
    {
        if (caster == null) return false;
        // effectCarrier is optional - spells can use only ImpactBehaviours

        if (!TrySpendMana(caster, manaCost))
            return false;

        // Pick a camera: prefer Camera.main, otherwise try caster children.
        Camera cam = Camera.main;
        if (cam == null)
            cam = caster.GetComponentInChildren<Camera>(true);

        Transform aimT = cam != null ? cam.transform : caster.transform;

        // Direction is EXACTLY where camera faces
        Vector3 aimDir = aimT.forward.normalized;

        // Spawn from camera position with a CAMERA-LOCAL offset + forward push
        Vector3 spawnPos = aimT.position;
        spawnPos += aimT.TransformVector(cameraLocalSpawnOffset);
        spawnPos += aimDir * cameraForwardSpawnPush;

        // Optional: aim at what the camera is looking at (helps when camera is close to walls)
        if (aimViaCameraRaycast)
        {
            if (Physics.Raycast(aimT.position, aimDir, out RaycastHit hit, aimRayDistance, aimRayMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 toHit = (hit.point - spawnPos);
                if (toHit.sqrMagnitude > 0.0001f)
                    aimDir = toHit.normalized; // still driven by camera forward, but converges on hit point
            }
        }

        // Start particle
        if (castStartParticlePrefab != null)
            Object.Instantiate(castStartParticlePrefab, spawnPos, Quaternion.LookRotation(aimDir));

        // Build projectile GO
        GameObject proj;
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
        runtime.effectCarrier = effectCarrier;
        runtime.flyingParticlePrefab = flyingParticlePrefab;
        runtime.hitParticlePrefab = hitParticlePrefab;
        runtime.caster = caster;
        runtime.despawnDelay = despawnDelay;
        runtime.parentSpell = this;
        runtime.ignoreCasterColliders = false;

        Debug.Log($"[ProjectileSpell] Spawned projectile at {spawnPos} with direction {aimDir}. Effects: {(effectCarrier != null ? effectCarrier.effects.Length : 0)}. ImpactBehaviours: {(impactBehaviours != null ? impactBehaviours.Length : 0)}");
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
        public EffectCarrier effectCarrier;
        public GameObject flyingParticlePrefab;
        public GameObject hitParticlePrefab;
        public GameObject caster;
        public float despawnDelay = 1.0f;
        public ProjectileSpell parentSpell;

        [HideInInspector] public bool ignoreCasterColliders = false;

        GameObject flyingParticleInstance;
        bool hasHit = false;

        void Start()
        {
            Debug.Log($"[ProjectileRuntime] Starting projectile setup. Speed: {speed}, Lifetime: {lifeTime}, ImpactRadius: {impactRadius}");

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
                Debug.Log($"[ProjectileRuntime] No collider found, adding SphereCollider");
                var sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.15f;
                colliders = new Collider[] { sc };
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

            // Only ignore caster collisions if the flag is set
            if (ignoreCasterColliders && caster != null)
            {
                var casterCols = caster.GetComponentsInChildren<Collider>(true);
                if (casterCols != null && casterCols.Length > 0 && colliders != null)
                {
                    foreach (var pc in colliders)
                    {
                        if (pc == null) continue;
                        foreach (var cc in casterCols)
                        {
                            if (cc == null) continue;
                            Physics.IgnoreCollision(pc, cc, true);
                        }
                    }
                }
            }

            Debug.Log($"[ProjectileRuntime] Projectile setup complete. Colliders: {colliders.Length}, Rigidbody kinematic: {GetComponent<Rigidbody>().isKinematic}");
        }

        void Update()
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;
            if (other == null) return;

            Debug.Log($"[Projectile] Hit detected: {other.name}");


            // Extra safety: never hit the caster or its children - this is fucking stupid
            // if (caster != null)
            // {
            //     if (other.gameObject == caster)
            //     {
            //         // DO NOTHING
            //     }
            //     if (other.transform.IsChildOf(caster.transform))
            //     {
            //         Debug.Log($"[Projectile] Ignoring hit on caster child");
            //         return;
            //     }
            // }

            Vector3 hitPos = transform.position;

            // Gather hits for both area and single-target
            Collider[] hits;
            var applied = new HashSet<GameObject>();
            if (impactRadius > 0f)
            {
                hits = Physics.OverlapSphere(hitPos, impactRadius);
                foreach (var c in hits)
                {
                    // if (c == null) continue;
                    // // Skip caster in AOE too - also fucking stupid
                    // if (caster != null && (c.gameObject == caster || c.transform.IsChildOf(caster.transform)))
                    //     continue;
                    var go = c.gameObject;
                    if (applied.Contains(go)) continue;
                    if (go.CompareTag("Player") || go.CompareTag("Enemy"))
                    {
                        ApplyEffects(go);
                        applied.Add(go);
                    }
                }

                // Also apply effect to players within radius (CharacterController isn't a Collider)
                var players = Object.FindObjectsOfType<PlayerController>();
                foreach (var pc in players)
                {
                    if (pc == null) continue;
                    var go = pc.gameObject;
                    // if (caster != null && (go == caster || go.transform.IsChildOf(caster.transform)))
                    //     continue;
                    if (applied.Contains(go)) continue;
                    var cc = go.GetComponent<CharacterController>();
                    Vector3 center = cc != null ? cc.bounds.center : go.transform.position;
                    if (Vector3.Distance(center, hitPos) <= impactRadius)
                    {
                        if (go.CompareTag("Player") || go.CompareTag("Enemy"))
                        {
                            ApplyEffects(go);
                            applied.Add(go);
                        }
                    }
                }
            }
            else
            {
                hits = new Collider[] { other };
                var go = other.gameObject;
                if (go.CompareTag("Player") || go.CompareTag("Enemy"))
                {
                    ApplyEffects(go);
                    applied.Add(go);
                }
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
            var cols2 = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols2)
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

        void ApplyEffects(GameObject target)
        {
            if (effectCarrier == null)
            {
                Debug.Log($"[Projectile] No effect carrier on spell, skipping effect application");
                return;
            }
            if (effectCarrier.effects == null || effectCarrier.effects.Length == 0)
            {
                Debug.Log($"[Projectile] Effect carrier has no effects");
                return;
            }

            Debug.Log($"[Projectile] Applying {effectCarrier.effects.Length} effects to {target.name}");
            foreach (var eff in effectCarrier.effects)
            {
                if (eff != null)
                {
                    Debug.Log($"[Projectile] Applying effect: {eff.displayName} to {target.name}");
                    try { eff.Apply(target, effectCarrier); } catch (System.Exception ex) { Debug.LogError($"[Projectile] Exception applying effect: {ex.Message}"); }
                }
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/Projectile", fileName = "NewProjectileSpell")]
public class ProjectileSpell : ScriptableObject, IInstantCastSpell
{
    [Header("Casting")]
    public float castTime = 1f;
    public float manaCost = 0f;

    [Header("Projectile")]
    public float speed = 8f;
    public float lifetime = 5f;
    public float impactRadius = 0f;
    public GameObject projectileModelPrefab;
    public float despawnDelay = 1.0f;

    [Header("Spawn From Camera (Projectiles)")]
    [Tooltip("Offset in CAMERA LOCAL SPACE (x=right, y=up, z=forward).")]
    public Vector3 cameraLocalSpawnOffset = new Vector3(0.0f, -0.15f, 0.25f);

    [Tooltip("Extra push forward from camera to ensure we don't start inside any collider.")]
    public float cameraForwardSpawnPush = 0.35f;

    [Tooltip("If true, projectile ignores all colliders on the caster.")]
    public bool ignoreCasterColliders = true;

    [Tooltip("If enabled, raycast from camera to pick a point, then aim from spawnPos to that point.")]
    public bool aimViaCameraRaycast = true;

    [Tooltip("Max distance for the camera raycast when aimViaCameraRaycast is on.")]
    public float aimRayDistance = 200f;

    [Tooltip("Layers considered for the aim raycast.")]
    public LayerMask aimRayMask = ~0;

    [Header("Effect")]
    public EffectCarrier effectCarrier;

    [Header("Particles")]
    public GameObject castStartParticlePrefab;
    public GameObject flyingParticlePrefab;
    public GameObject hitParticlePrefab;

    [Header("Impact Behaviours")]
    public ProjectileImpactBehaviour[] impactBehaviours;

    public float CastTime => castTime;

    public bool TryCast(GameObject caster)
    {
        return TriggerOnce(caster);
    }

    public bool TriggerOnce(GameObject caster)
    {
        if (caster == null)
            return false;

        if (!ManaUtility.TrySpendMana(caster, manaCost, "projectile_spell_cost"))
            return false;

        SpellAimUtility.GetAimData(
            caster,
            cameraLocalSpawnOffset,
            cameraForwardSpawnPush,
            aimViaCameraRaycast,
            aimRayDistance,
            aimRayMask,
            out _,
            out Vector3 spawnPos,
            out Vector3 aimDir,
            out _,
            out _,
            out _);

        if (castStartParticlePrefab != null)
            Object.Instantiate(castStartParticlePrefab, spawnPos, Quaternion.LookRotation(aimDir));

        GameObject projectile = projectileModelPrefab != null
            ? Object.Instantiate(projectileModelPrefab, spawnPos, Quaternion.LookRotation(aimDir))
            : CreateFallbackProjectile(spawnPos, aimDir);

        ProjectileRuntime runtime = projectile.AddComponent<ProjectileRuntime>();
        runtime.speed = speed;
        runtime.lifeTime = lifetime;
        runtime.impactRadius = impactRadius;
        runtime.effectCarrier = effectCarrier;
        runtime.flyingParticlePrefab = flyingParticlePrefab;
        runtime.hitParticlePrefab = hitParticlePrefab;
        runtime.caster = caster;
        runtime.despawnDelay = despawnDelay;
        runtime.parentSpell = this;
        runtime.ignoreCasterColliders = ignoreCasterColliders;

        projectile.transform.forward = aimDir;
        return true;
    }

    private GameObject CreateFallbackProjectile(Vector3 spawnPos, Vector3 aimDir)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.transform.position = spawnPos;
        projectile.transform.rotation = Quaternion.LookRotation(aimDir);

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;

        return projectile;
    }

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
        public bool ignoreCasterColliders = false;

        private GameObject flyingParticleInstance;
        private bool hasHit;

        void Start()
        {
            if (lifeTime > 0f)
                Destroy(gameObject, lifeTime);

            if (flyingParticlePrefab != null)
                flyingParticleInstance = Instantiate(flyingParticlePrefab, transform.position, transform.rotation, transform);

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            if (colliders == null || colliders.Length == 0)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.15f;
                colliders = new Collider[] { sphere };
            }
            else
            {
                foreach (Collider collider in colliders)
                    collider.isTrigger = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (ignoreCasterColliders && caster != null)
            {
                Collider[] casterColliders = caster.GetComponentsInChildren<Collider>(true);
                foreach (Collider projectileCollider in colliders)
                {
                    if (projectileCollider == null)
                        continue;

                    foreach (Collider casterCollider in casterColliders)
                    {
                        if (casterCollider == null)
                            continue;

                        Physics.IgnoreCollision(projectileCollider, casterCollider, true);
                    }
                }
            }
        }

        void Update()
        {
            if (!hasHit)
                transform.position += transform.forward * speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (hasHit || other == null)
                return;

            Vector3 hitPos = transform.position;
            Collider[] hits = impactRadius > 0f
                ? Physics.OverlapSphere(hitPos, impactRadius)
                : new Collider[] { other };

            ApplyEffectCarrierToHits(hitPos, hits);
            ApplyImpactBehaviours(hitPos, hits);

            hasHit = true;
            speed = 0f;

            if (flyingParticleInstance != null)
            {
                Destroy(flyingParticleInstance);
                flyingParticleInstance = null;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                renderer.enabled = false;

            Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in ownColliders)
                collider.enabled = false;

            if (hitParticlePrefab != null)
            {
                GameObject hitFx = Instantiate(hitParticlePrefab, hitPos, Quaternion.identity);
                hitFx.transform.SetParent(transform, true);
            }

            Destroy(gameObject, despawnDelay);
        }

        private void ApplyEffectCarrierToHits(Vector3 hitPos, Collider[] hits)
        {
            if (effectCarrier == null)
                return;

            HashSet<GameObject> applied = new HashSet<GameObject>();

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                GameObject target = hit.gameObject;
                if (!applied.Add(target))
                    continue;

                if (target.CompareTag("Player") || target.CompareTag("Enemy"))
                    effectCarrier.Apply(target);
            }

            if (impactRadius > 0f)
            {
                PlayerController[] players = Object.FindObjectsOfType<PlayerController>();
                foreach (PlayerController player in players)
                {
                    if (player == null)
                        continue;

                    GameObject target = player.gameObject;
                    if (!applied.Add(target))
                        continue;

                    CharacterController cc = target.GetComponent<CharacterController>();
                    Vector3 center = cc != null ? cc.bounds.center : target.transform.position;

                    if (Vector3.Distance(center, hitPos) <= impactRadius &&
                        (target.CompareTag("Player") || target.CompareTag("Enemy")))
                    {
                        effectCarrier.Apply(target);
                    }
                }
            }
        }

        private void ApplyImpactBehaviours(Vector3 hitPos, Collider[] hits)
        {
            if (parentSpell == null || parentSpell.impactBehaviours == null)
                return;

            foreach (ProjectileImpactBehaviour behaviour in parentSpell.impactBehaviours)
            {
                if (behaviour == null)
                    continue;

                try
                {
                    behaviour.Apply(caster, hitPos, hits, impactRadius);
                }
                catch { }
            }
        }
    }
}
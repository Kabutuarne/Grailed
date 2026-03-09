using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/ChanneledProjectile", fileName = "NewChanneledProjectileSpell")]
public class ChanneledProjectileSpell : ScriptableObject
{
    [Header("Casting")]
    public float castTime = 1f; // Time before channel starts
    public float channelManaCostPerSecond = 1f;

    [Header("Laser")]
    public GameObject startEffectPrefab; // Played once at cast start
    public GameObject laserEffectPrefab; // Looped laser/beam effect
    public GameObject hitEffectPrefab;   // Looped at hit point
    public PassiveEffect passiveEffect;  // Effect to apply to hit entity
    public Vector3 cameraLocalSpawnOffset = new Vector3(0.0f, -0.15f, 0.25f);
    public float cameraForwardSpawnPush = 0.35f;
    public bool aimViaCameraRaycast = true;
    public float aimRayDistance = 200f;
    public LayerMask aimRayMask = ~0;

    // Called to start the cast (returns runtime handler)
    public ChanneledProjectileRuntime StartCast(GameObject caster)
    {
        if (caster == null) return null;
        var runtime = caster.AddComponent<ChanneledProjectileRuntime>();
        runtime.Init(this, caster);
        return runtime;
    }

    // Runtime behaviour nested in same file to avoid extra scripts
    public class ChanneledProjectileRuntime : MonoBehaviour
    {
        private ChanneledProjectileSpell spell;
        private GameObject caster;

        GameObject startEffectInstance;
        GameObject laserEffectInstance;
        GameObject hitEffectInstance;
        GameObject hitTarget;

        float elapsed = 0f;
        bool channeling = false;
        bool shouldChannel = true;
        bool effectApplied = false;

        public void Init(ChanneledProjectileSpell spell, GameObject caster)
        {
            this.spell = spell;
            this.caster = caster;
            elapsed = 0f;
            channeling = false;
            shouldChannel = true;
            effectApplied = false;
            // Play start effect at caster spawn offset (camera-local offset is applied later when channel begins)
            if (spell.startEffectPrefab)
                startEffectInstance = Instantiate(spell.startEffectPrefab, caster.transform.position + spell.cameraLocalSpawnOffset, Quaternion.identity, null);
        }

        void Update()
        {
            if (!channeling)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= spell.castTime && shouldChannel)
                {
                    StartChannel();
                }
            }
            else
            {
                // During channel: ensure external conditions hold via PlayerCast; but still check mana here
                if (!shouldChannel)
                {
                    StopChannel();
                    return;
                }

                if (!TrySpendMana(caster, spell.channelManaCostPerSecond * Time.deltaTime))
                {
                    StopChannel();
                    return;
                }

                // Camera and aiming logic (same as ProjectileSpell)
                Camera cam = Camera.main;
                if (cam == null)
                    cam = caster.GetComponentInChildren<Camera>(true);
                Transform aimT = cam != null ? cam.transform : caster.transform;
                Vector3 aimDir = aimT.forward.normalized;
                Vector3 origin = aimT.position;
                origin += aimT.TransformVector(spell.cameraLocalSpawnOffset);
                origin += aimDir * spell.cameraForwardSpawnPush;

                // Optional: aim at what the camera is looking at (helps when camera is close to walls)
                Vector3 hitPoint = origin + aimDir * spell.aimRayDistance;
                if (spell.aimViaCameraRaycast)
                {
                    if (Physics.Raycast(aimT.position, aimDir, out RaycastHit hit, spell.aimRayDistance, spell.aimRayMask, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 toHit = (hit.point - origin);
                        if (toHit.sqrMagnitude > 0.0001f)
                            aimDir = toHit.normalized;
                        hitPoint = hit.point;

                        if (!effectApplied && spell.passiveEffect != null)
                        {
                            try { spell.passiveEffect.Apply(hit.collider.gameObject); } catch { }
                            effectApplied = true;
                            hitTarget = hit.collider.gameObject;
                        }
                    }
                    else
                    {
                        hitTarget = null;
                    }
                }

                // Position laser at camera origin and rotation
                if (laserEffectInstance)
                {
                    laserEffectInstance.transform.position = origin;
                    laserEffectInstance.transform.rotation = Quaternion.LookRotation(aimDir);
                }

                // Position hit effect at hit point
                if (spell.hitEffectPrefab != null)
                {
                    if (hitEffectInstance == null)
                        hitEffectInstance = Instantiate(spell.hitEffectPrefab, hitPoint, Quaternion.identity, null);
                    hitEffectInstance.transform.position = hitPoint;
                }
                else if (hitEffectInstance != null)
                {
                    Destroy(hitEffectInstance);
                    hitEffectInstance = null;
                }
            }
        }

        void StartChannel()
        {
            channeling = true;

            Camera cam = Camera.main;
            if (cam == null)
                cam = caster.GetComponentInChildren<Camera>(true);
            Transform aimT = cam != null ? cam.transform : caster.transform;
            Vector3 aimDir = aimT.forward.normalized;
            Vector3 origin = aimT.position;
            origin += aimT.TransformVector(spell.cameraLocalSpawnOffset);
            origin += aimDir * spell.cameraForwardSpawnPush;

            // Instantiate laser and hit effect immediately
            if (spell.laserEffectPrefab)
                laserEffectInstance = Instantiate(spell.laserEffectPrefab, origin, Quaternion.LookRotation(aimDir), null);

            if (spell.hitEffectPrefab != null && hitEffectInstance == null)
            {
                Vector3 hitPoint = origin + aimDir * spell.aimRayDistance;
                if (spell.aimViaCameraRaycast)
                {
                    if (Physics.Raycast(aimT.position, aimDir, out RaycastHit hit, spell.aimRayDistance, spell.aimRayMask, QueryTriggerInteraction.Ignore))
                        hitPoint = hit.point;
                }
                hitEffectInstance = Instantiate(spell.hitEffectPrefab, hitPoint, Quaternion.identity, null);
            }
        }

        // Called externally to stop channeling (e.g. on cast release, move, or mana fail)
        public void StopChannel()
        {
            shouldChannel = false;
            StopCasting();
        }

        private void StopCasting()
        {
            channeling = false;
            if (startEffectInstance) Destroy(startEffectInstance);
            if (laserEffectInstance) Destroy(laserEffectInstance);
            if (hitEffectInstance) Destroy(hitEffectInstance);
            hitEffectInstance = null;

            // Remove passive effect if needed
            if (effectApplied && spell.passiveEffect != null && hitTarget != null)
            {
                try { spell.passiveEffect.Remove(hitTarget); } catch { }
            }

            Destroy(this);
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
                try { status.AddManaEffect("channeled_projectile_cost", -amount); return true; } catch { }
            }
            return true;
        }
    }
}

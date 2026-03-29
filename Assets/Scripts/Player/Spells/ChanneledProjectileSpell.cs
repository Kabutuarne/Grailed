using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/ChanneledProjectile", fileName = "NewChanneledProjectileSpell")]
public class ChanneledProjectileSpell : ScriptableObject, IChanneledCastSpell
{
    [Header("Casting")]
    public float castTime = 1f;
    public float channelManaCostPerSecond = 1f;

    [Header("Laser")]
    public GameObject startEffectPrefab;
    public GameObject laserEffectPrefab;
    public GameObject hitEffectPrefab;
    public PassiveEffect passiveEffect;
    public Vector3 cameraLocalSpawnOffset = new Vector3(0.0f, -0.15f, 0.25f);
    public float cameraForwardSpawnPush = 0.35f;
    public bool aimViaCameraRaycast = true;
    public float aimRayDistance = 200f;
    public LayerMask aimRayMask = ~0;

    public float CastTime => castTime;

    public IChannelCastRuntime StartChannel(GameObject caster)
    {
        return StartCast(caster);
    }

    // Kept for compatibility with any existing direct calls.
    public ChanneledProjectileRuntime StartCast(GameObject caster)
    {
        if (caster == null)
            return null;

        ChanneledProjectileRuntime runtime = caster.AddComponent<ChanneledProjectileRuntime>();
        runtime.Init(this, caster);
        return runtime;
    }

    public class ChanneledProjectileRuntime : MonoBehaviour, IChannelCastRuntime
    {
        private ChanneledProjectileSpell spell;
        private GameObject caster;

        private GameObject startEffectInstance;
        private GameObject laserEffectInstance;
        private GameObject hitEffectInstance;
        private GameObject hitTarget;

        private bool channeling;
        private bool shouldChannel;

        public void Init(ChanneledProjectileSpell spell, GameObject caster)
        {
            this.spell = spell;
            this.caster = caster;
            shouldChannel = true;

            SpellAimUtility.GetAimData(
                caster,
                spell.cameraLocalSpawnOffset,
                spell.cameraForwardSpawnPush,
                spell.aimViaCameraRaycast,
                spell.aimRayDistance,
                spell.aimRayMask,
                out _,
                out Vector3 origin,
                out Vector3 aimDir,
                out _,
                out _,
                out _);

            if (spell.startEffectPrefab != null)
                startEffectInstance = Instantiate(spell.startEffectPrefab, origin, Quaternion.LookRotation(aimDir));

            BeginChannel();
        }

        void Update()
        {
            if (!channeling)
                return;

            if (!shouldChannel)
            {
                StopChannel();
                return;
            }

            if (!ManaUtility.TrySpendMana(caster, spell.channelManaCostPerSecond * Time.deltaTime, "channeled_projectile_cost"))
            {
                StopChannel();
                return;
            }

            SpellAimUtility.GetAimData(
                caster,
                spell.cameraLocalSpawnOffset,
                spell.cameraForwardSpawnPush,
                spell.aimViaCameraRaycast,
                spell.aimRayDistance,
                spell.aimRayMask,
                out _,
                out Vector3 origin,
                out Vector3 aimDir,
                out Vector3 hitPoint,
                out RaycastHit hitInfo,
                out bool hasHit);

            if (laserEffectInstance != null)
            {
                laserEffectInstance.transform.position = origin;
                laserEffectInstance.transform.rotation = Quaternion.LookRotation(aimDir);
            }

            if (spell.hitEffectPrefab != null)
            {
                if (hitEffectInstance == null)
                    hitEffectInstance = Instantiate(spell.hitEffectPrefab, hitPoint, Quaternion.identity);

                hitEffectInstance.transform.position = hitPoint;
            }
            else if (hitEffectInstance != null)
            {
                Destroy(hitEffectInstance);
                hitEffectInstance = null;
            }

            UpdateTargetEffect(hasHit ? hitInfo.collider.gameObject : null);
        }

        private void BeginChannel()
        {
            channeling = true;

            SpellAimUtility.GetAimData(
                caster,
                spell.cameraLocalSpawnOffset,
                spell.cameraForwardSpawnPush,
                spell.aimViaCameraRaycast,
                spell.aimRayDistance,
                spell.aimRayMask,
                out _,
                out Vector3 origin,
                out Vector3 aimDir,
                out Vector3 hitPoint,
                out _,
                out _);

            if (spell.laserEffectPrefab != null)
                laserEffectInstance = Instantiate(spell.laserEffectPrefab, origin, Quaternion.LookRotation(aimDir));

            if (spell.hitEffectPrefab != null)
                hitEffectInstance = Instantiate(spell.hitEffectPrefab, hitPoint, Quaternion.identity);
        }

        private void UpdateTargetEffect(GameObject newTarget)
        {
            if (spell.passiveEffect == null)
                return;

            if (hitTarget == newTarget)
                return;

            if (hitTarget != null)
            {
                try { spell.passiveEffect.Remove(hitTarget); } catch { }
            }

            hitTarget = newTarget;

            if (hitTarget != null)
            {
                try { spell.passiveEffect.Apply(hitTarget); } catch { }
            }
        }

        public void StopChannel()
        {
            shouldChannel = false;
            channeling = false;

            if (startEffectInstance != null) Destroy(startEffectInstance);
            if (laserEffectInstance != null) Destroy(laserEffectInstance);
            if (hitEffectInstance != null) Destroy(hitEffectInstance);

            startEffectInstance = null;
            laserEffectInstance = null;
            hitEffectInstance = null;

            if (spell.passiveEffect != null && hitTarget != null)
            {
                try { spell.passiveEffect.Remove(hitTarget); } catch { }
            }

            hitTarget = null;
            Destroy(this);
        }
    }
}
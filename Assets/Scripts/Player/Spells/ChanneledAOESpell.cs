using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/ChanneledAOE", fileName = "NewChanneledAOESpell")]
public class ChanneledAOESpell : ScriptableObject
{
    [Header("Casting")]
    public float castTime = 1f; // Time before channel starts
    public float channelManaCostPerSecond = 1f;
    public float channelDuration = 5f; // Max channel duration

    [Header("Area")]
    public float radius = 3f;
    public EffectCarrier effectCarrier;
    public GameObject channelParticlePrefab;
    public Vector3 effectOffset = Vector3.zero;

    // Called to start the cast (returns runtime handler)
    public ChanneledAOERuntime StartCast(GameObject caster)
    {
        if (caster == null) return null;
        var runtime = caster.AddComponent<ChanneledAOERuntime>();
        runtime.Init(this, caster);
        return runtime;
    }

    public class ChanneledAOERuntime : MonoBehaviour
    {
        private ChanneledAOESpell spell;
        private GameObject caster;
        private GameObject channelParticleInstance;
        private bool channeling = false;
        private float channelElapsed = 0f;

        public void Init(ChanneledAOESpell spell, GameObject caster)
        {
            this.spell = spell;
            this.caster = caster;
            channeling = false;
            channelElapsed = 0f;

            // Do NOT spawn the looped particle here.
            // It should appear only once channeling actually begins.
            BeginChannel();
        }

        void Update()
        {
            if (!channeling)
                return;

            channelElapsed += Time.deltaTime;
            if (channelElapsed >= spell.channelDuration)
            {
                StopChannel();
                return;
            }

            // Spend mana per second
            if (!TrySpendMana(caster, spell.channelManaCostPerSecond * Time.deltaTime))
            {
                StopChannel();
                return;
            }

            Vector3 effectCenter = caster.transform.position + spell.effectOffset;

            // Update particle position while channeling
            if (channelParticleInstance)
                channelParticleInstance.transform.position = effectCenter;

            // Apply effects to all entities in radius
            Collider[] hits = Physics.OverlapSphere(effectCenter, spell.radius);
            foreach (var c in hits)
            {
                ApplyEffects(c.gameObject);
            }
        }

        void BeginChannel()
        {
            channeling = true;
            channelElapsed = 0f;

            Vector3 effectCenter = caster.transform.position + spell.effectOffset;

            // Spawn looped particle ONLY when channel begins
            if (spell.channelParticlePrefab != null)
                channelParticleInstance = Instantiate(spell.channelParticlePrefab, effectCenter, Quaternion.identity, null);
        }

        public void StopChannel()
        {
            channeling = false;

            if (channelParticleInstance)
                Destroy(channelParticleInstance);

            channelParticleInstance = null;
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
                try { status.AddManaEffect("channeled_aoe_cost", -amount); return true; } catch { }
            }

            return true;
        }

        void ApplyEffects(GameObject target)
        {
            if (spell.effectCarrier == null) return;
            if (spell.effectCarrier.effects == null || spell.effectCarrier.effects.Length == 0) return;

            foreach (var eff in spell.effectCarrier.effects)
            {
                if (eff != null)
                {
                    try { eff.Apply(target, spell.effectCarrier); } catch { }
                }
            }
        }
    }
}
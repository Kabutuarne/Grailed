using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/Spell/ChanneledAOE", fileName = "NewChanneledAOESpell")]
public class ChanneledAOESpell : ScriptableObject, IChanneledCastSpell
{
    [Header("Casting")]
    public float castTime = 1f;
    public float channelManaCostPerSecond = 1f;
    public float channelDuration = 5f;

    [Header("Area")]
    public float radius = 3f;
    public EffectCarrier effectCarrier;
    public GameObject channelParticlePrefab;
    public Vector3 effectOffset = Vector3.zero;

    public float CastTime => castTime;

    public IChannelCastRuntime StartChannel(GameObject caster)
    {
        return StartCast(caster);
    }

    // Kept for compatibility with any existing direct calls.
    public ChanneledAOERuntime StartCast(GameObject caster)
    {
        if (caster == null)
            return null;

        ChanneledAOERuntime runtime = caster.AddComponent<ChanneledAOERuntime>();
        runtime.Init(this, caster);
        return runtime;
    }

    public class ChanneledAOERuntime : MonoBehaviour, IChannelCastRuntime
    {
        private ChanneledAOESpell spell;
        private GameObject caster;
        private GameObject channelParticleInstance;
        private float channelElapsed;
        private bool channeling;

        public void Init(ChanneledAOESpell spell, GameObject caster)
        {
            this.spell = spell;
            this.caster = caster;
            channelElapsed = 0f;
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

            if (!ManaUtility.TrySpendMana(caster, spell.channelManaCostPerSecond * Time.deltaTime, "channeled_aoe_cost"))
            {
                StopChannel();
                return;
            }

            Vector3 effectCenter = caster.transform.position + spell.effectOffset;

            if (channelParticleInstance != null)
                channelParticleInstance.transform.position = effectCenter;

            if (spell.effectCarrier == null)
                return;

            Collider[] hits = Physics.OverlapSphere(effectCenter, spell.radius);
            HashSet<GameObject> appliedTargets = new HashSet<GameObject>();

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                GameObject target = hit.gameObject;
                if (!appliedTargets.Add(target))
                    continue;

                spell.effectCarrier.Apply(target);
            }
        }

        private void BeginChannel()
        {
            channeling = true;
            channelElapsed = 0f;

            Vector3 effectCenter = caster.transform.position + spell.effectOffset;

            if (spell.channelParticlePrefab != null)
                channelParticleInstance = Instantiate(spell.channelParticlePrefab, effectCenter, Quaternion.identity);
        }

        public void StopChannel()
        {
            channeling = false;

            if (channelParticleInstance != null)
                Destroy(channelParticleInstance);

            channelParticleInstance = null;
            Destroy(this);
        }
    }
}
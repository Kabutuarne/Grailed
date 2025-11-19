using UnityEngine;
using System.Collections.Generic;

public class PlayerStatusEffects : MonoBehaviour
{
    public class Effect
    {
        public string id;
        public float duration;
        public float timer;
        public float speedMultiplier;
        public float healPerSecond;

        public Effect(string id, float duration, float speedMult, float hps)
        {
            this.id = id;
            this.duration = duration;
            this.timer = duration;
            this.speedMultiplier = speedMult;
            this.healPerSecond = hps;
        }
    }

    public List<Effect> activeEffects = new List<Effect>();
    PlayerStats stats;

    void Start()
    {
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var e = activeEffects[i];
            e.timer -= Time.deltaTime;

            if (e.healPerSecond != 0)
                stats.Heal(e.healPerSecond * Time.deltaTime);

            if (e.timer <= 0)
                activeEffects.RemoveAt(i);
        }
    }

    public void AddEffect(string id, float duration, float speedMult = 1, float hps = 0)
    {
        activeEffects.Add(new Effect(id, duration, speedMult, hps));
    }

    public float GetSpeedMultiplier()
    {
        float mult = 1;
        foreach (var e in activeEffects)
            mult *= e.speedMultiplier;

        return mult;
    }
}

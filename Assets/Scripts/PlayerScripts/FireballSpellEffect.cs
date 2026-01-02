using UnityEngine;

[CreateAssetMenu(menuName = "DungeonBroker/SpellEffect/Fireball", fileName = "NewFireballSpell")]
public class FireballSpellEffect : SpellEffect
{
    public float speed = 8f;
    public float lifetime = 5f;
    public float damage = 20f;
    public GameObject projectilePrefab; // optional prefab to use for projectile (must contain or accept FireballBehaviour)
    public float aoeRadius = 0f;
    public GameObject groundFirePrefab; // optional prefab to spawn at impact

    public override void Trigger(GameObject caster)
    {
        // Determine aim direction from the main camera if available, otherwise caster forward
        Vector3 aimDir = caster.transform.forward;
        if (Camera.main != null)
        {
            aimDir = Camera.main.transform.forward;
        }

        // spawn position slightly in front of caster/camera
        Vector3 spawnPos = caster.transform.position + aimDir.normalized * 1f + Vector3.up * 0.5f;

        GameObject proj = null;
        if (projectilePrefab != null)
        {
            proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(aimDir));
        }
        else
        {
            proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proj.transform.position = spawnPos;
            proj.transform.rotation = Quaternion.LookRotation(aimDir);
            var rb = proj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            var col = proj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        FireballBehaviour fb = proj.GetComponent<FireballBehaviour>();
        if (fb == null)
            fb = proj.AddComponent<FireballBehaviour>();

        fb.speed = speed;
        fb.lifeTime = lifetime;
        fb.damage = damage;
        fb.aoeRadius = aoeRadius;
        fb.groundFirePrefab = groundFirePrefab;
        fb.Initialize(caster);
        proj.transform.forward = aimDir;
    }
}

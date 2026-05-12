using UnityEngine;

[DisallowMultipleComponent]
public class SkeletonDeathHandler : MonoBehaviour
{
    [Header("Limb Explosion")]
    public GameObject headPrefab;
    public GameObject torsoPrefab;
    public GameObject leftArmPrefab;
    public GameObject rightArmPrefab;
    public GameObject leftLegPrefab;
    public GameObject rightLegPrefab;

    public float explosionForce = 5f;
    public float upwardModifier = 2f;
    public float torqueAmount = 10f;
    public float destroyDelay = 0.5f;

    private SkeletonAI ai;

    public void Initialize(SkeletonAI skeletonAI) => ai = skeletonAI;

    public void Die()
    {
        if (ai.animator != null)
            ai.animator.enabled = false;

        Collider mainCol = GetComponent<Collider>();
        if (mainCol) mainCol.enabled = false;

        if (ai.rb != null)
        {
            ai.rb.isKinematic = true;
            ai.rb.detectCollisions = false;
        }

        SpawnLimbWithPhysics(headPrefab, HumanBodyBones.Head);
        SpawnLimbWithPhysics(torsoPrefab, HumanBodyBones.Spine);
        SpawnLimbWithPhysics(leftArmPrefab, HumanBodyBones.LeftUpperArm);
        SpawnLimbWithPhysics(rightArmPrefab, HumanBodyBones.RightUpperArm);
        SpawnLimbWithPhysics(leftLegPrefab, HumanBodyBones.LeftUpperLeg);
        SpawnLimbWithPhysics(rightLegPrefab, HumanBodyBones.RightUpperLeg);

        if (ai.stats != null)
            ai.stats.SpawnDeathDrops();

        Destroy(gameObject, destroyDelay);
    }

    private void SpawnLimbWithPhysics(GameObject prefab, HumanBodyBones bone)
    {
        if (prefab == null || ai.animator == null) return;
        Transform boneTransform = ai.animator.GetBoneTransform(bone);
        if (boneTransform == null) return;

        GameObject limb = Instantiate(prefab, boneTransform.position, boneTransform.rotation);
        Rigidbody rb = limb.GetComponent<Rigidbody>() ?? limb.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.detectCollisions = true;

        Vector3 randomDir = Random.insideUnitSphere;
        randomDir.y = Mathf.Abs(randomDir.y) + 0.5f;
        rb.AddForce(randomDir * explosionForce + Vector3.up * upwardModifier, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueAmount, ForceMode.Impulse);

        Destroy(limb, 5f);
    }
}
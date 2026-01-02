using UnityEngine;
using UnityEditor;

public static class CreateSpellAssets
{
    [MenuItem("Tools/DungeonBroker/Create Fireball Prefab & Asset")]
    public static void CreateFireballPrefabAndAsset()
    {
        // Ensure folders
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/SpellEffects"))
            AssetDatabase.CreateFolder("Assets", "SpellEffects");

        // Create a temporary GameObject with a ParticleSystem and FireballBehaviour
        GameObject go = new GameObject("Fireball_Prefab_Temp");
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.0f));
        main.startSize = 0.4f;
        main.startLifetime = 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        var emission = ps.emission;
        emission.rateOverTime = 250f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.1f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // Create a simple particle material using the built-in particles shader
        Shader particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
        {
            var mat = new Material(particleShader);
            mat.name = "FireballParticleMat";
            mat.SetColor("_Color", new Color(1f, 0.6f, 0.15f, 1f));
            AssetDatabase.CreateAsset(mat, "Assets/Prefabs/FireballParticleMat.mat");
            renderer.material = mat;
        }

        // Add collider/behaviour
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.2f;

        var fb = go.AddComponent<FireballBehaviour>();
        fb.speed = 10f;
        fb.damage = 25f;
        fb.lifeTime = 3f;

        // Save as prefab
        string prefabPath = "Assets/Prefabs/Fireball.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

        // Destroy temp
        Object.DestroyImmediate(go);

        // Create FireballSpellEffect asset and assign the prefab
        var fireAsset = ScriptableObject.CreateInstance<FireballSpellEffect>();
        fireAsset.title = "Fireball";
        fireAsset.castTime = 0.2f;
        fireAsset.manaCost = 10f;
        fireAsset.description = "A flaming projectile spawned towards aim direction.";

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        fireAsset.effectPrefab = prefab;

        string assetPath = "Assets/SpellEffects/FireballSpellEffect.asset";
        AssetDatabase.CreateAsset(fireAsset, assetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("DungeonBroker", "Created Fireball prefab and SpellEffect asset.", "OK");
    }
}

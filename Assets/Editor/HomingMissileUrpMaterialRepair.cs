using UnityEditor;
using UnityEngine;

public static class HomingMissileUrpMaterialRepair
{
    [InitializeOnLoadMethod]
    private static void RepairIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            var missileMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/homing missile/Materials/Missiles_Pack.mat");
            var explosionMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/homing missile/Materials/Explosion.mat");
            var smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/homing missile/Materials/SmokeLight.mat");

            if (missileMaterial == null || explosionMaterial == null || smokeMaterial == null)
            {
                return;
            }

            if (missileMaterial.shader.name != "Universal Render Pipeline/Lit" ||
                explosionMaterial.shader.name != "Universal Render Pipeline/Particles/Unlit" ||
                smokeMaterial.shader.name != "Universal Render Pipeline/Particles/Unlit")
            {
                Repair();
            }
        };
    }

    [MenuItem("Tools/Repair/Homing Missile URP Materials")]
    public static void Repair()
    {
        ConvertLit("Assets/homing missile/Materials/Missiles_Pack.mat");
        ConvertParticle("Assets/homing missile/Materials/Explosion.mat", true);
        ConvertParticle("Assets/homing missile/Materials/SmokeLight.mat", false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Repaired homing missile materials for URP.");
    }

    private static void ConvertLit(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (material == null || shader == null)
        {
            Debug.LogError($"Could not convert material at {path}.");
            return;
        }

        var mainTex = material.GetTexture("_MainTex");
        var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

        material.shader = shader;
        material.SetTexture("_BaseMap", mainTex);
        material.SetTexture("_MainTex", mainTex);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", 1f);
        material.SetFloat("_DstBlend", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHABLEND_ON");

        EditorUtility.SetDirty(material);
    }

    private static void ConvertParticle(string path, bool additive)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (material == null || shader == null)
        {
            Debug.LogError($"Could not convert particle material at {path}.");
            return;
        }

        var mainTex = material.GetTexture("_MainTex");
        var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        var emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;

        material.shader = shader;
        material.SetTexture("_BaseMap", mainTex);
        material.SetTexture("_MainTex", mainTex);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetColor("_EmissionColor", emissionColor);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", additive ? 1f : 5f);
        material.SetFloat("_DstBlend", additive ? 1f : 10f);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");

        EditorUtility.SetDirty(material);
    }
}

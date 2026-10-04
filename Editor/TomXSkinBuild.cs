using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public enum TomSkinDebugView
{
    Off, Albedo, DetailRGB, LineRGB, Liquid, DiffuseNormal, Gloss,
    UV1, UV2, UV3, Control, Coverage, LiquidNormal, DiffuseTint
}

public static class TomXSkinBuild
{
    const string Root = "Assets/Mods/TomShadersX";
    const string Bundle = "chara/tom/shaders/tomx.unity3d";

    public static string Validate()
    {
        return TomXStageTwoValidation.Validate("tom/SkinX");
    }

    static Texture2D DefaultTexture(string name, Color color)
    {
        string path = Root + "/Material/" + name + ".asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixel(0, 0, color); texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
        }
        return texture;
    }

    public static string Prepare()
    {
        var shader = Shader.Find("tom/SkinX");
        if (!shader) throw new Exception("SkinX is not imported.");
        var detail = DefaultTexture("SkinNeutralDetail", new Color(1, 0, 0, 1));
        var control = DefaultTexture("SkinNeutralControl", new Color(1, 1, 0, 1));
        var empty = DefaultTexture("SkinTransparentOverlay", Color.clear);
        var importer = AssetImporter.GetAtPath(Root + "/Shaders/Tom/SkinX.shader") as ShaderImporter;
        importer.SetDefaultTextures(new[] { "_DetailMask", "_SkinControlMap", "_overtex1", "_overtex2", "_overtex3" },
            new Texture[] { detail, control, empty, empty, empty });
        importer.SaveAndReimport();
        var defaults = new Material(shader);
        try
        {
            foreach (string key in new[] { "_DetailMask", "_SkinControlMap", "_overtex1", "_overtex2", "_overtex3" })
                if (!defaults.GetTexture(key) || !AssetDatabase.GetAssetPath(defaults.GetTexture(key)).StartsWith(Root + "/"))
                    throw new Exception("Missing packaged SkinX default texture: " + key);
        }
        finally { UnityEngine.Object.DestroyImmediate(defaults); }
        string materialPath = Root + "/Material/m_TomSkinX.mat";
        string prefabPath = Root + "/Prefab/a_TomSkinX.prefab";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(shader) { name = "m_TomSkinX" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        if (material.shader != shader) throw new Exception("Existing SkinX carrier uses another shader.");
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
        {
            var carrier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                carrier.name = "a_TomSkinX";
                carrier.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(carrier.GetComponent<Collider>());
                PrefabUtility.SaveAsPrefabAsset(carrier, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(carrier); }
        }
        AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(Bundle, "");
        AssetDatabase.SaveAssets();
        return "SkinX default data and carrier created through Unity APIs.\n" + Validate();
    }

    public static string Build()
    {
        Prepare();
        var assets = new List<string>();
        foreach (string entry in TomXCoatEyeBuild.Entries) assets.Add(Root + "/Prefab/a_Tom" + entry + ".prefab");
        assets.Add(Root + "/Prefab/a_TomSkinX.prefab");
        assets.Add(Root + "/Tooltips/tom_x_tooltips.xml");
        foreach (string dependency in AssetDatabase.GetDependencies(assets.ToArray(), true))
            if (dependency.StartsWith("Assets/") && !dependency.StartsWith(Root + "/"))
                throw new Exception("External SkinX build dependency: " + dependency);
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CodexBridge/Builds/TomX-0.3.1/abdata");
        Directory.CreateDirectory(output);
        var build = new AssetBundleBuild { assetBundleName = Bundle, assetNames = assets.ToArray() };
        var result = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (!result) throw new Exception("SkinX bundle build failed.");
        string validation = Validate();
        var bundle = AssetBundle.LoadFromFile(Path.Combine(output, Bundle));
        if (!bundle) throw new Exception("Built SkinX bundle could not be loaded.");
        try
        {
            foreach (string path in assets)
                if (!bundle.LoadAsset<UnityEngine.Object>(path)) throw new Exception("Missing packaged asset: " + path);
            foreach (string path in assets)
            {
                if (!path.EndsWith(".prefab", StringComparison.Ordinal)) continue;
                var preset = bundle.LoadAsset<GameObject>(path).GetComponent<Renderer>().sharedMaterial;
                var defaults = new Material(preset.shader);
                try
                {
                    if (!Mathf.Approximately(preset.GetFloat("_IndirectDiffuseIntensity"), .25f)
                        || !Mathf.Approximately(defaults.GetFloat("_IndirectDiffuseIntensity"), .25f))
                        throw new Exception("Packaged indirect default/preset mismatch: " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(defaults); }
            }
            var carrier = bundle.LoadAsset<GameObject>(Root + "/Prefab/a_TomSkinX.prefab");
            var skin = carrier.GetComponent<Renderer>().sharedMaterial;
            if (!skin || !skin.shader || skin.shader.name != "tom/SkinX" || !skin.shader.isSupported)
                throw new Exception("Invalid packaged SkinX material/shader binding.");
            foreach (string key in new[] { "_DetailMask", "_SkinControlMap", "_overtex1", "_overtex2", "_overtex3" })
                if (!skin.GetTexture(key)) throw new Exception("Missing packaged default texture " + key);
        }
        finally { bundle.Unload(true); }
        return "Built and reloaded eight X carriers and tooltips: " + output + ". No game installation was modified.\n" + validation;
    }
}

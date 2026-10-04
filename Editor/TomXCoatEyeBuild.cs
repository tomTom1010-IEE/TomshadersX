using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TomXCoatEyeBuild
{
    public static readonly string[] Entries = { "MainOpaqueX", "MainAlphaX", "MainAlphaX2Pass", "MainAlphaXBackFront", "HairX", "EyeWX", "EyeX" };
    const string Root = "Assets/Mods/TomShadersX";
    const string Bundle = "chara/tom/shaders/tomx.unity3d";

    public static string Validate()
    {
        var result = new StringBuilder();
        foreach (string entry in Entries)
            result.AppendLine(entry + ": " + TomXStageTwoValidation.Validate("tom/" + entry));
        return result.ToString();
    }

    public static string Prepare()
    {
        string validation = Validate();
        foreach (string name in new[] { "EyeWX", "EyeX" })
        {
            string materialPath = Root + "/Material/m_Tom" + name + ".mat";
            string prefabPath = Root + "/Prefab/a_Tom" + name + ".prefab";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("tom/" + name)) { name = "m_Tom" + name };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            if (material.shader.name != "tom/" + name) throw new Exception("Existing carrier has another shader: " + materialPath);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            {
                var carrier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    carrier.name = "a_Tom" + name;
                    carrier.GetComponent<Renderer>().sharedMaterial = material;
                    UnityEngine.Object.DestroyImmediate(carrier.GetComponent<Collider>());
                    PrefabUtility.SaveAsPrefabAsset(carrier, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(carrier); }
            }
            AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(Bundle, "");
        }
        AssetDatabase.SaveAssets();
        return "Eye carriers created through AssetDatabase/PrefabUtility. Existing carriers preserved.\n" + validation;
    }

    public static string Build()
    {
        Prepare();
        var assets = new List<string>();
        foreach (string entry in Entries) assets.Add(Root + "/Prefab/a_Tom" + entry + ".prefab");
        assets.Add(Root + "/Tooltips/tom_x_tooltips.xml");
        foreach (string path in AssetDatabase.GetDependencies(assets.ToArray(), true))
            if (path.StartsWith("Assets/") && !path.StartsWith(Root + "/"))
                throw new Exception("Cross-package build dependency: " + path);
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CodexBridge/Builds/TomX-0.2.1/abdata");
        Directory.CreateDirectory(output);
        // All seven carriers use our actual custom shaders, not game-shader preview replacements.
        var build = new AssetBundleBuild { assetBundleName = Bundle, assetNames = assets.ToArray() };
        var result = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (!result) throw new Exception("X shader bundle build failed.");
        return "Built only the seven X shader carriers and tooltip catalog: " + output + ". No game installation or legacy-package edits.";
    }
}

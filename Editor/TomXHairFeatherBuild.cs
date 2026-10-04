using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TomXHairFeatherBuild
{
    public static string Build()
    {
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CodexBridge/Builds/TomHairFeather");
        Directory.CreateDirectory(output);
        var build = new AssetBundleBuild { assetBundleName = "hairx-feather.unity3d",
            assetNames = new[] { "Assets/Mods/TomShadersX/Shaders/Tom/HiddenHairFeather.shader" } };
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] {build},
            BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Feather helper shader bundle build failed.");
        return "Built optional feather shader bundle: " + output + ". Not installed in the game.";
    }
}

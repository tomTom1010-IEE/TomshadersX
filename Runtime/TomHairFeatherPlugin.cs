#if TOM_X_BEPINEX
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TomShadersX
{
    // Optional KKS adapter. Not compiled into the Unity authoring project's assembly.
    [BepInPlugin("tom.shaders.x.hairfeather", "Tom HairX Feather (Experimental)", "0.3.0")]
    public sealed class TomHairFeatherPlugin : BaseUnityPlugin
    {
        readonly Dictionary<Camera, TomHairFeatherCamera> cameras = new Dictionary<Camera, TomHairFeatherCamera>();
        readonly List<Material> materials = new List<Material>();
        readonly HashSet<string> writers = new HashSet<string>(StringComparer.Ordinal) {
            "xukmi/EyePlus", "xukmi/EyeWPlus", "xukmi/EyeAlphaPlus", "xukmi/EyeWAlphaPlus",
            "xukmi/EyePlusTess", "xukmi/EyeWPlusTess", "tom/EyeWX", "tom/EyeX"
        };
        ConfigEntry<bool> enabledFeature;
        ConfigEntry<bool> diagnosticLogging;
        AssetBundle bundle;
        Shader fieldShader;
        float nextScan;
        string lastDiagnostic;

        void Awake()
        {
            enabledFeature = Config.Bind("Feather", "Enabled", true,
                "Experimental inward HairX feather for known xukmi and TomX eye stencil writers. Disabled releases camera GPU work.");
            diagnosticLogging = Config.Bind("Diagnostics", "LogStateChanges", true,
                "Log discovery and per-camera fallback reasons only when state changes (at most once per second). Does not read GPU pixels.");
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), "hairx-feather.unity3d");
            if (File.Exists(path)) bundle = AssetBundle.LoadFromFile(path);
            if (bundle) fieldShader = bundle.LoadAsset<Shader>("Assets/Mods/TomShadersX/Shaders/Tom/HiddenHairFeather.shader");
            if (fieldShader)
            {
                var probe = new Material(fieldShader);
                bool compatible = probe.HasProperty("_TomHairFieldVersion") && probe.GetFloat("_TomHairFieldVersion") >= 2f;
                Destroy(probe);
                if (!compatible)
                {
                    Logger.LogWarning("[HairFeather] Outdated helper bundle: replace hairx-feather.unity3d together with DLL 0.3.0. Hard fallback enabled.");
                    fieldShader = null;
                }
            }
            if (!fieldShader) Logger.LogWarning("Feather shader bundle missing. HairX Feather will use Hard until the optional bundle is available.");
            else Logger.LogInfo("[HairFeather] Loaded " + path + "; shader=" + fieldShader.name
                + "; supported=" + fieldShader.isSupported + "; API=" + SystemInfo.graphicsDeviceType);
        }

        void Update()
        {
            if (!enabledFeature.Value || !fieldShader)
            {
                Clear();
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 1f;
                    LogState(!enabledFeature.Value ? "Disabled by Feather.Enabled" : "Hard: auxiliary shader missing");
                }
                return;
            }
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            var receivers = new List<Renderer>();
            var sources = new List<TomHairFeatherCamera.Source>();
            var discovery = new List<string>();
            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                materials.Clear(); renderer.GetSharedMaterials(materials);
                for (int n = 0; n < materials.Count; n++)
                {
                    Material material = materials[n];
                    if (!material || !material.shader) continue;
                    if (material.shader.name == "tom/HairX")
                    {
                        if (!receivers.Contains(renderer)) receivers.Add(renderer);
                        if (diagnosticLogging.Value) discovery.Add("Hair " + renderer.name + " / " + material.name
                            + ": queue=" + material.renderQueue + ", mode=" + material.GetFloat("_HairFrontMode")
                            + ", width=" + material.GetFloat("_HairFeatherWidth")
                            + ", widthMode=" + (material.HasProperty("_HairFeatherWidthMode") ? material.GetFloat("_HairFeatherWidthMode") : 0)
                            + ", worldWidth=" + (material.HasProperty("_HairFeatherWorldWidth") ? material.GetFloat("_HairFeatherWorldWidth") : 0));
                    }
                    else if (writers.Contains(material.shader.name))
                    {
                        bool tomEye = material.shader.name == "tom/EyeWX" || material.shader.name == "tom/EyeX";
                        // Missing X mask passes stay missing so the camera takes its explicit hard fallback.
                        string pass = tomEye || material.FindPass("StencilMask") >= 0 ? "StencilMask" : "Forward";
                        sources.Add(new TomHairFeatherCamera.Source { renderer = renderer, materialIndex = n,
                            passName = pass });
                        if (diagnosticLogging.Value) discovery.Add("Writer " + renderer.name + " / " + material.shader.name
                            + ": queue=" + material.renderQueue + ", pass=" + pass + ", index=" + material.FindPass(pass));
                    }
                }
            }
            var alive = new HashSet<Camera>();
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera.cameraType != CameraType.Game || receivers.Count == 0 || sources.Count == 0) continue;
                TomHairFeatherCamera helper;
                if (!cameras.TryGetValue(camera, out helper) || !helper)
                {
                    // Do not take ownership of another integration's component.
                    if (camera.GetComponent<TomHairFeatherCamera>()) continue;
                    helper = camera.gameObject.AddComponent<TomHairFeatherCamera>(); cameras[camera] = helper;
                }
                helper.fieldShader = fieldShader; helper.receivers = receivers.ToArray(); helper.sources = sources.ToArray();
                alive.Add(camera);
            }
            var stale = new List<Camera>();
            foreach (var entry in cameras) if (!entry.Key || !alive.Contains(entry.Key)) stale.Add(entry.Key);
            foreach (Camera camera in stale) { if (cameras[camera]) Destroy(cameras[camera]); cameras.Remove(camera); }
            if (diagnosticLogging.Value)
            {
                // Stable order avoids logging changes caused only by discovery ordering.
                discovery.Sort(StringComparer.Ordinal);
                var lines = new List<string>();
                foreach (Camera camera in Camera.allCameras)
                {
                    TomHairFeatherCamera helper;
                    cameras.TryGetValue(camera, out helper);
                    string state = helper ? (helper.Status ?? "Awaiting first OnPreCull")
                        : camera.GetComponent<TomHairFeatherCamera>() ? "External helper; not owned by plugin"
                        : "No helper: requires Game camera, HairX receivers and recognized writers";
                    string line = "Camera " + camera.name + " (#" + camera.GetInstanceID() + "): " + state
                        + "; type=" + camera.cameraType + "; path=" + camera.actualRenderingPath
                        + "; rect=" + camera.rect + "; pixels=" + camera.pixelWidth + "x" + camera.pixelHeight
                        + "; target=" + (camera.targetTexture ? camera.targetTexture.name : "screen")
                        + "; stereo=" + camera.stereoEnabled;
                    if (helper) line += "; earliestHairQueue=" + helper.EarliestHairQueue
                        + "; visibleWriters=" + helper.VisibleSources + "; lateWriters=" + helper.LateSources
                        + "; missingPass=" + helper.MissingPassSources + "; sourceDraws=" + helper.SourceDraws
                        + "; filters=" + helper.FilterPasses + "; radius=" + helper.SearchRadiusPixels
                        + "; widthBudgetClamped=" + helper.WidthBudgetClamped;
                    lines.Add(line);
                }
                lines.Sort(StringComparer.Ordinal);
                var message = new StringBuilder("Discovery: HairX renderers=" + receivers.Count + ", recognized writers=" + sources.Count);
                foreach (string line in discovery) message.Append("\n  ").Append(line);
                foreach (string line in lines) message.Append("\n  ").Append(line);
                LogState(message.ToString());
            }
        }

        void LogState(string message)
        {
            if (!diagnosticLogging.Value) { lastDiagnostic = null; return; }
            if (lastDiagnostic == message) return;
            lastDiagnostic = message;
            Logger.LogInfo("[HairFeather] " + message);
        }

        void Clear()
        {
            foreach (var entry in cameras) if (entry.Value) { entry.Value.enabled = false; Destroy(entry.Value); }
            cameras.Clear();
        }
        void OnDestroy() { Clear(); if (bundle) bundle.Unload(false); }
    }
}
#endif

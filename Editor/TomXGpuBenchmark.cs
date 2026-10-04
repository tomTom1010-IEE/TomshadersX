using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit Bridge opt-in; no game plugin or persistent scene/material changes.
public static partial class TomXGpuBenchmark
{
    const string X = "Assets/Mods/TomShadersX/Shaders/Tom/";
    const string V = "Assets/Mods/KKShadersPlus-release1.7.1/Shaders/";
    const string Lil = "Assets/Mods/liltoon/Shader/lilToon.shader";
    const string LilCutout = "Assets/Mods/liltoon/Shader/lilToonCutout.shader";
    const int SamplesPerBlock = 20, Rounds = 3;
    static Run current;

    [Serializable] sealed class Sample
    {
        public int round, order;
        public double gpuMs, renderCallMs, fenceReadbackMs;
    }
    [Serializable] sealed class Case
    {
        public string id, preset, shaderPath, dependencyHash, layout;
        public int lights, eyePairs, width = 1920, height = 1080;
        public bool addDisabled;
        public string[] passes;
        public ulong psInvocations, vsInvocations, primitives;
        public double medianMs, p10Ms, p90Ms;
        public double previewMeanLinearRGB;
        public List<Sample> samples = new List<Sample>();
        [NonSerialized] public Material material;
    }
    [Serializable] sealed class Report
    {
        public string status = "running", error, startedUtc = DateTime.UtcNow.ToString("o"), finishedUtc;
        public string unity, gpu, api, colorSpace, cpu;
        public int meshSubdivisions, trianglesPerEye;
        public string stencilSetup = "Ref 2 seeded identically over the target before the measured interval, without color/depth writes. This supplies V+ Eye ForwardAdd's pre-existing stencil dependency; native material passes are unchanged. The old unseeded V+ eye multi-light results are not comparable.";
        public string vplusSetup = "_UseForwardAddFullShadows=1 and _DisablePointLights=0 on V+ test materials so its optional Add path actually executes lighting instead of its default early clip. Shadows remain disabled on all lights. Prior Add-off pilot timings are excluded from functional multi-light comparisons.";
        public string method = "D3D11 TIMESTAMP + TIMESTAMP_DISJOINT via render-thread events, BeforeForwardOpaque to AfterEverything. One camera render per Editor update. End-query polling and 1px readback are outside GPU interval. Pipeline statistics sampled only in untimed diagnostics. No FPS-derived or CPU-substituted GPU estimates.";
        public string scope = "Synthetic single curved UV patch per eye, 2 eyes per proxy character; no body, hair, animation, shadows or postprocessing. 1920x1080 ARGBHalf, no MSAA, orthographic fixed camera, 25-degree mesh orientation. Crowd uses fixed-size eyes, not constant-total-coverage rescaling. Not complete KKS character/game performance.";
        public string comparison = "Local V+ LTS 1.7.5 and local lilToon KKS port 1.0.3, loaded by asset path, not ambiguous Shader.Find. Same mesh/texture/lights/target; material models, coverage and pass structure differ. Not equal-feature or equal-image rankings. lilToon parallax is its simple offset, POM off; reflection is not a separate clearcoat.";
        public int completedBlocks, totalBlocks, samplesPerBlock = SamplesPerBlock, rounds = Rounds;
        public List<Case> cases = new List<Case>();
    }

    sealed class Timer : IDisposable
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryW(string path);
        [DllImport("kernel32", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32")] static extern bool FreeLibrary(IntPtr module);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Init(IntPtr texture);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Event();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Stats(int enabled);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Read(out double ms, out ulong ps, out ulong vs, out ulong primitives);
        readonly IntPtr module;
        readonly Stats stats;
        readonly Read read;
        public readonly IntPtr callback;
        T Proc<T>(string name) where T : class
        {
            IntPtr p = GetProcAddress(module, name);
            if (p == IntPtr.Zero) throw new Exception("Missing GPU timer export: " + name);
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }
        public Timer(RenderTexture target)
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "CodexBridge/Builds/TomXGpuTimer/Release/TomXGpuTimer.dll");
            module = LoadLibraryW(path);
            if (module == IntPtr.Zero) throw new Exception("Build GPU timer first: " + path + "; Win32=" + Marshal.GetLastWin32Error());
            try
            {
                stats = Proc<Stats>("SetStatistics"); read = Proc<Read>("Result"); callback = Proc<Event>("GetRenderEvent")();
                if (Proc<Init>("Initialize")(target.GetNativeTexturePtr()) != 1) throw new Exception("D3D11 query initialization failed.");
            }
            catch { FreeLibrary(module); throw; }
        }
        public void Statistics(bool enabled) { stats(enabled ? 1 : 0); }
        public double ReadResult(Case c, bool diagnostic)
        {
            double ms; ulong ps, vs, primitives;
            int result = read(out ms, out ps, out vs, out primitives);
            if (result != 1 || double.IsNaN(ms) || double.IsInfinity(ms) || ms < 0)
                throw new Exception("GPU timestamp unavailable/disjoint/timeout: " + result);
            if (diagnostic) { c.psInvocations = ps; c.vsInvocations = vs; c.primitives = primitives; }
            return ms;
        }
        public void Dispose() { FreeLibrary(module); }
    }

    sealed class Run : IDisposable
    {
        public readonly string output;
        readonly Report report;
        readonly Scene previous, scene;
        readonly RenderTexture oldTarget;
        readonly int oldPixelLights;
        readonly Dictionary<Light, int> oldMasks = new Dictionary<Light, int>();
        readonly List<Object> owned = new List<Object>();
        readonly List<Renderer> eyes = new List<Renderer>();
        readonly List<Light> lights = new List<Light>();
        readonly List<Case> order = new List<Case>();
        readonly Camera camera;
        readonly RenderTexture target;
        readonly Texture2D fence;
        readonly Timer timer;
        readonly CommandBuffer begin, end;
        readonly int layer;
        readonly Texture2D iris, depth, blank;
        int round, block, frame, sampleOrder;
        bool disposed;
        public Run(int subdivisions)
        {
            previous = SceneManager.GetActiveScene(); oldTarget = RenderTexture.active;
            oldPixelLights = QualitySettings.pixelLightCount;
            var used = new HashSet<int>(); foreach (GameObject g in Object.FindObjectsOfType<GameObject>()) used.Add(g.layer);
            for (layer = 31; layer >= 8 && used.Contains(layer); layer--) { }
            if (layer < 8) throw new Exception("No unused layer for isolated benchmark.");
            output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CodexBridge/Reports/XGpuBaseline-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(), cpu = SystemInfo.processorType,
                meshSubdivisions = subdivisions, trianglesPerEye = subdivisions * subdivisions * 2 };
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (Light light in Object.FindObjectsOfType<Light>()) { oldMasks[light] = light.cullingMask; light.cullingMask &= ~(1 << layer); }
                QualitySettings.pixelLightCount = 8;
                RenderSettings.skybox = null; RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.12f, .12f, .12f);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflection = Own(TomXCoatEyeValidation.Environment()); RenderSettings.reflectionIntensity = 1;
                camera = new GameObject("GPU benchmark camera").AddComponent<Camera>(); camera.enabled = false;
                camera.cullingMask = 1 << layer; camera.renderingPath = RenderingPath.Forward;
                camera.allowHDR = true; camera.allowMSAA = false; camera.useOcclusionCulling = false;
                camera.orthographic = true; camera.orthographicSize = 1.8f;
                camera.transform.position = new Vector3(0, 0, -8); camera.transform.LookAt(Vector3.zero);
                camera.nearClipPlane = .1f; camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.012f, .016f, .022f, 0);
                target = Own(new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear));
                target.Create(); camera.targetTexture = target;
                fence = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
                iris = Own(TomXCoatEyeValidation.Iris()); depth = Own(TomXCoatEyeValidation.SurfaceMap(2, false, false));
                blank = Own(TomXCoatEyeValidation.Solid(Color.clear));
                iris.name = "Benchmark iris (shared 1024 RGBA mipmapped)";
                depth.name = "Benchmark shallow bowl (linear height R, 1024 mipmapped)";
                blank.name = "Benchmark zero RGBA";
                Mesh mesh = Own(TomXCoatEyeValidation.EyePatch(subdivisions));
                for (int i = 0; i < 16; i++)
                {
                    var g = new GameObject("Proxy eye " + i); g.layer = layer;
                    g.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = g.AddComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Simple; eyes.Add(r);
                }
                for (int i = 0; i < 8; i++)
                {
                    var l = new GameObject("Pixel light " + i).AddComponent<Light>();
                    l.type = i == 0 ? LightType.Directional : LightType.Point;
                    l.renderMode = LightRenderMode.ForcePixel; l.cullingMask = 1 << layer;
                    l.shadows = LightShadows.None; l.range = 30; l.intensity = i == 0 ? 1 : .35f;
                    l.transform.rotation = Quaternion.Euler(20, -20, 0);
                    l.transform.position = new Vector3(Mathf.Cos(i) * 2, Mathf.Sin(i) * 1.2f, -3);
                    lights.Add(l);
                }
                BuildCases();
                timer = new Timer(target);
                Shader seedShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Mods/TomShadersX/Tests/GpuTimer/BenchmarkStencilSeed.shader");
                if (!seedShader || !seedShader.isSupported) throw new Exception("Missing benchmark stencil seed shader.");
                var seed = Own(new Material(seedShader));
                begin = new CommandBuffer { name = "TomX GPU timestamp begin" };
                // Legacy V+ Add precedes its own Base stencil writer. Supply the scene dependency for all candidates, outside timing.
                begin.DrawProcedural(Matrix4x4.identity, seed, 0, MeshTopology.Triangles, 3);
                begin.IssuePluginEvent(timer.callback, 0);
                end = new CommandBuffer { name = "TomX GPU timestamp end" }; end.IssuePluginEvent(timer.callback, 1);
                camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, begin); camera.AddCommandBuffer(CameraEvent.AfterEverything, end);
                report.totalBlocks = report.cases.Count * Rounds;
                Shuffle(); Save();
            }
            catch { Dispose(); throw; }
        }
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        void Set(Material m, string p, float value) { if (m.HasProperty(p)) m.SetFloat(p, value); }
        void SetColor(Material m, string p, UnityEngine.Color value) { if (m.HasProperty(p)) m.SetColor(p, value); }
        void Texture(Material m, string p, UnityEngine.Texture value) { if (m.HasProperty(p)) m.SetTexture(p, value); }
        Material Material(string preset, string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("Unsupported benchmark shader: " + path);
            var m = Own(new Material(shader)); m.name = preset; m.enableInstancing = false;
            Texture(m, "_MainTex", iris); Texture(m, "_expression", blank); Texture(m, "_overtex1", blank); Texture(m, "_overtex2", blank);
            SetColor(m, "_BaseColor", UnityEngine.Color.white); SetColor(m, "_Color", UnityEngine.Color.white);
            Set(m, "_OutlineOn", 0); Set(m, "_UseOutline", 0); Set(m, "_OutlineWidth", 0);
            if (path.StartsWith(X, StringComparison.Ordinal))
            {
                Set(m, "_ClearCoat", preset.Contains("coat") ? 1 : 0); Set(m, "_ClearCoatRoughness", .15f);
                Set(m, "_ClearCoatIOR", 1.5f); Set(m, "_EyeRefractionIOR", 1.376f);
                Set(m, "_EyeOpticsMode", preset.Contains("flat") || preset.Contains("bowl") || preset.Contains("map") ? 1 : 0);
                Set(m, "_IrisDepth", .25f); Set(m, "_IrisRadiusX", .37f); Set(m, "_IrisRadiusY", .37f);
                Set(m, "_IrisDepthShape", preset.Contains("bowl") ? 2 : 0);
                Set(m, "_UseEyeSurfaceMask", preset.Contains("map") ? 1 : 0);
                Texture(m, "_EyeSurfaceMap", depth); Set(m, "_EyeDispersion", preset.Contains("disp") ? 1 : 0);
                Set(m, "_LightProbeBlend", 0); Set(m, "_CustomSHVolumeBlend", 0); Set(m, "_VertexLightIntensity", 0);
                SetColor(m, "_AmbientColor", new UnityEngine.Color(.12f, .12f, .12f, 1));
                Set(m, "_IndirectDiffuseIntensity", 1); // Keep the published comparison baseline reproducible.
            }
            else if (path.StartsWith(V, StringComparison.Ordinal))
            {
                Set(m, "_UseMatCapReflection", 0); Set(m, "_ReflectionVal", preset.Contains("reflect") ? 1 : 0);
                Set(m, "_Roughness", .25f); Set(m, "_DisablePointLights", 0);
                Set(m, "_UseForwardAddFullShadows", 1);
                Set(m, "_SpecularPower", .5f); SetColor(m, "_CustomAmbient", new UnityEngine.Color(.12f, .12f, .12f, 1));
            }
            else
            {
                Set(m, "_UseShadow", 1); Set(m, "_UseReflection", preset.Contains("reflect") ? 1 : 0);
                Set(m, "_Smoothness", .75f); Set(m, "_UseParallax", preset.Contains("parallax") ? 1 : 0);
                Set(m, "_UsePOM", 0); Set(m, "_Parallax", .08f); Texture(m, "_ParallaxMap", depth);
                // Cutout reference approximates EyeX's aperture; it is not an eye-optics equivalent.
                if (path == LilCutout) { Set(m, "_Cutoff", .001f); Set(m, "_ZWrite", 0); }
            }
            return m;
        }
        void Add(string preset, string path, string layout, int pairs, int lightCount, bool disableAdd = false)
        {
            var c = new Case { preset = preset, shaderPath = path, layout = layout, eyePairs = pairs, lights = lightCount, addDisabled = disableAdd };
            c.id = preset + "-" + layout + "-p" + pairs + "-l" + lightCount + (disableAdd ? "-noadd" : "");
            c.material = Material(preset, path);
            if (disableAdd) c.material.SetShaderPassEnabled("FORWARDADD", false);
            c.dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            c.passes = new string[c.material.passCount]; for (int i = 0; i < c.passes.Length; i++) c.passes[i] = c.material.GetPassName(i);
            report.cases.Add(c);
        }
        void BuildCases()
        {
            string[] presets = { "x-eye-off", "x-eye-coat", "x-eye-flat", "x-eye-bowl", "x-eye-map", "x-eye-map-coat", "x-eye-map-disp", "x-eye-map-disp-coat", "v-eye-base", "v-eye-reflect", "lil-base", "lil-reflect", "lil-parallax", "lil-eye-base", "lil-eye-reflect", "lil-eye-parallax" };
            foreach (string p in presets)
            {
                string path = p.StartsWith("x-") ? X + "EyeX.shader" : p.StartsWith("v-") ? V + "Eye/EyePlus.shader" : p.StartsWith("lil-eye-") ? LilCutout : Lil;
                foreach (int l in new[] { 1, 4, 8 })
                {
                    foreach (int pairs in new[] { 1, 4, 8 }) Add(p, path, "crowd", pairs, l);
                    Add(p, path, "close", 1, l);
                }
            }
            foreach (string p in new[] { "x-eye-off", "x-eye-map", "x-eye-map-disp-coat" })
                foreach (string layout in new[] { "crowd", "close" }) Add(p, X + "EyeX.shader", layout, layout == "crowd" ? 8 : 1, 8, true);
            foreach (int l in new[] { 1, 4, 8 })
            {
                Add("x-eyew-off", X + "EyeWX.shader", "close", 1, l); Add("x-eyew-coat", X + "EyeWX.shader", "close", 1, l);
                Add("v-eyew-reflect", V + "Eye/EyeWPlus.shader", "close", 1, l);
                Add("x-opaque-off", X + "MainOpaqueX.shader", "close", 1, l); Add("x-opaque-coat", X + "MainOpaqueX.shader", "close", 1, l);
                Add("v-opaque-reflect", V + "Item/MainOpaquePlus.shader", "close", 1, l);
            }
            Add("empty", X + "EyeX.shader", "crowd", 0, 1);
            WriteMaterials();
        }
        void WriteMaterials()
        {
            var text = new System.Text.StringBuilder();
            foreach (Case c in report.cases)
            {
                text.AppendLine("## " + c.id + " | " + c.shaderPath + " | " + c.dependencyHash);
                text.AppendLine("queue=" + c.material.renderQueue + "; passes=" + string.Join(",", c.passes) + "; keywords=" + string.Join(",", c.material.shaderKeywords));
                for (int i = 0; i < ShaderUtil.GetPropertyCount(c.material.shader); i++)
                {
                    string p = ShaderUtil.GetPropertyName(c.material.shader, i); var type = ShaderUtil.GetPropertyType(c.material.shader, i);
                    string value;
                    if (type == ShaderUtil.ShaderPropertyType.TexEnv) { var t = c.material.GetTexture(p); value = t ? t.name + " " + t.width + "x" + t.height : "shader default"; }
                    else if (type == ShaderUtil.ShaderPropertyType.Color || type == ShaderUtil.ShaderPropertyType.Vector) value = c.material.GetVector(p).ToString("R");
                    else value = c.material.GetFloat(p).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    text.AppendLine(p + "=" + value);
                }
            }
            File.WriteAllText(Path.Combine(output, "materials.txt"), text.ToString());
        }
        void Shuffle()
        {
            order.Clear(); order.AddRange(report.cases); var rng = new System.Random(54004 + round);
            for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); Case swap = order[i]; order[i] = order[j]; order[j] = swap; }
        }
        void Configure(Case c)
        {
            for (int i = 0; i < lights.Count; i++) lights[i].enabled = i < c.lights;
            int cols = c.eyePairs <= 1 ? 1 : c.eyePairs == 4 ? 2 : 4, rows = c.eyePairs <= 1 ? 1 : 2;
            for (int i = 0; i < eyes.Count; i++)
            {
                Renderer r = eyes[i]; r.gameObject.SetActive(i < c.eyePairs * 2); r.sharedMaterial = c.material;
                int pair = i / 2; float side = i % 2 == 0 ? -1 : 1;
                float scale = c.layout == "close" ? 1 : .3f;
                r.transform.localScale = Vector3.one * scale; r.transform.rotation = Quaternion.Euler(0, 25, 0);
                r.transform.position = c.layout == "close" ? new Vector3(side * .95f, 0, 0) :
                    new Vector3((pair % cols - (cols - 1) * .5f) * 1.5f + side * .29f, (pair / cols - (rows - 1) * .5f) * 1.4f, 0);
            }
        }
        void Screenshot(Case c)
        {
            var t = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target; t.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); t.Apply();
                var pixels = t.GetPixels(); int valid = 0; double total = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    if (float.IsNaN(p.r) || float.IsInfinity(p.r) || float.IsNaN(p.g) || float.IsInfinity(p.g) || float.IsNaN(p.b) || float.IsInfinity(p.b))
                        throw new Exception("Nonfinite preview: " + c.id);
                    if (p.r + p.g + p.b > .15f) valid++;
                    total += (p.r + p.g + p.b) / 3.0;
                    pixels[i] = new UnityEngine.Color(Mathf.LinearToGammaSpace(Mathf.Max(0, p.r) / (1 + Mathf.Max(0, p.r))),
                        Mathf.LinearToGammaSpace(Mathf.Max(0, p.g) / (1 + Mathf.Max(0, p.g))), Mathf.LinearToGammaSpace(Mathf.Max(0, p.b) / (1 + Mathf.Max(0, p.b))), 1);
                }
                if (c.eyePairs > 0 && valid < 100) throw new Exception("Blank benchmark preview: " + c.id);
                c.previewMeanLinearRGB = total / pixels.Length;
                png.SetPixels(pixels); png.Apply(); File.WriteAllBytes(Path.Combine(output, c.id + ".png"), png.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(t); Object.DestroyImmediate(png); }
        }
        public bool Tick()
        {
            Case c = order[block];
            if (frame == 0)
            {
                Configure(c);
                if (EditorUtility.DisplayCancelableProgressBar("TomX GPU baseline", "Round " + (round + 1) + "/" + Rounds + ": " + c.id, (float)report.completedBlocks / report.totalBlocks))
                    throw new OperationCanceledException("Benchmark cancelled by user.");
            }
            int warm = round == 0 ? 8 : 4;
            bool diagnostic = round == 0 && frame == warm - 1;
            timer.Statistics(diagnostic);
            var watch = Stopwatch.StartNew(); camera.Render(); double submit = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); RenderTexture.active = target;
            fence.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); fence.Apply(false);
            double readback = watch.Elapsed.TotalMilliseconds;
            double ms = timer.ReadResult(c, diagnostic);
            if (diagnostic)
            {
                if (c.eyePairs > 0 && (c.psInvocations == 0 || c.primitives == 0)) throw new Exception("No GPU geometry in " + c.id);
                if (c.layout == "close" && (c.lights == 1 || c.lights == 8) || c.eyePairs == 8 && c.lights == 8) Screenshot(c);
            }
            if (frame >= warm) c.samples.Add(new Sample { round = round, order = sampleOrder++, gpuMs = ms, renderCallMs = submit, fenceReadbackMs = readback });
            frame++;
            if (frame >= warm + SamplesPerBlock)
            {
                Summarize(c); frame = 0; block++; report.completedBlocks++; Save();
                if (block == order.Count)
                {
                    block = 0; round++;
                    if (round == Rounds) { report.status = "complete"; report.finishedUtc = DateTime.UtcNow.ToString("o"); Save(); return true; }
                    Shuffle();
                }
            }
            return false;
        }
        void Summarize(Case c)
        {
            var values = new List<double>(); foreach (Sample s in c.samples) values.Add(s.gpuMs); values.Sort();
            c.medianMs = values[values.Count / 2]; c.p10Ms = values[(values.Count - 1) / 10]; c.p90Ms = values[(values.Count - 1) * 9 / 10];
        }
        void Save() { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
        public void Fail(Exception e) { report.status = "failed"; report.error = e.ToString(); report.finishedUtc = DateTime.UtcNow.ToString("o"); Save(); }
        public void InterruptIfRunning()
        {
            if (report.status == "running") Fail(new OperationCanceledException("Benchmark interrupted before completion (e.g. assembly reload)."));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            EditorUtility.ClearProgressBar();
            if (camera) camera.RemoveAllCommandBuffers();
            if (timer != null)
            {
                using (var release = new CommandBuffer()) { release.IssuePluginEvent(timer.callback, 2); Graphics.ExecuteCommandBuffer(release); }
                RenderTexture.active = target; fence.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); fence.Apply(false);
                timer.Dispose();
            }
            if (begin != null) begin.Release(); if (end != null) end.Release();
            RenderTexture.active = oldTarget;
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach (var p in oldMasks) if (p.Key) p.Key.cullingMask = p.Value;
            QualitySettings.pixelLightCount = oldPixelLights;
            foreach (Object o in owned) if (o) Object.DestroyImmediate(o);
        }
    }
    public static string Start(int subdivisions = 32)
    {
        if (current != null || skinCurrent != null || EditorApplication.isPlaying) throw new Exception("Benchmark already running or Editor in Play Mode.");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) throw new Exception("Benchmark requires D3D11; no CPU timer fallback.");
        if (subdivisions < 8 || subdivisions > 128) throw new Exception("Mesh subdivisions must be 8..128.");
        current = new Run(subdivisions); EditorApplication.update += Tick; AssemblyReloadEvents.beforeAssemblyReload += Stop;
        return "Started asynchronous GPU benchmark; completion is report.status, not this start acknowledgement. " + current.output;
    }
    static void Tick()
    {
        try { if (current.Tick()) Stop(); }
        catch (Exception e) { current.Fail(e); UnityEngine.Debug.LogException(e); Stop(); }
    }
    static void Stop()
    {
        EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        if (current != null) { current.InterruptIfRunning(); current.Dispose(); current = null; }
    }
}

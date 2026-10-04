using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Shares only the native timestamp wrapper with the eye benchmark; no eye presets change.
public static partial class TomXGpuBenchmark
{
    static SkinRun skinCurrent;
    [Serializable] sealed class SkinCase
    {
        public string id, layout, preset, shaderHash;
        public int lights, bodies;
        public ulong psInvocations, primitives;
        public double medianMs, p10Ms, p90Ms;
        public int nonblackPixels;
        public List<double> gpuSamplesMs = new List<double>();
        [NonSerialized] public Material material;
    }
    [Serializable] sealed class SkinReport
    {
        public string status = "running", error, unity, gpu, api, colorSpace, startedUtc = DateTime.UtcNow.ToString("o"), finishedUtc;
        public int completedBlocks, totalBlocks, rounds = 3, samplesPerBlock = 20, width = 1920, height = 1080;
        public string method = "D3D11 TIMESTAMP/DISJOINT on the render thread, BeforeForwardOpaque to AfterEverything. One render per Editor update; readback/query polling outside interval. Three deterministic shuffled rounds; 8/4 warmups then 20 samples per block. Statistics only in untimed diagnostic frame.";
        public string scope = "Large face sphere and seven-part capsule/sphere body proxies, one or four bodies at fixed size, 1/4/8 forced pixel lights. No actual character meshes, skinning, hair, clothes, shadows, animation or postprocessing. Not KKS full-character acceptance. No quality reductions; all presets share the same camera/lights/maps.";
        public List<SkinCase> cases = new List<SkinCase>();
    }
    sealed class SkinRun : IDisposable
    {
        public readonly string output;
        readonly SkinReport report;
        readonly Scene oldScene, scene;
        readonly RenderTexture oldTarget;
        readonly int oldPixelLights;
        readonly List<Object> owned = new List<Object>();
        readonly Dictionary<Light,int> masks = new Dictionary<Light,int>();
        readonly List<Light> lights = new List<Light>();
        readonly List<GameObject> bodies = new List<GameObject>();
        readonly List<Renderer> surfaces = new List<Renderer>();
        readonly List<SkinCase> order = new List<SkinCase>();
        readonly System.Random random = new System.Random(31004);
        readonly Case statistics = new Case();
        Camera camera;
        GameObject face;
        RenderTexture target;
        Texture2D fence;
        Timer timer;
        CommandBuffer begin, end;
        int layer, round, block, frame;
        bool disposed;
        T Own<T>(T o) where T : Object { owned.Add(o); return o; }
        Texture2D Solid(Color c) { return Own(TomXCoatEyeValidation.Solid(c)); }
        public SkinRun()
        {
            oldScene = SceneManager.GetActiveScene(); oldTarget = RenderTexture.active; oldPixelLights = QualitySettings.pixelLightCount;
            output = Path.Combine(Directory.GetParent(Application.dataPath).FullName,"CodexBridge/Reports/XSkinGPU-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            report = new SkinReport { unity=Application.unityVersion, gpu=SystemInfo.graphicsDeviceName,
                api=SystemInfo.graphicsDeviceType.ToString(), colorSpace=QualitySettings.activeColorSpace.ToString() };
            try
            {
                var used = new HashSet<int>(); foreach(GameObject g in Object.FindObjectsOfType<GameObject>()) used.Add(g.layer);
                for(layer=31;layer>=8 && used.Contains(layer);layer--) { }
                if(layer<8) throw new Exception("No free Skin GPU fixture layer.");
                foreach(Light l in Object.FindObjectsOfType<Light>()) { masks[l]=l.cullingMask; l.cullingMask &= ~(1<<layer); }
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
                QualitySettings.pixelLightCount=8; RenderSettings.skybox=null; RenderSettings.fog=false;
                RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.12f,.12f,.12f,1);
                RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom; RenderSettings.customReflection=Own(TomXCoatEyeValidation.Environment());
                RenderSettings.reflectionIntensity=1;
                camera=new GameObject("Skin GPU camera").AddComponent<Camera>(); camera.enabled=false; camera.cullingMask=1<<layer;
                camera.renderingPath=RenderingPath.Forward; camera.orthographic=true; camera.allowHDR=true; camera.allowMSAA=false;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.nearClipPlane=.05f; camera.farClipPlane=30;
                target=Own(new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)); target.Create(); camera.targetTexture=target;
                fence=Own(new Texture2D(1,1,TextureFormat.RGBAFloat,false,true));
                face=Part(null,PrimitiveType.Sphere,new Vector3(0,0,0),Vector3.one);
                face.transform.rotation=Quaternion.Euler(0,25,0);
                for(int n=0;n<4;n++)
                {
                    var body=new GameObject("Seven-part body proxy "+n); body.layer=layer; bodies.Add(body);
                    Part(body,PrimitiveType.Sphere,new Vector3(0,1.55f,0),new Vector3(.3f,.36f,.3f));
                    Part(body,PrimitiveType.Capsule,new Vector3(0,1,0),new Vector3(.48f,.43f,.32f));
                    Part(body,PrimitiveType.Sphere,new Vector3(0,.62f,0),new Vector3(.42f,.35f,.32f));
                    foreach(float sign in new[]{-1f,1f})
                    {
                        Part(body,PrimitiveType.Capsule,new Vector3(sign*.33f,1,0),new Vector3(.14f,.37f,.14f));
                        Part(body,PrimitiveType.Capsule,new Vector3(sign*.13f,.28f,0),new Vector3(.19f,.38f,.19f));
                    }
                }
                for(int n=0;n<8;n++)
                {
                    var l=new GameObject("Skin GPU light "+n).AddComponent<Light>(); lights.Add(l);
                    l.type=n==0?LightType.Directional:LightType.Point; l.renderMode=LightRenderMode.ForcePixel;
                    l.cullingMask=1<<layer; l.shadows=LightShadows.None; l.intensity=n==0?1:.65f; l.range=20;
                    l.transform.position=new Vector3(Mathf.Cos(n)*3,1+Mathf.Sin(n),-2); l.transform.rotation=Quaternion.Euler(20,-25,0);
                }
                var mats=new Dictionary<string,Material>();
                foreach(string preset in new[]{"opaque","dry","soft","coat","liquid","liquid-coat"}) mats[preset]=Material(preset);
                foreach(string layout in new[]{"face-close","body-one","body-four"})
                foreach(int count in new[]{1,4,8})
                foreach(var p in mats)
                    report.cases.Add(new SkinCase { id=layout+"-"+count+"L-"+p.Key,layout=layout,preset=p.Key,lights=count,
                        bodies=layout=="body-four"?4:1,material=p.Value,
                        shaderHash=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(p.Value.shader)).ToString() });
                report.cases.Add(new SkinCase { id="empty",layout="empty",preset="empty",lights=1,bodies=0,material=mats["dry"] });
                timer=new Timer(target); begin=new CommandBuffer { name="Skin GPU begin" }; end=new CommandBuffer { name="Skin GPU end" };
                begin.IssuePluginEvent(timer.callback,0); end.IssuePluginEvent(timer.callback,1);
                camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque,begin); camera.AddCommandBuffer(CameraEvent.AfterEverything,end);
                report.totalBlocks=report.cases.Count*3; Shuffle(); Save();
            }
            catch(Exception e) { Fail(e); Dispose(); throw; }
        }
        GameObject Part(GameObject parent,PrimitiveType primitive,Vector3 position,Vector3 scale)
        {
            var g=GameObject.CreatePrimitive(primitive); g.layer=layer;
            if(parent) g.transform.SetParent(parent.transform,false);
            g.transform.localPosition=position; g.transform.localScale=scale;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r=g.GetComponent<Renderer>(); r.shadowCastingMode=ShadowCastingMode.Off; surfaces.Add(r); return g;
        }
        Material Material(string preset)
        {
            var m=Own(new Material(Shader.Find(preset=="opaque"?"tom/MainOpaqueX":"tom/SkinX")));
            m.SetColor("_BaseColor",new Color(.65f,.38f,.29f,1)); m.SetFloat("_OutlineOn",0);
            m.SetFloat("_LightProbeBlend",0); m.SetFloat("_CustomSHVolumeBlend",0); m.SetFloat("_VertexLightIntensity",0);
            m.SetColor("_AmbientColor",new Color(.12f,.12f,.12f,1)); m.SetFloat("_ReflectionMode",2);
            m.SetFloat("_IndirectDiffuseIntensity",1); // Keep the published comparison baseline reproducible.
            if(preset!="opaque")
            {
                m.SetTexture("_DetailMask",Solid(new Color(1,0,0,1))); m.SetTexture("_SkinControlMap",Solid(new Color(1,1,0,1)));
                m.SetTexture("_overtex1",Solid(Color.clear)); m.SetTexture("_overtex2",Solid(Color.clear)); m.SetTexture("_overtex3",Solid(Color.clear));
                if(preset!="dry") { m.SetFloat("_SkinStrength",.7f); m.SetFloat("_SkinWrap",.4f); m.SetFloat("_SkinWarmth",.3f); }
                if(preset.Contains("liquid"))
                {
                    m.SetTexture("_liquidmask",Solid(Color.red)); m.SetTexture("_Texture2",Solid(new Color(.4f,.8f,0,1)));
                    m.SetTexture("_Texture3",Solid(new Color(1,.6f,1,.6f))); m.SetFloat("_liquidftop",2);
                    m.SetFloat("_SkinCoatCoverage",1); m.SetFloat("_SkinLiquidCoatNormal",1);
                }
                if(preset.Contains("coat")) m.SetFloat("_ClearCoat",1);
            }
            return m;
        }
        void Shuffle()
        {
            order.Clear(); order.AddRange(report.cases);
            for(int i=order.Count-1;i>0;i--) { int n=random.Next(i+1); var c=order[n]; order[n]=order[i]; order[i]=c; }
        }
        void Configure(SkinCase c)
        {
            face.SetActive(c.layout=="face-close");
            for(int n=0;n<bodies.Count;n++)
            {
                bodies[n].SetActive(c.layout.StartsWith("body") && n<c.bodies);
                bodies[n].transform.position=new Vector3(c.bodies==4?(n-1.5f)*.9f:0,0,0);
            }
            camera.orthographicSize=c.layout=="face-close"?.55f:1;
            camera.transform.position=new Vector3(0,c.layout=="face-close"?0:.8f,-5); camera.transform.rotation=Quaternion.identity;
            for(int n=0;n<lights.Count;n++) lights[n].enabled=n<c.lights;
            foreach(Renderer r in surfaces) r.sharedMaterial=c.material;
        }
        void Preview(SkinCase c)
        {
            var read=new Texture2D(1920,1080,TextureFormat.RGBAFloat,false,true); var png=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                read.ReadPixels(new Rect(0,0,1920,1080),0,0); read.Apply(); var pixels=read.GetPixels();
                for(int i=0;i<pixels.Length;i++)
                {
                    Color p=pixels[i]; if(float.IsNaN(p.r+p.g+p.b) || float.IsInfinity(p.r+p.g+p.b)) throw new Exception("Nonfinite Skin GPU preview.");
                    if(p.r+p.g+p.b>.01f) c.nonblackPixels++;
                    pixels[i]=new Color(Mathf.LinearToGammaSpace(p.r/(1+p.r)),Mathf.LinearToGammaSpace(p.g/(1+p.g)),Mathf.LinearToGammaSpace(p.b/(1+p.b)),1);
                }
                if(c.bodies>0 && c.nonblackPixels<1000) throw new Exception("Blank Skin GPU case "+c.id);
                png.SetPixels(pixels); png.Apply(); File.WriteAllBytes(Path.Combine(output,c.id+".png"),png.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(read); Object.DestroyImmediate(png); }
        }
        public bool Tick()
        {
            SkinCase c=order[block];
            if(frame==0)
            {
                Configure(c);
                if(EditorUtility.DisplayCancelableProgressBar("SkinX GPU baseline",c.id+" round "+(round+1),report.completedBlocks/(float)report.totalBlocks))
                    throw new OperationCanceledException("Skin GPU benchmark cancelled.");
            }
            int warm=round==0?8:4; bool diagnostic=round==0 && frame==warm-1;
            timer.Statistics(diagnostic); camera.Render(); RenderTexture.active=target;
            fence.ReadPixels(new Rect(0,0,1,1),0,0); fence.Apply(false);
            double ms=timer.ReadResult(statistics,diagnostic);
            if(diagnostic)
            {
                c.psInvocations=statistics.psInvocations; c.primitives=statistics.primitives;
                if(c.bodies>0 && (c.psInvocations==0 || c.primitives==0)) throw new Exception("Missing Skin GPU geometry "+c.id);
                Preview(c);
            }
            if(frame>=warm) c.gpuSamplesMs.Add(ms);
            if(++frame>=warm+20)
            {
                var sorted=new List<double>(c.gpuSamplesMs); sorted.Sort(); c.medianMs=sorted[sorted.Count/2];
                c.p10Ms=sorted[(sorted.Count-1)/10]; c.p90Ms=sorted[(sorted.Count-1)*9/10];
                frame=0; block++; report.completedBlocks++; Save();
                if(block==order.Count)
                {
                    block=0; if(++round==3) { report.status="complete"; report.finishedUtc=DateTime.UtcNow.ToString("o"); Save(); return true; }
                    Shuffle();
                }
            }
            return false;
        }
        void Save() { File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true)); }
        public void Fail(Exception e) { report.status="failed"; report.error=e.ToString(); report.finishedUtc=DateTime.UtcNow.ToString("o"); Save(); }
        public void Dispose()
        {
            if(disposed) return; disposed=true;
            if(report.status=="running") Fail(new OperationCanceledException("Skin GPU benchmark interrupted."));
            EditorUtility.ClearProgressBar(); if(camera) camera.RemoveAllCommandBuffers();
            if(timer!=null)
            {
                using(var release=new CommandBuffer()) { release.IssuePluginEvent(timer.callback,2); Graphics.ExecuteCommandBuffer(release); }
                RenderTexture.active=target; fence.ReadPixels(new Rect(0,0,1,1),0,0); fence.Apply(false); timer.Dispose();
            }
            if(begin!=null) begin.Release(); if(end!=null) end.Release(); RenderTexture.active=oldTarget;
            if(scene.IsValid()) EditorSceneManager.CloseScene(scene,true); if(oldScene.IsValid()) SceneManager.SetActiveScene(oldScene);
            foreach(var pair in masks) if(pair.Key) pair.Key.cullingMask=pair.Value; QualitySettings.pixelLightCount=oldPixelLights;
            foreach(var o in owned) if(o) Object.DestroyImmediate(o);
        }
    }
    public static string StartSkin()
    {
        if(current!=null || skinCurrent!=null || EditorApplication.isPlaying) throw new Exception("A benchmark is running or Editor is in Play Mode.");
        if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11) throw new Exception("Skin benchmark requires D3D11 timestamps; no CPU substitute.");
        skinCurrent=new SkinRun(); EditorApplication.update+=TickSkin; AssemblyReloadEvents.beforeAssemblyReload+=StopSkin;
        return "Started Skin GPU benchmark. Wait for report.status=complete; acknowledgement is not completion. "+skinCurrent.output;
    }
    static void TickSkin()
    {
        try { if(skinCurrent.Tick()) StopSkin(); }
        catch(Exception e) { skinCurrent.Fail(e); UnityEngine.Debug.LogException(e); StopSkin(); }
    }
    static void StopSkin()
    {
        EditorApplication.update-=TickSkin; AssemblyReloadEvents.beforeAssemblyReload-=StopSkin;
        if(skinCurrent!=null) { skinCurrent.Dispose(); skinCurrent=null; }
    }
}

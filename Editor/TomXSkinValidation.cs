using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit Bridge fixtures. All scene objects and runtime textures are temporary.
public static class TomXSkinValidation
{
    const int Size = 384;
    [Serializable] sealed class Check { public string name; public double value; public bool passed; }
    [Serializable] sealed class Shot
    {
        public string name;
        public float mean, peak;
        public int nonFinite;
        [NonSerialized] public Color[] pixels;
    }
    [Serializable] sealed class Report
    {
        public string unity, gpu, api, colorSpace;
        public string scope = "Synthetic UV/channel and sphere fixtures, not a live KKS character. Simulates known game property writes; game card/expression acceptance remains manual.";
        public string display = "Unmodified float pixels for assertions; PNG uses fixed x1.5 exposure, Reinhard and display gamma. No per-shot normalization.";
        public List<Shot> shots = new List<Shot>();
        public List<Check> checks = new List<Check>();
    }
    sealed class Fixture : IDisposable
    {
        public readonly Report report;
        public readonly string output;
        public Camera camera;
        public Light sun, point, spot;
        public Renderer subject;
        public GameObject sphere, quad;
        public Mesh quadMesh;
        public int layer;
        readonly Scene previous, scene;
        readonly RenderTexture previousTarget, target;
        readonly int oldPixelLights;
        readonly ShadowQuality oldShadows;
        readonly float oldShadowDistance;
        readonly float oldLineWidth;
        readonly List<Object> owned = new List<Object>();
        readonly Dictionary<Light, int> masks = new Dictionary<Light, int>();
        readonly Texture2D read, png;
        public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public Texture2D Solid(Color value) { return Own(TomXCoatEyeValidation.Solid(value)); }
        public Fixture()
        {
            previous = SceneManager.GetActiveScene(); previousTarget = RenderTexture.active;
            oldPixelLights = QualitySettings.pixelLightCount; oldShadows = QualitySettings.shadows;
            oldShadowDistance = QualitySettings.shadowDistance;
            oldLineWidth = Shader.GetGlobalFloat("_linewidthG");
            var used = new HashSet<int>(); foreach (GameObject g in Object.FindObjectsOfType<GameObject>()) used.Add(g.layer);
            for (layer = 31; layer >= 8 && used.Contains(layer); layer--) { }
            if (layer < 8) throw new Exception("No free fixture layer.");
            foreach (Light l in Object.FindObjectsOfType<Light>()) { masks[l] = l.cullingMask; l.cullingMask &= ~(1 << layer); }
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CodexBridge/Reports/XSkin-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString() };
            QualitySettings.pixelLightCount = 8; QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 15;
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black; RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflection = Own(TomXCoatEyeValidation.Environment()); RenderSettings.reflectionIntensity = 1;
            camera = new GameObject("Skin contract camera").AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << layer; camera.renderingPath = RenderingPath.Forward;
            camera.orthographic = true; camera.orthographicSize = .62f; camera.allowHDR = true; camera.allowMSAA = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.nearClipPlane = .05f; camera.farClipPlane = 20;
            camera.transform.position = new Vector3(0, 0, -3); camera.transform.LookAt(Vector3.zero);
            target = Own(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
            target.Create(); camera.targetTexture = target;
            read = Own(new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true));
            png = Own(new Texture2D(Size, Size, TextureFormat.RGB24, false));
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.layer = layer;
            sphere.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            quad = new GameObject("Four independent UV sets"); quad.layer = layer;
            quadMesh = Own(new Mesh()); quadMesh.name = "Skin contract UV and vertex color probe";
            quadMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0) };
            quadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quadMesh.tangents = new[] { new Vector4(1,0,0,-1), new Vector4(1,0,0,-1), new Vector4(1,0,0,-1), new Vector4(1,0,0,-1) };
            quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            quadMesh.uv2 = RepeatUV(new Vector2(.2f,.8f)); quadMesh.uv3 = RepeatUV(new Vector2(.7f,.3f)); quadMesh.uv4 = RepeatUV(new Vector2(.4f,.6f));
            Colors(Color.white); quadMesh.triangles = new[] { 0,2,1, 1,2,3 }; quadMesh.RecalculateBounds();
            quad.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            quad.AddComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            sun = Light("Key", LightType.Directional); sun.transform.rotation = Quaternion.Euler(25,-30,0); sun.intensity = 1;
            point = Light("Point", LightType.Point); point.transform.position = new Vector3(.5f,.3f,-1.5f); point.range = 4; point.intensity = 1.5f; point.enabled = false;
            spot = Light("Spot", LightType.Spot); spot.transform.position = new Vector3(-.5f,.2f,-1.5f); spot.transform.LookAt(Vector3.zero); spot.range = 4; spot.spotAngle = 95; spot.intensity = 2; spot.enabled = false;
            UseQuad(false);
        }
        Light Light(string name, LightType type)
        {
            var l = new GameObject(name).AddComponent<Light>(); l.type = type; l.cullingMask = 1 << layer;
            l.renderMode = LightRenderMode.ForcePixel; l.shadows = LightShadows.None; return l;
        }
        public void Colors(Color c) { quadMesh.colors = new[] { c,c,c,c }; }
        public void UseQuad(bool use)
        {
            sphere.SetActive(!use); quad.SetActive(use); subject = (use ? quad : sphere).GetComponent<Renderer>();
        }
        public Material Material(string name = "SkinX")
        {
            var shader = Shader.Find("tom/" + name);
            if (!shader || !shader.isSupported) throw new Exception("Unsupported fixture shader " + name);
            var m = Own(new Material(shader));
            m.SetColor("_BaseColor", new Color(.65f,.38f,.29f,1)); m.SetFloat("_UseRamp",0);
            m.SetFloat("_SpecularStrength",0); m.SetFloat("_LightProbeBlend",0); m.SetFloat("_CustomSHVolumeBlend",0);
            m.SetFloat("_VertexLightIntensity",0); m.SetColor("_AmbientColor", new Color(.12f,.12f,.12f,1));
            m.SetFloat("_IndirectDiffuseIntensity",1); // Fixed regression exposure; defaults are tested separately.
            m.SetFloat("_CullOption",0);
            if (name == "SkinX")
            {
                m.SetTexture("_DetailMask", Solid(new Color(1,0,0,1)));
                m.SetTexture("_SkinControlMap", Solid(new Color(1,1,0,1)));
                m.SetTexture("_overtex1", Solid(Color.clear)); m.SetTexture("_overtex2", Solid(Color.clear)); m.SetTexture("_overtex3", Solid(Color.clear));
            }
            return m;
        }
        public Shot Capture(string name, Material m)
        {
            subject.sharedMaterial = m; camera.Render(); camera.Render(); RenderTexture.active = target;
            read.ReadPixels(new Rect(0,0,Size,Size),0,0); read.Apply(false);
            var s = new Shot { name = name, pixels = read.GetPixels() };
            double total = 0; var display = new Color[s.pixels.Length];
            for (int n=0;n<s.pixels.Length;n++)
            {
                var p = s.pixels[n];
                if (!Finite(p.r) || !Finite(p.g) || !Finite(p.b) || !Finite(p.a)) { s.nonFinite++; continue; }
                float l = (p.r+p.g+p.b)/3; total += l; s.peak = Mathf.Max(s.peak,l); display[n] = Display(p);
            }
            s.mean = (float)(total/s.pixels.Length); report.shots.Add(s);
            png.SetPixels(display); png.Apply(); File.WriteAllBytes(Path.Combine(output,name+".png"),png.EncodeToPNG());
            Check("Finite: " + name, s.nonFinite, s.nonFinite == 0);
            return s;
        }
        public void Check(string name, double value, bool passed) { report.checks.Add(new Check { name=name, value=value, passed=passed }); }
        public void Same(string name, Shot a, Shot b, double tolerance = .00001) { double d = Difference(a,b); Check(name,d,d<tolerance); }
        public void Different(string name, Shot a, Shot b, double tolerance = .00001) { double d = Difference(a,b); Check(name,d,d>tolerance); }
        public void Center(string name, Shot s, Color expected, double tolerance = .003)
        {
            Color p=s.pixels[(Size/2)*Size+Size/2]; double d=Math.Max(Math.Abs(p.r-expected.r),Math.Max(Math.Abs(p.g-expected.g),Math.Abs(p.b-expected.b)));
            Check(name,d,d<tolerance);
        }
        public void Sheet(string name, params Shot[] shots)
        {
            var sheet = Own(new Texture2D(Size*shots.Length,Size,TextureFormat.RGB24,false));
            for (int n=0;n<shots.Length;n++) { var p=new Color[Size*Size]; for(int j=0;j<p.Length;j++) p[j]=Display(shots[n].pixels[j]); sheet.SetPixels(n*Size,0,Size,Size,p); }
            sheet.Apply(); File.WriteAllBytes(Path.Combine(output,name+".png"),sheet.EncodeToPNG());
        }
        public void Save() { File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true)); }
        public void Dispose()
        {
            RenderTexture.active=previousTarget; EditorSceneManager.CloseScene(scene,true);
            if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach(var p in masks) if(p.Key) p.Key.cullingMask=p.Value;
            QualitySettings.pixelLightCount=oldPixelLights; QualitySettings.shadows=oldShadows; QualitySettings.shadowDistance=oldShadowDistance;
            Shader.SetGlobalFloat("_linewidthG",oldLineWidth);
            foreach(var o in owned) if(o) Object.DestroyImmediate(o);
        }
    }
    static Vector2[] RepeatUV(Vector2 uv) { return new[] {uv,uv,uv,uv}; }
    static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    static Color Display(Color p) { return new Color(Tone(p.r),Tone(p.g),Tone(p.b),1); }
    static float Tone(float x) { x=Mathf.Max(x,0)*1.5f; return Mathf.Pow(x/(1+x),1/2.2f); }
    static double Difference(Shot a,Shot b)
    {
        double sum=0; for(int i=0;i<a.pixels.Length;i++) { var x=a.pixels[i]-b.pixels[i]; sum+=Math.Abs(x.r)+Math.Abs(x.g)+Math.Abs(x.b); }
        return sum/(a.pixels.Length*3);
    }
    static Texture2D Texture(Fixture f, Func<float,float,Color> pixel, int size=64)
    {
        var t=f.Own(new Texture2D(size,size,TextureFormat.RGBA32,false,true)); t.wrapMode=TextureWrapMode.Clamp; t.filterMode=FilterMode.Point;
        var pixels=new Color[size*size]; for(int y=0;y<size;y++)for(int x=0;x<size;x++) pixels[y*size+x]=pixel((x+.5f)/size,(y+.5f)/size);
        t.SetPixels(pixels); t.Apply(); return t;
    }

    public static string Run()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play Mode before SkinX fixtures.");
        TomXSkinBuild.Validate();
        using(var f=new Fixture())
        {
            try
            {
                Material skin=f.Material(), opaque=f.Material("MainOpaqueX");
                Shot dry=f.Capture("skin-neutral",skin);
                f.Same("Neutral Skin equals X opaque with matching states",dry,f.Capture("opaque-reference",opaque));
                skin.SetFloat("_SkinStrength",1); skin.SetFloat("_SkinWrap",.6f);
                Shot soft=f.Capture("skin-soft",skin); f.Different("Soft front-side response",dry,soft);
                skin.SetFloat("_SkinWarmth",1); Shot warm=f.Capture("skin-soft-warm",skin); f.Different("Warm transition",soft,warm);
                skin.SetFloat("_ClearCoat",1); skin.SetFloat("_ClearCoatRoughness",.12f);
                Shot coated=f.Capture("skin-soft-coat",skin); f.Different("Shared coat visible",warm,coated);
                f.Sheet("skin-overview",dry,soft,warm,coated);
                skin.SetFloat("_ClearCoat",0); skin.SetFloat("_ClearCoatIOR",2.5f);
                f.Same("Coat IOR has no effect with coat off",warm,f.Capture("skin-coat-off-high-ior",skin));

                TestIndirectDefault(f); TestColorInheritance(f); TestLayers(f); TestMasks(f); TestSkinControls(f); TestLiquid(f); TestLighting(f); TestCoverage(f);
                TomXSkinBuild.Validate();
                f.Save(); int failed=f.report.checks.FindAll(c=>!c.passed).Count;
                if(failed>0) throw new Exception(failed+" Skin checks failed; inspect "+f.output);
                return f.report.checks.Count+" checks passed; "+f.report.shots.Count+" GPU captures. "+f.output;
            }
            finally { f.Save(); }
        }
    }

    static void TestIndirectDefault(Fixture f)
    {
        var entries = new List<string>(TomXCoatEyeBuild.Entries) { "SkinX" };
        foreach (string name in entries)
        {
            var defaults = f.Own(new Material(Shader.Find("tom/"+name)));
            float value = defaults.GetFloat("_IndirectDiffuseIntensity");
            f.Check(name+" indirect authoring default is 0.25",value,Mathf.Approximately(value,.25f));
            defaults.SetFloat("_IndirectDiffuseIntensity",1.7f);
            var saved = f.Own(new Material(defaults));
            Shader original = saved.shader;
            saved.shader = Shader.Find(name == "SkinX" ? "tom/MainOpaqueX" : "tom/SkinX");
            saved.shader = original;
            value = saved.GetFloat("_IndirectDiffuseIntensity");
            f.Check(name+" explicit override survives copy and shader reassignment",value,Mathf.Approximately(value,1.7f));
        }

        f.UseQuad(true); var m=f.Material();
        m.SetColor("_AmbientColor",new Color(.8f,.6f,.4f,1));
        m.SetFloat("_MainLightIntensity",0); m.SetFloat("_AdditionalLightIntensity",0);
        Shot previous=f.Capture("indirect-old-one",m);
        m.SetFloat("_IndirectDiffuseIntensity",.25f);
        Shot reduced=f.Capture("indirect-default-quarter",m);
        f.Check("Indirect-only output reduces to one quarter",reduced.mean/previous.mean,
            previous.mean>.001f && Mathf.Abs(reduced.mean/previous.mean-.25f)<.001f);
        f.Center("Indirect diffuse uses quarter-strength preset",reduced,new Color(.65f*.8f*.25f,.38f*.6f*.25f,.29f*.4f*.25f,1));
        f.Sheet("indirect-default-comparison",previous,reduced);
        m.SetFloat("_SkinDebugView",1); Shot albedo=f.Capture("indirect-quarter-albedo",m);
        m.SetFloat("_IndirectDiffuseIntensity",1);
        f.Same("Indirect preset does not change skin albedo",albedo,f.Capture("indirect-one-albedo",m));

        m.SetFloat("_SkinDebugView",0); m.SetColor("_AmbientColor",Color.black);
        m.SetFloat("_MainLightIntensity",1); m.SetFloat("_AdditionalLightIntensity",1);
        f.point.enabled=true; f.UseQuad(false);
        Shot direct=f.Capture("indirect-one-direct-only",m);
        m.SetFloat("_IndirectDiffuseIntensity",.25f);
        f.Same("Indirect preset leaves Base and Add direct light unchanged",direct,f.Capture("indirect-quarter-direct-only",m));
        m.SetFloat("_ClearCoat",1); m.SetFloat("_ClearCoatDebugView",2);
        Shot coat=f.Capture("indirect-quarter-coat-environment",m);
        f.Check("Indirect-default fixture has visible coat reflection",coat.mean,coat.mean>.00001f);
        m.SetFloat("_IndirectDiffuseIntensity",1);
        f.Same("Indirect preset leaves coat environment unchanged",coat,f.Capture("indirect-one-coat-environment",m));
        m.SetFloat("_ClearCoat",0); m.SetFloat("_ClearCoatDebugView",0);
        m.SetFloat("_ReflectionMode",2); m.SetFloat("_DebugView",3);
        Shot reflection=f.Capture("indirect-one-reflection",m);
        f.Check("Indirect-default fixture has visible substrate reflection",reflection.mean,reflection.mean>.00001f);
        m.SetFloat("_IndirectDiffuseIntensity",.25f);
        f.Same("Indirect preset leaves substrate reflection unchanged",reflection,f.Capture("indirect-quarter-reflection",m));
        f.point.enabled=false;
    }

    static void TestColorInheritance(Fixture f)
    {
        f.UseQuad(true);
        var skin = Shader.Find("tom/SkinX");
        string[] paths = {
            "Assets/Shaders/Shader Forge_main_skin.shader",
            "Assets/Mods/KKShadersPlus-release1.7.1/Shaders/Skin/SkinPlus.shader"
        };
        Color baked = new Color(.56f,.31f,.18f,1);
        Color tint = new Color(.8f,.6f,.4f,1);
        var texture = f.Own(new RenderTexture(4,4,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear));
        texture.Create();
        var oldTarget = RenderTexture.active;
        try { RenderTexture.active = texture; GL.Clear(false,true,baked); }
        finally { RenderTexture.active = oldTarget; }

        for (int sourceIndex=0; sourceIndex<paths.Length; sourceIndex++)
        {
            var source = AssetDatabase.LoadAssetAtPath<Shader>(paths[sourceIndex]);
            if (!source) throw new Exception("Missing read-only skin reference: " + paths[sourceIndex]);
            string prefix = sourceIndex == 0 ? "inherit-stock" : "inherit-vplus";
            var m = f.Own(new Material(source));
            m.SetTexture("_MainTex",texture);
            var scale = new Vector2(.8f,.9f); var offset = new Vector2(.1f,.05f);
            m.SetTextureScale("_MainTex",scale); m.SetTextureOffset("_MainTex",offset);
            m.SetTexture("_DetailMask",f.Solid(new Color(1,0,0,1)));
            for (int n=1; n<=3; n++)
            {
                m.SetTexture("_overtex"+n,f.Solid(Color.clear));
                m.SetColor("_overcolor"+n,Color.white);
            }
            bool hasTint = m.HasProperty("_Col0");
            if (hasTint) m.SetColor("_Col0",tint);

            // MaterialEditor changes the shader on the existing material, not a fresh carrier.
            m.shader = skin;
            f.Check(prefix+" preserves composed MainTex",0,m.GetTexture("_MainTex") == texture);
            f.Check(prefix+" preserves MainTex ST",0,m.GetTextureScale("_MainTex") == scale && m.GetTextureOffset("_MainTex") == offset);
            f.Check(prefix+" neutral extra BaseColor",0,m.GetColor("_BaseColor") == Color.white);
            f.Check(prefix+" preserves Col0 by property name",0,m.GetColor("_Col0") == (hasTint ? tint : Color.white));
            m.SetFloat("_SkinDebugView",1); m.SetFloat("_CullOption",0);
            Color expected = baked * (hasTint ? tint : Color.white);
            Shot inherited = f.Capture(prefix+"-albedo",m);
            f.Center(prefix+" renders inherited skin color",inherited,expected);

            m.SetColor(Shader.PropertyToID("_Col0"),new Color(.45f,.7f,.9f,1));
            expected = baked * new Color(.45f,.7f,.9f,1);
            f.Center(prefix+" named Col0 update",f.Capture(prefix+"-col0-update",m),expected);
            m.SetTexture("_overtex1",f.Solid(Color.white));
            m.SetColor(Shader.PropertyToID("_overcolor1"),new Color(.7f,.1f,.2f,.4f));
            expected = Color.Lerp(expected,new Color(.7f,.1f,.2f,1),.4f);
            f.Center(prefix+" named overlay color update",f.Capture(prefix+"-overlay-update",m),expected);

            Color extra = new Color(.9f,.8f,.7f,1);
            m.SetColor("_BaseColor",extra);
            f.Center(prefix+" extra tint remains multiplicative",f.Capture(prefix+"-extra-tint",m),expected*extra);
        }
    }

    static void TestLayers(Fixture f)
    {
        f.UseQuad(true); var m=f.Material(); m.SetColor("_BaseColor",Color.white); m.SetFloat("_SkinDebugView",1);
        var uv=Texture(f,(x,y)=>new Color(x,y,.1f,1));
        for(int n=1;n<=3;n++)
        {
            m.SetTexture("_overtex"+n,uv); m.SetColor("_overcolor"+n,Color.white);
            var s=f.Capture("overlay-"+n+"-own-uv",m);
            Vector2 expected=n==1?new Vector2(.2f,.8f):n==2?new Vector2(.7f,.3f):new Vector2(.4f,.6f);
            f.Center("Overlay "+n+" uses its UV",s,new Color(expected.x,expected.y,.1f,1),.02);
            m.SetColor("_overcolor"+n,Color.clear);
        }
        m.SetColor("_overcolor1",Color.white); f.Colors(new Color(0,1,1,1));
        f.Center("Vertex R gates overlay 1 UV",f.Capture("overlay-1-vertex-gate",m),new Color(0,0,.1f,1),.02);
        f.Colors(Color.white); m.SetFloat("_nip",1); m.SetFloat("_nipsize",0);
        Shot small=f.Capture("overlay-nipple-size-zero",m); m.SetFloat("_nipsize",1);
        f.Different("Localized nipple remapping",small,f.Capture("overlay-nipple-size-one",m));
        m.SetFloat("_nip",0); m.SetColor("_overcolor1",Color.clear); m.SetColor("_overcolor2",Color.white);
        f.Colors(new Color(1,1,0,1)); f.Center("Vertex B gates overlay 2 UV",f.Capture("overlay-2-vertex-gate",m),new Color(0,0,.1f,1),.02); f.Colors(Color.white);
        m.SetColor("_overcolor2",Color.clear);
        for(int n=1;n<=3;n++) { m.SetTexture("_overtex"+n,f.Solid(n==1?Color.red:n==2?Color.green:Color.blue)); m.SetColor("_overcolor"+n,new Color(1,1,1,.5f)); }
        f.Center("Ordered RGBA overlays",f.Capture("overlay-ordered",m),new Color(.25f,.375f,.625f,1));
        for(int n=1;n<=3;n++) m.SetColor("_overcolor"+n,Color.clear);
        m.SetTexture("_ColMask",f.Solid(new Color(.5f,.5f,1,1))); m.SetColor("_Col3",new Color(.1f,.3f,.7f,1));
        f.Center("Sequential extra tint blue has final priority",f.Capture("color-region-order",m),new Color(.1f,.3f,.7f,1));
        m.SetTexture("_ColMask",null); m.SetTexture("_overtex1",f.Solid(new Color(.5f,.25f,0,1))); m.SetColor("_overcolor1",Color.white); m.SetFloat("_tex1mask",1); m.SetFloat("_nip_specular",1);
        f.Center("Legacy overlay 1 RG encoding",f.Capture("overlay-rg-encoding",m),new Color(.543f,.543f,.543f,1),.007);
        m.SetColor("_overcolor1",Color.clear); m.SetTexture("_overtex2",f.Solid(Color.red));
        m.SetColor("_overcolor2",new Color(1,1,1,0)); Shot noBlush=f.Capture("blush-zero",m);
        m.SetColor("_overcolor2",new Color(1,1,1,.2f)); f.Different("Runtime blush alpha write",noBlush,f.Capture("blush-point-two",m));
        m.SetTextureScale("_overtex3",new Vector2(.5f,.5f)); m.SetTextureOffset("_overtex3",new Vector2(.1f,.2f)); m.SetTexture("_overtex3",uv); m.SetColor("_overcolor3",Color.white);
        f.Center("Overlay independent ST",f.Capture("overlay-3-st",m),new Color(.3f,.5f,.1f,1),.02);
    }

    static void TestMasks(Fixture f)
    {
        var m=f.Material(); m.SetFloat("_SkinDebugView",6); m.SetFloat("_SpecularPower",.2f); m.SetFloat("_SpecularPowerNail",.8f);
        foreach(float a in new[]{0f,.5f,1f})
        {
            m.SetTexture("_DetailMask",f.Solid(new Color(1,0,0,a)));
            float expected=Mathf.Max(a*.2f,(1-a)*.8f);
            f.Center("Regional gloss "+a,f.Capture("gloss-region-"+a,m),new Color(expected,expected,expected,1));
        }
        m.SetFloat("_SpecularPower",1); f.Center("Runtime skin gloss write",f.Capture("gloss-game-update",m),Color.white);
        m.SetFloat("_SkinDebugView",13); m.SetColor("_ShadowColor",new Color(.4f,.5f,.6f,1));
        m.SetTexture("_DetailMask",f.Solid(new Color(1,1,0,1))); f.Center("Detail G painted shade",f.Capture("detail-g-shade",m),new Color(.4f,.5f,.6f,1));
        m.SetTexture("_DetailMask",f.Solid(new Color(1,0,0,1))); m.SetTexture("_LineMask",f.Solid(Color.blue));
        f.Center("Line B painted shade",f.Capture("line-b-shade",m),new Color(.4f,.5f,.6f,1));
        m.SetTexture("_LineMask",f.Solid(Color.red)); m.SetColor("_SkinLineColor",Color.black);
        f.Center("Line R attenuation",f.Capture("line-r-attenuation",m),new Color(.5f,.5f,.5f,1));
        Shader.SetGlobalFloat("_linewidthG",.5f); m.SetTexture("_LineMask",f.Solid(Color.green));
        f.Center("Line G exponent",f.Capture("line-g-exponent",m),new Color(.6f,.6f,.6f,1));
        m.SetFloat("_linetexon",0); f.Center("Line off",f.Capture("line-disabled",m),Color.white);
        m.SetTexture("_LineMask",null); m.SetFloat("_SkinDebugView",5); m.SetTexture("_NormalMapDetail",Texture(f,(x,y)=>new Color(1,.8f,1,.8f)));
        Shot full=f.Capture("diffuse-normal-full",m); m.SetFloat("_SkinDiffuseNormalDetail",0);
        f.Different("Diffuse-only detail reduction",full,f.Capture("diffuse-normal-main",m));
        m.SetFloat("_DebugView",2); m.SetFloat("_SkinDebugView",0); m.SetFloat("_SpecularStrength",1); m.SetFloat("_SkinDiffuseNormalDetail",1);
        Shot spec=f.Capture("specular-full-detail",m); m.SetFloat("_SkinDiffuseNormalDetail",0);
        f.Same("Diffuse reduction preserves specular normals",spec,f.Capture("specular-diffuse-reduced",m));
    }

    static void TestSkinControls(Fixture f)
    {
        f.UseQuad(false); var skin=f.Material(); var opaque=f.Material("MainOpaqueX");
        skin.SetFloat("_UseRamp",1); opaque.SetFloat("_UseRamp",1);
        f.Same("Neutral skin also matches procedural Toon",f.Capture("neutral-toon-skin",skin),f.Capture("neutral-toon-opaque",opaque));
        skin.SetFloat("_SkinStrength",1); skin.SetFloat("_SkinWrap",1); skin.SetFloat("_SkinWarmth",1);
        skin.SetTexture("_SkinControlMap",f.Solid(Color.black));
        f.Same("Control R zero restores neutral skin",f.Capture("control-soft-zero",skin),f.Capture("control-reference",opaque));
        skin.SetTexture("_SkinControlMap",f.Solid(Color.red)); Shot noWarm=f.Capture("control-warm-zero",skin);
        skin.SetFloat("_SkinWarmth",0); f.Same("Control G zero disables warmth",noWarm,f.Capture("warm-slider-zero",skin));

        var m=f.Material(); f.UseQuad(true);
        m.SetTexture("_NormalMap",Texture(f,(x,y)=>new Color(1,.7f,1,.7f)));
        m.SetTexture("_NormalMask",f.Solid(Color.green)); m.SetFloat("_SkinDebugView",5);
        Shot mapped=f.Capture("face-normal-mapped",m); m.SetFloat("_SkinFaceNormalStrength",1);
        Shot softened=f.Capture("face-normal-geometric",m); f.Different("NormalMask G affects diffuse only when enabled",mapped,softened);
        f.Center("Face mask can reach geometric normal",softened,new Color(.5f,.5f,0,1));
        m.SetFloat("_SkinDebugView",0); m.SetFloat("_DebugView",2); m.SetFloat("_SpecularStrength",1);
        Shot spec=f.Capture("face-specular-soft",m); m.SetFloat("_SkinFaceNormalStrength",0);
        f.Same("Face art softening retains specular",spec,f.Capture("face-specular-original",m));
        m.SetFloat("_SkinDebugView",5);
        f.quadMesh.tangents=new[] {new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1)};
        Shot mirrored=f.Capture("normal-mirrored-tangent",m);
        Color a=mapped.pixels[(Size/2)*Size+Size/2], b=mirrored.pixels[(Size/2)*Size+Size/2];
        double mirrorError=Math.Abs(a.r-b.r)+Math.Abs(a.b-b.b)+Math.Abs(a.g+b.g-1);
        f.Check("Mirrored tangent flips bitangent normal only",mirrorError,mirrorError<.003);
        f.quadMesh.tangents=new[] {new Vector4(1,0,0,-1),new Vector4(1,0,0,-1),new Vector4(1,0,0,-1),new Vector4(1,0,0,-1)};

        f.UseQuad(false); m=f.Material(); m.SetFloat("_SpecularStrength",1); m.SetFloat("_SpecularToonBlend",0); m.SetFloat("_DebugView",2);
        Shot full=f.Capture("detail-r-direct-full",m);
        m.SetTexture("_DetailMask",f.Solid(new Color(0,0,0,1))); m.SetFloat("_UseDetailRAsSpecularMap",1);
        Shot masked=f.Capture("detail-r-direct-masked",m);
        f.Check("Stationary Detail R masks direct highlight",masked.mean,full.mean>.0001 && masked.mean<.00001);
        m.SetFloat("_UseDetailRAsSpecularMap",0); m.SetFloat("_notusetexspecular",0);
        Shot movingOff=f.Capture("detail-r-moving-zero",m); f.Check("Moving R mask also gates lit highlight",movingOff.mean,movingOff.mean<.00001);
        m.SetTexture("_DetailMask",Texture(f,(x,y)=>new Color(x>.5f?1:0,0,0,1)));
        m.SetFloat("_SpeclarHeight",1); Shot centered=f.Capture("detail-r-moving-center",m);
        m.SetFloat("_SpeclarHeight",0); f.Different("Legacy view-shift moves the pattern",centered,f.Capture("detail-r-moving-shift",m));
        m.SetFloat("_notusetexspecular",1); m.SetFloat("_DebugView",3); m.SetFloat("_ReflectionMode",2);
        m.SetTexture("_DetailMask",f.Solid(new Color(1,0,0,1))); m.SetFloat("_SpecularPower",1);
        Shot probe=f.Capture("gloss-probe-full",m); m.SetFloat("_SpecularPower",0);
        Shot noProbe=f.Capture("gloss-probe-zero",m); f.Check("Game regional gloss also gates substrate probes",noProbe.mean,probe.mean>.0001 && noProbe.mean<.00001);
        m.SetFloat("_DebugView",5); m.SetFloat("_RimStrength",1);
        Shot rim=f.Capture("rim-detail-b-zero",m); m.SetTexture("_DetailMask",f.Solid(new Color(1,0,1,1)));
        Shot noRim=f.Capture("rim-detail-b-one",m); f.Check("Detail B suppresses Rim",noRim.mean,rim.mean>.0001 && noRim.mean<.00001);
    }

    static void TestLiquid(Fixture f)
    {
        f.UseQuad(true); var m=f.Material(); m.SetFloat("_SkinDebugView",4); m.SetTexture("_Texture2",f.Solid(new Color(.25f,.75f,0,1)));
        string[] names={"_liquidftop","_liquidfbot","_liquidbtop","_liquidbbot","_liquidface"};
        Color[] regions={Color.red,Color.green,Color.blue,new Color(1,1,0,1),new Color(0,1,1,1)};
        for(int region=0;region<5;region++)
        {
            m.SetTexture("_liquidmask",f.Solid(regions[region]));
            foreach(float amount in new[]{0f,1f,2f})
            {
                m.SetFloat(names[region],amount); float expected=amount==0?0:amount==1?.25f:.75f;
                f.Center("Liquid region "+region+" amount "+amount,f.Capture("liquid-"+region+"-amount-"+amount,m),new Color(expected,expected,expected,1));
            }
            m.SetFloat(names[region],0);
        }
        m.SetTexture("_liquidmask",Texture(f,(x,y)=>x>.5f?Color.red:Color.black)); m.SetFloat("_liquidftop",2); m.SetTexture("_Texture2",f.Solid(Color.white));
        Shot mask=f.Capture("liquid-region-original",m); m.SetVector("_LiquidTiling",new Vector4(.3f,.8f,4,3));
        f.Same("Liquid tiling does not move regions",mask,f.Capture("liquid-region-tiled",m));
        m.SetTexture("_liquidmask",f.Solid(Color.red)); m.SetTexture("_Texture2",Texture(f,(x,y)=>new Color(x,y,0,1))); m.SetFloat("_liquidftop",1);
        m.SetVector("_LiquidTiling",new Vector4(0,0,1,1)); Shot p=f.Capture("liquid-pattern-original",m);
        m.SetVector("_LiquidTiling",new Vector4(.3f,0,.3f,1)); f.Different("Liquid pattern has independent transform",p,f.Capture("liquid-pattern-transform",m));
        m.SetTexture("_Texture2",f.Solid(Color.white)); m.SetFloat("_SkinDebugView",0); f.UseQuad(false);
        Shot liquid=f.Capture("liquid-coat-off",m); m.SetFloat("_SkinCoatCoverage",3); m.SetFloat("_SkinWetness",1); m.SetFloat("_ClearCoatIOR",2.5f);
        f.Same("Liquid/wetness never enables coat",liquid,f.Capture("liquid-coat-still-off",m));
        m.SetFloat("_ClearCoat",1); m.SetFloat("_ClearCoatDebugView",4);
        m.SetFloat("_SkinCoatCoverage",1); f.Center("Liquid coat coverage",f.Capture("coat-liquid-coverage",m),Color.white);
        m.SetFloat("_liquidftop",0); f.Center("No liquid no liquid coat",f.Capture("coat-liquid-empty",m),Color.black);
        m.SetTexture("_SkinControlMap",f.Solid(new Color(1,1,.4f,1))); m.SetFloat("_SkinCoatCoverage",2);
        f.Center("Authored wetness coverage",f.Capture("coat-wet-mask",m),new Color(.4f,.4f,.4f,1));
        m.SetFloat("_ClearCoatDebugView",0); m.SetFloat("_SkinCoatCoverage",1); m.SetFloat("_liquidftop",2);
        Shot coated=f.Capture("liquid-coated",m); m.SetTexture("_Texture3",Texture(f,(x,y)=>new Color(1,.75f,1,.65f))); m.SetFloat("_SkinLiquidCoatNormal",1);
        f.Different("Liquid normal reaches surface and optional coat",coated,f.Capture("liquid-coated-normal",m));
    }

    static void TestLighting(Fixture f)
    {
        f.UseQuad(false); var m=f.Material(); m.SetFloat("_SkinStrength",1); m.SetFloat("_SkinWrap",.7f); m.SetFloat("_SkinWarmth",1);
        Shot key=f.Capture("lighting-key",m); f.point.enabled=f.spot.enabled=true;
        Shot multi=f.Capture("lighting-multi",m); f.Different("Additional lights reach skin",key,multi);
        m.SetFloat("_DebugView",3); m.SetFloat("_ReflectionMode",2); Shot env=f.Capture("environment-many-lights",m);
        f.point.enabled=f.spot.enabled=false; f.Same("Probe environment Base only",env,f.Capture("environment-key-only",m));
        m.SetFloat("_DebugView",0); m.SetFloat("_ReflectionMode",0); m.SetFloat("_ClearCoat",1); m.SetFloat("_ClearCoatDebugView",2);
        Shot coatEnv=f.Capture("coat-environment-key",m); f.point.enabled=true;
        f.Same("Coat environment Base only",coatEnv,f.Capture("coat-environment-add",m));
        f.point.enabled=false; m.SetFloat("_ClearCoat",0); m.SetFloat("_ClearCoatDebugView",0);
        f.sun.shadows=LightShadows.Hard;
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.layer=f.layer;
        blocker.transform.position=new Vector3(.35f,.32f,-.85f); blocker.transform.localScale=Vector3.one*.5f;
        blocker.GetComponent<Renderer>().sharedMaterial=f.Material("MainOpaqueX"); blocker.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
        f.Different("External blocker still shadows softened skin",key,f.Capture("lighting-blocked",m)); blocker.SetActive(false); f.sun.shadows=LightShadows.None;
        m.SetFloat("_MainLightIntensity",0); m.SetFloat("_IndirectDiffuseIntensity",0); f.spot.enabled=true;
        Shot spot=f.Capture("lighting-spot",m); f.spot.cookie=f.Solid(Color.clear);
        f.Different("Spot cookie respected",spot,f.Capture("lighting-spot-blocked-cookie",m)); f.spot.cookie=null; f.spot.enabled=false;
        m.SetFloat("_MainLightIntensity",1); m.SetFloat("_SkinStrength",0); m.SetTexture("_LineMask",f.Solid(Color.red)); m.SetColor("_SkinLineColor",Color.black);
        m.SetFloat("_linetexon",0); Shot basePlain=f.Capture("lines-base-plain",m); f.point.enabled=true; Shot addPlain=f.Capture("lines-add-plain",m);
        m.SetFloat("_linetexon",1); Shot addLines=f.Capture("lines-add-enabled",m); f.point.enabled=false; Shot baseLines=f.Capture("lines-base-enabled",m);
        double error=0; for(int n=0;n<basePlain.pixels.Length;n++)
        {
            Color a=(addPlain.pixels[n]-basePlain.pixels[n])*.5f;
            Color b=addLines.pixels[n]-baseLines.pixels[n]; error+=Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
        }
        error/=basePlain.pixels.Length*3; f.Check("Line factor identical in Base and Add",error,error<.0001);
    }

    static void TestCoverage(Fixture f)
    {
        f.UseQuad(true); var m=f.Material(); m.SetColor("_BaseColor",Color.white); m.SetFloat("_SkinDebugView",1);
        m.SetTexture("_AlphaMask",Texture(f,(x,y)=>new Color(x>.5f?1:0,y>.5f?1:0,0,1)));
        foreach(int r in new[]{0,1})foreach(int g in new[]{0,1})
        {
            m.SetFloat("_alpha_a",r); m.SetFloat("_alpha_b",g); Shot s=f.Capture("clothing-r"+r+"-g"+g,m);
            foreach(int x in new[]{Size/3,Size*2/3})foreach(int y in new[]{Size/3,Size*2/3})
            {
                float expected=(r==0||x>Size/2)&&(g==0||y>Size/2)?1:0;
                double error=Math.Abs(s.pixels[y*Size+x].r-expected); f.Check("Clothing RG "+r+g+" at "+x+","+y,error,error<.003);
            }
        }
        m.SetFloat("_alpha_a",0); m.SetFloat("_alpha_b",0); m.SetTexture("_MainTex",f.Solid(new Color(1,1,1,0)));
        f.Center("MainTex alpha ignored by default",f.Capture("main-alpha-ignored",m),Color.white);
        m.SetFloat("_SkinMainAlphaClip",1); Shot empty=f.Capture("main-alpha-clipped",m); f.Check("Main alpha opt-in clips",empty.mean,empty.mean<.00001);
        m.SetTexture("_MainTex",null); m.SetFloat("_SkinMainAlphaClip",0); m.SetTexture("_AlphaMask",f.Solid(Color.black)); m.SetFloat("_alpha_a",1);
        m.SetFloat("_SkinDebugView",0); m.SetFloat("_OutlineOn",1); m.SetFloat("_OutlineWidth",6); m.SetColor("_OutlineColor",Color.green); f.UseQuad(false); f.point.enabled=true;
        Shot hidden=f.Capture("all-color-passes-clipped",m); f.Check("Base Add Outline all obey clothing mask",hidden.mean,hidden.mean<.00001);
        m.SetFloat("_alpha_a",0); Shot outline=f.Capture("outline-visible",m); m.SetTexture("_DetailMask",f.Solid(new Color(1,0,1,1)));
        f.Different("Detail B suppresses shell width",outline,f.Capture("outline-detail-b-suppressed",m));
        f.point.enabled=false; m.SetFloat("_OutlineOn",0); m.SetTexture("_DetailMask",f.Solid(new Color(1,0,0,1)));
        f.UseQuad(true); f.quad.transform.localPosition=new Vector3(.3f,.3f,-.7f); f.subject.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
        var receiver=GameObject.CreatePrimitive(PrimitiveType.Quad); receiver.layer=f.layer; receiver.transform.localScale=Vector3.one*1.2f;
        receiver.GetComponent<Renderer>().sharedMaterial=f.Material("MainOpaqueX"); receiver.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        f.sun.transform.rotation=Quaternion.identity; f.sun.shadows=LightShadows.Hard;
        Shot shadow=f.Capture("clothing-shadow-present",m); m.SetFloat("_alpha_a",1); Shot noShadow=f.Capture("clothing-shadow-clipped",m);
        f.Different("ShadowCaster obeys clothing RG",shadow,noShadow);
        m.SetFloat("_alpha_a",0); m.SetFloat("_alpha_b",1);
        f.Same("ShadowCaster also obeys clothing G",noShadow,f.Capture("clothing-g-shadow-clipped",m));
        m.SetFloat("_alpha_b",0); m.SetTexture("_MainTex",f.Solid(new Color(1,1,1,0)));
        f.Same("Shadow ignores Main alpha by default",shadow,f.Capture("main-alpha-shadow-default",m));
        m.SetFloat("_SkinMainAlphaClip",1);
        f.Same("Shadow honors Main alpha opt-in",noShadow,f.Capture("main-alpha-shadow-clipped",m));
        receiver.SetActive(false); f.subject.shadowCastingMode=ShadowCastingMode.Off; f.quad.transform.localPosition=Vector3.zero; f.sun.shadows=LightShadows.None;
    }
}

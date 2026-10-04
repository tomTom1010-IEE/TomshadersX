using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Reproducible appearance atlas. No saved scene/material is changed by this fixture.
public static class TomXShowcase
{
    const int Size = 768;
    [Serializable] sealed class Property { public string name, value; }
    [Serializable] sealed class Lamp { public string type; public bool enabled; public float intensity; public Color color; public Vector3 position, rotation; }
    [Serializable] sealed class Shot
    {
        public string file, group, title, shader, lighting;
        public float angle, mean, peak;
        public int nonFinite, litPixels;
        public List<Property> properties = new List<Property>();
        public List<Lamp> lights = new List<Lamp>();
        [NonSerialized] public Color[] pixels;
    }
    [Serializable] sealed class Check { public string name; public double difference; public bool passed; }
    [Serializable] sealed class Report
    {
        public string unity, gpu, api, colorSpace;
        public string scope = "Actual X shaders on one shared 128x64 UV sphere, diameter 1. No game audit, no performance claim. Controlled analytic SH, not a captured Studio scene.";
        public string display = "768x768 ARGBFloat, fixed exposure 1.5, Reinhard then pow(1/2.2); black background; no per-shot normalization or image retouching.";
        public string camera = "Orthographic size 0.61, radius 3, near 0.05, far 20. Yaw recorded per shot; mesh never rotates. No postprocessing.";
        public string customSH = "2x2x2 atlas, constant across space. Ar=(.36,.26,-.05,.45); Ag=(-.03,.18,0,.4); Ab=(-.32,.3,-.03,.48); B/C=0; world bounds +/-2. Separate native ambient SH uses AddAmbientLight(.22) and a warm directional lobe.";
        public List<Shot> shots = new List<Shot>();
        public List<Check> checks = new List<Check>();
    }
    sealed class Stage : IDisposable
    {
        public readonly Report report = new Report();
        public readonly string output;
        public Camera camera;
        public Renderer sphere;
        public Light key, fill, accent;
        public GameObject blocker, background, writer;
        public Texture2D iris, depth, flow, normal, detail, lines, liquid, matcap, ramp, checker;
        public Texture3D sh;
        readonly Scene previous, scene;
        readonly List<Object> owned = new List<Object>();
        readonly Dictionary<Light,int> masks = new Dictionary<Light,int>();
        readonly Dictionary<Texture,string> textureFiles = new Dictionary<Texture,string>();
        readonly RenderTexture previousRT, target;
        readonly Texture2D read, png;
        readonly int pixelLights;
        readonly ShadowQuality shadows;
        readonly ShadowResolution shadowResolution;
        readonly float shadowDistance;
        readonly int layer;
        float angle;
        string lighting;
        public T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
        public Stage()
        {
            previous = SceneManager.GetActiveScene(); previousRT = RenderTexture.active;
            pixelLights = QualitySettings.pixelLightCount; shadows = QualitySettings.shadows;
            shadowResolution = QualitySettings.shadowResolution; shadowDistance = QualitySettings.shadowDistance;
            var used = new HashSet<int>(); foreach(var g in Object.FindObjectsOfType<GameObject>()) used.Add(g.layer);
            int available = 31; while(available >= 8 && used.Contains(available)) available--;
            if(available < 8) throw new Exception("No free showcase layer."); layer = available;
            foreach(var l in Object.FindObjectsOfType<Light>()) { masks[l]=l.cullingMask; l.cullingMask &= ~(1<<layer); }
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            output = Path.GetFullPath("CodexBridge/Reports/XShowcase-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"Textures"));
            report.unity=Application.unityVersion; report.gpu=SystemInfo.graphicsDeviceName;
            report.api=SystemInfo.graphicsDeviceType.ToString(); report.colorSpace=QualitySettings.activeColorSpace.ToString();
            QualitySettings.pixelLightCount=8; QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.VeryHigh; QualitySettings.shadowDistance=12;
            RenderSettings.skybox=null; RenderSettings.fog=false; RenderSettings.ambientMode=AmbientMode.Custom;
            RenderSettings.ambientLight=Color.black; RenderSettings.ambientProbe=new SphericalHarmonicsL2();
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            RenderSettings.customReflection=Own(TomXCoatEyeValidation.Environment()); RenderSettings.reflectionIntensity=1;
            camera = new GameObject("X showcase camera").AddComponent<Camera>(); camera.enabled=false;
            camera.cullingMask=1<<layer; camera.renderingPath=RenderingPath.Forward;
            camera.orthographic=true; camera.orthographicSize=.61f; camera.nearClipPlane=.05f; camera.farClipPlane=20;
            camera.allowHDR=true; camera.allowMSAA=false; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            target=Own(new RenderTexture(Size,Size,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)); target.Create(); camera.targetTexture=target;
            read=Own(new Texture2D(Size,Size,TextureFormat.RGBAFloat,false,true)); png=Own(new Texture2D(Size,Size,TextureFormat.RGB24,false));
            var subject=new GameObject("Shared standard UV sphere 128x64"); subject.layer=layer;
            subject.AddComponent<MeshFilter>().sharedMesh=Own(UVSphere()); sphere=subject.AddComponent<MeshRenderer>();
            sphere.shadowCastingMode=ShadowCastingMode.On; sphere.receiveShadows=true;
            sphere.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;
            key=Light("Key",LightType.Directional); fill=Light("Fill",LightType.Point); accent=Light("Accent",LightType.Spot);
            blocker=GameObject.CreatePrimitive(PrimitiveType.Sphere); blocker.name="Shadow-only occluder"; blocker.layer=layer;
            blocker.transform.position=new Vector3(-.26f,.48f,-.48f); blocker.transform.localScale=new Vector3(.5f,.5f,.5f);
            blocker.GetComponent<Renderer>().sharedMaterial=Own(new Material(Shader.Find("tom/MainOpaqueX")));
            blocker.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
            background=GameObject.CreatePrimitive(PrimitiveType.Quad); background.layer=layer; background.transform.position=new Vector3(0,0,.65f);
            background.transform.localScale=Vector3.one*1.1f;
            writer=GameObject.CreatePrimitive(PrimitiveType.Sphere); writer.layer=layer; writer.transform.localScale=Vector3.one*.64f;
            writer.transform.position=new Vector3(.14f,0,.15f);
            BuildTextures();
            var bg=Own(new Material(Shader.Find("tom/MainOpaqueX"))); bg.SetFloat("_DebugView",6);
            bg.SetTexture("_EmissionMask",checker); bg.SetFloat("_EmissionIntensity",.3f); background.GetComponent<Renderer>().sharedMaterial=bg;
            var wm=Own(new Material(Shader.Find("tom/EyeWX"))); wm.SetColor("_BaseColor",new Color(.65f,.7f,.75f)); writer.GetComponent<Renderer>().sharedMaterial=wm;
            Reset();
        }
        Light Light(string name,LightType type)
        {
            var l=new GameObject(name).AddComponent<Light>(); l.type=type; l.cullingMask=1<<layer;
            l.renderMode=LightRenderMode.ForcePixel; l.shadows=LightShadows.None; return l;
        }
        public void Orbit(float yaw)
        {
            angle=yaw; camera.transform.position=Quaternion.Euler(0,yaw,0)*new Vector3(0,0,-3);
            camera.transform.LookAt(Vector3.zero);
        }
        public void Reset()
        {
            Orbit(0); sphere.SetPropertyBlock(null); blocker.SetActive(false); background.SetActive(false); writer.SetActive(false);
            var feather=camera.GetComponent<TomShadersX.TomHairFeatherCamera>(); if(feather) Object.DestroyImmediate(feather);
            key.enabled=true; key.intensity=1.05f; key.color=Color.white; key.transform.rotation=Quaternion.Euler(30,-35,0);
            key.shadows=LightShadows.None; key.shadowBias=.025f; key.shadowNormalBias=.05f;
            fill.enabled=false; fill.intensity=1.3f; fill.color=new Color(.45f,.7f,1); fill.range=4; fill.transform.position=new Vector3(.8f,.2f,-1.1f);
            accent.enabled=false; accent.intensity=2; accent.color=new Color(1,.46f,.19f); accent.range=4; accent.spotAngle=80;
            accent.transform.position=new Vector3(-.8f,-.1f,-1.2f); accent.transform.LookAt(Vector3.zero);
            RenderSettings.ambientProbe=new SphericalHarmonicsL2(); lighting="white directional key; fixed cubemap available; material ambient .12 at gain .25";
        }
        public Material Material(string entry="MainOpaqueX")
        {
            var shader=Shader.Find("tom/"+entry); if(!shader || !shader.isSupported) throw new Exception("Unsupported "+entry);
            var m=Own(new Material(shader));
            m.SetColor("_BaseColor",new Color(.63f,.40f,.30f,1)); m.SetFloat("_UseRamp",0); m.SetFloat("_SpecularStrength",0);
            m.SetFloat("_LightProbeBlend",0); m.SetFloat("_CustomSHVolumeBlend",0); m.SetFloat("_VertexLightIntensity",0);
            m.SetColor("_AmbientColor",new Color(.12f,.12f,.12f,1)); m.SetFloat("_IndirectDiffuseIntensity",.25f);
            m.SetFloat("_CullOption",2); m.SetFloat("_EnvironmentToonBlend",0); m.SetFloat("_SpecularToonBlend",0);
            if(entry=="EyeX")
            {
                m.SetTexture("_MainTex",iris); m.SetColor("_BaseColor",Color.white);
                m.SetTexture("_expression",Texture2D.blackTexture); m.SetFloat("_exppower",0); m.SetFloat("_isHighLight",0);
                m.SetColor("_overcolor1",Color.clear); m.SetColor("_overcolor2",Color.clear);
                m.SetFloat("_IrisRadiusX",.2f); m.SetFloat("_IrisRadiusY",.34f); m.SetFloat("_IrisDepth",.28f);
                m.SetFloat("_EyeOpticsMaxOffset",.65f); m.SetFloat("_EyeRefractionIOR",1.35f);
            }
            if(entry=="SkinX")
            {
                m.SetTexture("_DetailMask",Solid("skin-neutral-detail",new Color(1,0,0,1)));
                m.SetTexture("_SkinControlMap",Solid("skin-neutral-control",new Color(1,1,0,1)));
                m.SetColor("_overcolor1",Color.clear); m.SetColor("_overcolor2",Color.clear); m.SetColor("_overcolor3",Color.clear);
            }
            return m;
        }
        public Shot Capture(string group,string title,Material m)
        {
            sphere.sharedMaterial=m; camera.Render(); camera.Render(); RenderTexture.active=target;
            read.ReadPixels(new Rect(0,0,Size,Size),0,0); read.Apply(false);
            var shot=new Shot {group=group,title=title,shader=m.shader.name,angle=angle,lighting=lighting,
                file=string.Format("{0:D3}-{1}.png",report.shots.Count+1,group),pixels=read.GetPixels()};
            var display=new Color[shot.pixels.Length]; double sum=0;
            for(int n=0;n<display.Length;n++)
            {
                var c=shot.pixels[n];
                if(!Finite(c.r)||!Finite(c.g)||!Finite(c.b)||!Finite(c.a)) { shot.nonFinite++; continue; }
                float lum=(c.r+c.g+c.b)/3; sum+=lum; shot.peak=Mathf.Max(shot.peak,lum);
                if(lum>.004f) shot.litPixels++; display[n]=new Color(Display(c.r),Display(c.g),Display(c.b),1);
            }
            shot.mean=(float)(sum/display.Length);
            for(int p=0;p<ShaderUtil.GetPropertyCount(m.shader);p++)
            {
                string name=ShaderUtil.GetPropertyName(m.shader,p); string value;
                switch(ShaderUtil.GetPropertyType(m.shader,p))
                {
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var t=m.GetTexture(name); string file;
                        value=t ? (textureFiles.TryGetValue(t,out file)? file : t.name) : "shader default";
                        value+="; ST="+m.GetTextureScale(name).ToString("F5")+"/"+m.GetTextureOffset(name).ToString("F5"); break;
                    case ShaderUtil.ShaderPropertyType.Color: value=m.GetColor(name).ToString("F5"); break;
                    case ShaderUtil.ShaderPropertyType.Vector: value=m.GetVector(name).ToString("F5"); break;
                    default: value=m.GetFloat(name).ToString("R",CultureInfo.InvariantCulture); break;
                }
                shot.properties.Add(new Property {name=name,value=value});
            }
            foreach(var l in new[]{key,fill,accent}) shot.lights.Add(new Lamp {type=l.type.ToString(),enabled=l.enabled,
                intensity=l.intensity,color=l.color,position=l.transform.position,rotation=l.transform.eulerAngles});
            report.shots.Add(shot); png.SetPixels(display); png.Apply(); File.WriteAllBytes(Path.Combine(output,shot.file),png.EncodeToPNG());
            Check("Finite and visible: "+shot.file,shot.mean,shot.nonFinite==0 && shot.litPixels>Size*Size/100);
            return shot;
        }
        public void Check(string name,double value,bool ok) {report.checks.Add(new Check {name=name,difference=value,passed=ok});}
        public void Different(string name,Shot a,Shot b,double min=.00002) {var d=Difference(a,b);Check(name,d,d>min);}
        public void Same(string name,Shot a,Shot b) {var d=Difference(a,b);Check(name,d,d<.000001);}
        public void NativeSH()
        {
            key.enabled=false; var coefficients=new SphericalHarmonicsL2(); coefficients.AddAmbientLight(new Color(.22f,.24f,.3f));
            coefficients.AddDirectionalLight(new Vector3(-.65f,.6f,-.3f).normalized,new Color(1,.44f,.19f),1.3f);
            RenderSettings.ambientProbe=coefficients; lighting="native ambient SH: cool fill plus warm upper-left lobe, direct lamps off";
        }
        public void CustomSH()
        {
            key.enabled=false; var block=new MaterialPropertyBlock(); block.SetTexture("_TomSHVolumeTex",sh);
            block.SetMatrix("_TomSHWorldToLocal",Matrix4x4.identity); block.SetVector("_TomSHBoundsMin",new Vector4(-2,-2,-2,0));
            block.SetVector("_TomSHBoundsInvSize",new Vector4(.25f,.25f,.25f,0)); block.SetVector("_TomSHVolumeGrid",new Vector4(2,2,2,14));
            block.SetVector("_TomSHVolumeParams",new Vector4(1,1,.1f,1)); sphere.SetPropertyBlock(block);
            lighting="analytic world-space SH volume: warm +X, cool -X, brighter +Y; direct lamps off";
        }
        public void Feather()
        {
            var h=camera.gameObject.AddComponent<TomShadersX.TomHairFeatherCamera>(); h.downsample=1;
            h.receivers=new[]{sphere}; h.sources=new[]{new TomShadersX.TomHairFeatherCamera.Source {renderer=writer.GetComponent<Renderer>()}};
            h.fieldShader=Shader.Find("Hidden/TomX/HairFeather");
        }
        public Texture2D Solid(string name,Color c) {return Texture(name,4,(u,v)=>c,true);}
        Texture2D Texture(string name,int size,Func<float,float,Color> sample,bool linear=true)
        {
            var t=Own(new Texture2D(size,size,TextureFormat.RGBA32,true,linear)); t.name=name; t.wrapMode=TextureWrapMode.Clamp;
            t.filterMode=FilterMode.Trilinear; var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=sample((x+.5f)/size,(y+.5f)/size);
            t.SetPixels(pixels);t.Apply(true); string file="Textures/"+name+".png";
            File.WriteAllBytes(Path.Combine(output,file),t.EncodeToPNG()); textureFiles[t]=file; return t;
        }
        void BuildTextures()
        {
            iris=Texture("iris-color",1024,(u,v)=> {
                float x=(u-.5f)/.2f,y=(v-.5f)/.34f,r=Mathf.Sqrt(x*x+y*y),a=Mathf.Atan2(y,x);
                float fibers=.5f+.22f*Mathf.Sin(a*97+Mathf.Sin(r*22))+.17f*Mathf.Sin(a*151+r*34);
                var c=Color.Lerp(new Color(.045f,.16f,.18f),new Color(.28f,.62f,.47f),Mathf.Clamp01(fibers));
                c=Color.Lerp(c,new Color(.69f,.41f,.095f),Mathf.Exp(-Mathf.Pow((r-.4f)*6,2))*.65f);
                c*=Mathf.Lerp(.1f,1,Mathf.SmoothStep(0,1,(1-r)/.1f));
                c=Color.Lerp(new Color(.002f,.004f,.006f),c,Mathf.SmoothStep(0,1,(r-.28f)/.018f));
                c=Color.Lerp(c,new Color(.74f,.78f,.77f),Mathf.SmoothStep(0,1,(r-1)/.035f)); c.a=1; return c;
            },false);
            depth=Texture("iris-height-linear",1024,(u,v)=> {
                float x=(u-.5f)/.2f,y=(v-.5f)/.34f,r=Mathf.Sqrt(x*x+y*y);
                float d=Mathf.SmoothStep(0,1,(1-r)/.65f); return new Color(1-d,1-d,1-d,1);
            });
            flow=Texture("strand-flow-linear",512,(u,v)=> {
                float a=Mathf.Sin(v*15)*.75f+u*2; return new Color(.5f+.5f*Mathf.Cos(a),.5f+.5f*Mathf.Sin(a),0,1);
            });
            normal=Texture("ripple-normal-packed",512,(u,v)=> {
                float x=.28f*Mathf.Sin(u*90)*Mathf.Cos(v*60),y=.28f*Mathf.Cos(u*90)*Mathf.Sin(v*60);
                return new Color(1,.5f+.5f*y,1,.5f+.5f*x);
            });
            detail=Texture("skin-detail-linear",512,(u,v)=>new Color(.4f+.6f*Mathf.SmoothStep(0,1,Mathf.Sin(u*15)),
                .55f*Mathf.Exp(-Mathf.Pow((v-.34f)*18,2)),0,1));
            lines=Texture("skin-lines-linear",512,(u,v)=>new Color(1-.8f*Mathf.Exp(-Mathf.Pow((v-.55f)*95,2)),.5f,0,1));
            liquid=Texture("liquid-pattern-linear",512,(u,v)=> {
                float a=Mathf.Sin(u*29+Mathf.Sin(v*35)*.7f)*Mathf.Sin(v*23);
                float wet=Mathf.SmoothStep(0,1,(a-.15f)/.3f); return new Color(wet,wet*.6f,0,1);
            });
            matcap=Texture("matcap-color",512,(u,v)=> {
                float d=Mathf.Exp(-Mathf.Pow((u-.32f)*7,2)-Mathf.Pow((v-.72f)*9,2));
                return new Color(.35f+d*.75f,.45f+d*.7f,.6f+d*.4f,1);
            },false);
            ramp=Texture("three-band-ramp-linear",256,(u,v)=> {float r=u<.25f?.08f:u<.63f?.45f:1;return new Color(r,r,r,1);});
            checker=Texture("checker-color",128,(u,v)=>((int)(u*12)+(int)(v*12))%2==0?new Color(.15f,.5f,.7f):new Color(.95f,.8f,.55f),false);
            sh=Own(new Texture3D(14,2,2,TextureFormat.RGBAHalf,false)); sh.name="Analytic world SH";sh.wrapMode=TextureWrapMode.Clamp;sh.filterMode=FilterMode.Bilinear;
            var values=new Color[56];var coefficients=new[]{new Color(.36f,.26f,-.05f,.45f),new Color(-.03f,.18f,0,.4f),new Color(-.32f,.3f,-.03f,.48f),Color.clear,Color.clear,Color.clear,Color.clear};
            for(int z=0;z<2;z++)for(int y=0;y<2;y++)for(int x=0;x<14;x++)values[(z*2+y)*14+x]=coefficients[x/2];
            sh.SetPixels(values);sh.Apply();
        }
        public void Save() {File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));}
        public void Dispose()
        {
            camera.targetTexture=null;RenderTexture.active=previousRT;
            EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach(var pair in masks) if(pair.Key) pair.Key.cullingMask=pair.Value;
            QualitySettings.pixelLightCount=pixelLights;QualitySettings.shadows=shadows;
            QualitySettings.shadowResolution=shadowResolution;QualitySettings.shadowDistance=shadowDistance;
            for(int n=owned.Count-1;n>=0;n--)if(owned[n])Object.DestroyImmediate(owned[n]);
        }
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode before showcase.");
        TomXSkinBuild.Validate();
        using(var s=new Stage())
        {
            try
            {
                Core(s); Layers(s); Specialists(s); Eyes(s); Coverage(s); Combinations(s);
                s.Save(); int failed=s.report.checks.FindAll(c=>!c.passed).Count;
                if(failed>0)throw new Exception(failed+" showcase pixel checks failed: "+s.output);
                return s.report.shots.Count+" actual sphere captures; "+s.report.checks.Count+" checks passed. "+s.output;
            }
            finally{s.Save();}
        }
    }
    static void Core(Stage s)
    {
        var m=s.Material();var smooth=s.Capture("01-diffuse","Continuous diffuse",m);
        m.SetFloat("_UseRamp",1);m.SetFloat("_ToonSoftness",.02f);var toon=s.Capture("01-diffuse","Hard Toon diffuse",m);
        m.SetFloat("_ToonSoftness",.5f);s.Capture("01-diffuse","Soft Toon diffuse",m);
        m.SetFloat("_RampMode",1);m.SetTexture("_RampTex",s.ramp);s.Capture("01-diffuse","Three-band texture ramp",m);
        s.Different("Toon changes diffuse",smooth,toon);
        s.Reset();m=s.Material();s.blocker.SetActive(true);s.key.shadows=LightShadows.Soft;
        var shadow=s.Capture("02-shadow","Soft external shadow",m);
        m.SetFloat("_ShadowRemapStrength",1);m.SetFloat("_ShadowSoftness",.01f);s.Capture("02-shadow","Remapped hard shadow",m);
        m.SetFloat("_UseRamp",1);m.SetFloat("_ToonSoftness",.04f);s.Capture("02-shadow","Shadow + Toon response",m);
        m.SetFloat("_ShadowStrength",0);var unshadowed=s.Capture("02-shadow","Diffuse shadow bypass",m);
        s.Different("Actual external shadow contributes",shadow,unshadowed);
        s.Reset();m=s.Material();m.SetFloat("_SpecularStrength",3);m.SetFloat("_Roughness",.28f);m.SetFloat("_SpecularIOR",1.8f);
        var ggx=s.Capture("03-specular","GGX / roughness 0.28",m);
        m.SetFloat("_Roughness",.65f);s.Capture("03-specular","GGX / roughness 0.65",m);
        m.SetFloat("_SpecularToonBlend",1);m.SetFloat("_SpecularSize",.8f);m.SetFloat("_SpecularSoftness",.03f);
        var shaped=s.Capture("03-specular","Toon highlight",m);
        m.SetFloat("_SpecularSoftness",.65f);m.SetFloat("_SpecularBands",3);s.Capture("03-specular","Three highlight bands",m);
        s.Different("Continuous versus Toon specular",ggx,shaped);
        s.Reset();m=s.Material();m.SetFloat("_ReflectionMode",2);m.SetFloat("_SpecularStrength",1.5f);m.SetFloat("_Roughness",.24f);
        s.Capture("04-material","Dielectric",m);m.SetFloat("_Metallic",.5f);s.Capture("04-material","Half metallic",m);
        m.SetFloat("_Metallic",1);s.Capture("04-material","Metal / smooth",m);m.SetFloat("_Roughness",.65f);s.Capture("04-material","Metal / rough",m);
        s.Reset();m=s.Material();m.SetColor("_BaseColor",new Color(.6f,.6f,.6f));s.key.enabled=false;
        s.Capture("05-sh","Flat material ambient",m);s.NativeSH();m.SetFloat("_LightProbeBlend",1);m.SetFloat("_IndirectDiffuseIntensity",1);
        var native=s.Capture("05-sh","Native directional SH",m);s.CustomSH();m.SetFloat("_CustomSHVolumeBlend",1);
        var custom=s.Capture("05-sh","Custom world SH volume",m);m.SetFloat("_IndirectToonBlend",1);m.SetFloat("_IndirectToonSoftness",.08f);
        s.Capture("05-sh","SH with Toon shaping",m);s.Different("Custom SH has its own directional response",native,custom);
        s.Reset();m=s.Material();m.SetColor("_BaseColor",new Color(.12f,.19f,.22f));
        var off=s.Capture("06-environment","Reflection off",m);m.SetFloat("_ReflectionMode",2);m.SetFloat("_Roughness",.12f);
        var on=s.Capture("06-environment","Smooth probe reflection",m);m.SetFloat("_Roughness",.55f);s.Capture("06-environment","Rough probe reflection",m);
        m.SetFloat("_Roughness",.2f);m.SetFloat("_EnvironmentToonBlend",1);m.SetFloat("_EnvironmentSoftness",.02f);s.Capture("06-environment","Toon environment",m);
        s.Different("Probe reflection is present",off,on);
    }
    static void Layers(Stage s)
    {
        s.Reset();var m=s.Material();s.Capture("07-matcap","MatCap off",m);m.SetTexture("_MatCap",s.matcap);m.SetFloat("_ReflectionMode",1);
        m.SetFloat("_MatCapIntensity",.65f);s.Capture("07-matcap","Add MatCap",m);m.SetFloat("_MatCapBlendMode",0);s.Capture("07-matcap","Multiply MatCap",m);
        m.SetFloat("_ReflectionMode",3);m.SetFloat("_Roughness",.2f);s.Capture("07-matcap","Multiply + environment",m);
        s.Reset();m=s.Material();m.SetColor("_BaseColor",new Color(.12f,.33f,.27f));
        var off=s.Capture("08-clearcoat","Dry substrate",m);m.SetFloat("_ClearCoat",1);m.SetFloat("_ClearCoatRoughness",.08f);
        var coat=s.Capture("08-clearcoat","Smooth clearcoat",m);m.SetFloat("_ClearCoatRoughness",.4f);s.Capture("08-clearcoat","Rough clearcoat",m);
        m.SetFloat("_ClearCoatRoughness",.12f);m.SetFloat("_ClearCoatNormalSource",3);m.SetTexture("_ClearCoatNormalMap",s.normal);
        s.Capture("08-clearcoat","Independent coat normal",m);s.Different("Clearcoat visible independently of substrate reflection",off,coat);
        s.Reset();m=s.Material();s.Capture("09-art-layers","Plain surface",m);m.SetFloat("_RimStrength",.8f);m.SetColor("_RimColor",new Color(.3f,.7f,1));
        s.Capture("09-art-layers","Cool rim",m);m.SetFloat("_RimStrength",0);m.SetFloat("_OutlineOn",1);m.SetFloat("_OutlineWidth",6);
        m.SetColor("_OutlineColor",new Color(.1f,.45f,.6f));s.Capture("09-art-layers","Pixel-width outline",m);
        m.SetFloat("_OutlineOn",0);m.SetTexture("_EmissionMask",s.liquid);m.SetFloat("_EmissionIntensity",.5f);m.SetColor("_EmissionColor",new Color(.08f,.6f,1));
        s.Capture("09-art-layers","Masked emission",m);
        s.Reset();m=s.Material();m.SetColor("_BaseColor",new Color(.18f,.25f,.35f));m.SetFloat("_SpecularStrength",2);m.SetFloat("_Roughness",.35f);
        s.Capture("10-normals","Geometry normal",m);m.SetTexture("_NormalMap",s.normal);s.Capture("10-normals","Main ripple normal",m);
        m.SetTexture("_NormalMapDetail",s.normal);m.SetTextureScale("_NormalMapDetail",new Vector2(3,3));s.Capture("10-normals","Main + detail normals",m);
        m.SetFloat("_ClearCoat",1);m.SetFloat("_ClearCoatNormalSource",0);s.Capture("10-normals","Smooth coat over detail",m);
        s.Reset();m=s.Material();m.SetFloat("_UseRamp",1);m.SetFloat("_SpecularToonBlend",1);m.SetFloat("_SpecularStrength",2);m.SetFloat("_ClearCoat",.7f);
        var one=s.Capture("11-multilight","One directional light",m);s.fill.enabled=true;var two=s.Capture("11-multilight","+ blue point light",m);
        s.accent.enabled=true;s.Capture("11-multilight","+ amber spot light",m);m.SetFloat("_AdditionalLightIntensity",0);s.Capture("11-multilight","Additional gain = 0",m);
        s.Different("ForwardAdd contributes",one,two);
    }
    static void Specialists(Stage s)
    {
        s.Reset();var m=s.Material("HairX");m.SetColor("_BaseColor",new Color(.22f,.13f,.18f));m.SetFloat("_SpecularStrength",3);m.SetFloat("_SpecularIOR",1.8f);m.SetFloat("_Roughness",.4f);
        m.SetFloat("_HairAnisotropy",0);var isotropic=s.Capture("12-anisotropy","Isotropic GGX",m);m.SetFloat("_HairAnisotropy",.95f);
        var anisotropic=s.Capture("12-anisotropy","Strand V / anisotropic",m);m.SetFloat("_StrandAngle",0);s.Capture("12-anisotropy","Strand U / anisotropic",m);
        m.SetTexture("_StrandDirectionMap",s.flow);m.SetFloat("_StrandDirectionBlend",1);s.Capture("12-anisotropy","Authored RG flow",m);
        s.Different("Anisotropic distribution changes",isotropic,anisotropic);
        m.SetFloat("_StrandDirectionBlend",0);m.SetFloat("_StrandAngle",90);m.SetFloat("_SpecularToonBlend",1);m.SetFloat("_SpecularSize",.85f);
        m.SetFloat("_SpecularSoftness",.03f);s.Capture("13-hair-toon","Hard strand highlight",m);m.SetFloat("_SpecularSoftness",.7f);s.Capture("13-hair-toon","Soft strand highlight",m);
        m.SetFloat("_SpecularBands",3);s.Capture("13-hair-toon","Three highlight bands",m);m.SetFloat("_ClearCoat",.7f);s.Capture("13-hair-toon","Strand + surface coat",m);
        s.Reset();m=s.Material("SkinX");var neutral=s.Capture("14-skin","Neutral X diffuse",m);m.SetFloat("_SkinStrength",1);m.SetFloat("_SkinWrap",.65f);
        s.Capture("14-skin","Soft front-side skin",m);m.SetFloat("_SkinWarmth",1);m.SetFloat("_SkinWarmWidth",.55f);s.Capture("14-skin","Warm transition",m);
        m.SetFloat("_ClearCoat",.8f);m.SetFloat("_ClearCoatRoughness",.12f);var wet=s.Capture("14-skin","Warm skin + wet coat",m);s.Different("Skin combination differs",neutral,wet);
        s.Reset();m=s.Material("SkinX");m.SetTexture("_DetailMask",s.detail);m.SetFloat("_SkinShadeStrength",1);s.Capture("15-skin-art","Painted detail shade",m);
        m.SetTexture("_LineMask",s.lines);m.SetFloat("_linetexon",1);m.SetFloat("_SkinLineStrength",1);s.Capture("15-skin-art","Internal line mask",m);
        m.SetTexture("_overtex2",s.liquid);m.SetColor("_overcolor2",new Color(.85f,.24f,.2f,.5f));s.Capture("15-skin-art","UV2 color overlay",m);
        m.SetTexture("_liquidmask",s.Solid("liquid-front-top",new Color(1,0,0,1)));m.SetTexture("_Texture2",s.liquid);m.SetTexture("_Texture3",s.normal);
        m.SetFloat("_liquidftop",1);m.SetFloat("_ClearCoat",1);m.SetFloat("_SkinCoatCoverage",1);m.SetFloat("_SkinLiquidColorStrength",.35f);
        s.Capture("15-skin-art","Localized liquid + coat",m);
        s.Reset();m=s.Material("EyeWX");m.SetColor("_BaseColor",new Color(.72f,.76f,.77f));s.Capture("16-eyew","Eye surface / dry",m);
        m.SetFloat("_SpecularStrength",1);m.SetFloat("_Roughness",.45f);s.Capture("16-eyew","Substrate specular",m);
        m.SetFloat("_ClearCoat",1);m.SetFloat("_ClearCoatRoughness",.08f);s.Capture("16-eyew","Independent wet coat",m);
        m.SetColor("_Color",new Color(.5f,.4f,.36f));s.Capture("16-eyew","Game color alias tint",m);
    }
    static void Eyes(Stage s)
    {
        s.Reset();s.Orbit(28);var m=s.Material("EyeX");var off=s.Capture("17-eye-shapes","Optics off / yaw 28",m);
        m.SetFloat("_EyeOpticsMode",1);s.Capture("17-eye-shapes","Flat recess / yaw 28",m);m.SetFloat("_IrisDepthShape",1);s.Capture("17-eye-shapes","Shallow cone / yaw 28",m);
        m.SetFloat("_IrisDepthShape",2);var bowl=s.Capture("17-eye-shapes","Shallow bowl / yaw 28",m);s.Different("Eye optics changes visible art",off,bowl);
        m.SetFloat("_UseEyeSurfaceMask",1);m.SetTexture("_EyeSurfaceMap",s.depth);m.SetFloat("_ClearCoat",.65f);
        foreach(float a in new[]{-35f,-15f,15f,35f}) {s.Orbit(a);s.Capture("18-eye-angles","Painted depth / yaw "+a,m);}
        s.Orbit(32);m.SetFloat("_ClearCoat",0);m.SetFloat("_EyeDispersion",0);var mono=s.Capture("19-eye-dispersion","Single interior ray",m);
        m.SetFloat("_EyeDispersion",1);var rgb=s.Capture("19-eye-dispersion","RGB dispersion = 1",m);s.Different("RGB ray splitting is present",mono,rgb,.000001);
        m.SetFloat("_ClearCoat",1);s.Capture("19-eye-dispersion","Dispersion + clearcoat",m);m.SetFloat("_EyeRefractionIOR",2.3f);s.Capture("19-eye-dispersion","Higher refraction IOR",m);
        m.SetFloat("_EyeDispersion",0);m.SetFloat("_ClearCoat",0);m.SetFloat("_EyeRefractionIOR",1.35f);m.SetFloat("_ClearCoatIOR",1);
        var low=s.Capture("20-ior-isolation","Coat off / coat IOR 1",m);m.SetFloat("_ClearCoatIOR",2.5f);var high=s.Capture("20-ior-isolation","Coat off / coat IOR 2.5",m);
        s.Same("Coat IOR never changes iris UV",low,high);m.SetFloat("_EyeRefractionIOR",1);s.Capture("20-ior-isolation","Refraction IOR 1",m);
        m.SetFloat("_EyeRefractionIOR",2.5f);s.Capture("20-ior-isolation","Refraction IOR 2.5",m);
        // Paired oblique controls separate changed viewing angle from actual optical displacement.
        m.SetFloat("_EyeRefractionIOR",1.35f);
        foreach(float a in new[]{-32f,32f}) {s.Orbit(a);m.SetFloat("_EyeOpticsMode",0);s.Capture("21-eye-angle-controls","Yaw "+a+" / optics off",m);
            m.SetFloat("_EyeOpticsMode",1);s.Capture("21-eye-angle-controls","Yaw "+a+" / painted optics",m);}
    }
    static void Coverage(Stage s)
    {
        s.Reset();s.background.SetActive(true);var m=s.Material();s.Capture("22-transparency","Opaque reference",m);
        m=s.Material("MainAlphaX");m.SetFloat("_Alpha",.4f);s.Capture("22-transparency","Standard alpha = 0.4",m);
        m=s.Material("MainAlphaXBackFront");m.SetFloat("_Alpha",.4f);s.Capture("22-transparency","Back + front layers",m);
        m=s.Material("MainAlphaX2Pass");m.SetFloat("_Alpha",.4f);s.Capture("22-transparency","Legacy depth prepass",m);
        s.Reset();s.writer.SetActive(true);m=s.Material("HairX");m.SetColor("_BaseColor",new Color(.32f,.34f,.47f));
        m.SetFloat("_HairFrontOpacity",.18f);s.Capture("23-hairfront","HairFront off",m);
        m.SetFloat("_HairFrontMode",1);var hard=s.Capture("23-hairfront","Hard stencil window",m);
        m.SetFloat("_HairFrontMode",2);m.SetFloat("_HairFeatherWidth",32);s.Feather();var feather=s.Capture("23-hairfront","32 px inward feather",m);
        m.SetFloat("_HairFeatherWidthMode",1);m.SetFloat("_HairFeatherWorldWidth",.05f);s.Capture("23-hairfront","World-unit feather",m);
        s.Different("Hair sphere has visible feather boundary",hard,feather);
    }
    static void Combinations(Stage s)
    {
        s.Reset();var m=s.Material();m.SetColor("_BaseColor",new Color(.4f,.14f,.1f));m.SetFloat("_UseRamp",1);m.SetFloat("_ToonSoftness",.15f);
        m.SetFloat("_SpecularToonBlend",1);m.SetFloat("_SpecularStrength",2);m.SetFloat("_ClearCoat",.75f);s.Capture("24-finished-materials","Toon lacquer",m);
        m=s.Material();m.SetColor("_BaseColor",new Color(.12f,.37f,.48f));m.SetFloat("_Metallic",.9f);m.SetFloat("_Roughness",.24f);m.SetFloat("_ReflectionMode",2);
        m.SetFloat("_SpecularStrength",1.5f);s.Capture("24-finished-materials","Colored metal",m);
        m=s.Material("HairX");m.SetColor("_BaseColor",new Color(.2f,.12f,.22f));m.SetFloat("_UseRamp",1);m.SetFloat("_SpecularStrength",3);
        m.SetFloat("_SpecularIOR",1.9f);m.SetFloat("_SpecularToonBlend",1);m.SetFloat("_SpecularSize",.85f);m.SetFloat("_HairAnisotropy",.9f);s.Capture("24-finished-materials","Stylized hair sheen",m);
        m=s.Material("SkinX");m.SetFloat("_SkinStrength",.7f);m.SetFloat("_SkinWarmth",.8f);m.SetFloat("_ClearCoat",.5f);s.Capture("24-finished-materials","Soft glazed skin",m);
    }
    static float Display(float x) {x=Mathf.Max(0,x)*1.5f;return Mathf.Pow(x/(1+x),1/2.2f);}
    static bool Finite(float x) {return !float.IsNaN(x)&&!float.IsInfinity(x);}
    static double Difference(Shot a,Shot b)
    {
        double d=0;for(int n=0;n<a.pixels.Length;n++){var x=a.pixels[n]-b.pixels[n];d+=Math.Abs(x.r)+Math.Abs(x.g)+Math.Abs(x.b);}
        return d/(a.pixels.Length*3);
    }
    static Mesh UVSphere()
    {
        const int w=128,h=64;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tangents=new List<Vector4>();var triangles=new List<int>();var colors=new List<Color>();
        for(int y=0;y<=h;y++)for(int x=0;x<=w;x++)
        {
            float u=(float)x/w,v=(float)y/h,phi=(u-.5f)*Mathf.PI*2,theta=(1-v)*Mathf.PI;
            var n=new Vector3(Mathf.Sin(theta)*Mathf.Sin(phi),Mathf.Cos(theta),-Mathf.Sin(theta)*Mathf.Cos(phi));
            vertices.Add(n*.5f);normals.Add(n);uv.Add(new Vector2(u,v));tangents.Add(new Vector4(Mathf.Cos(phi),0,Mathf.Sin(phi),-1));colors.Add(Color.white);
        }
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){int a=y*(w+1)+x,b=a+1,c=a+w+1;triangles.AddRange(new[]{a,c,b,b,c,c+1});}
        var mesh=new Mesh {name="Standard UV sphere 128x64"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetColors(colors);
        for(int n=0;n<4;n++)mesh.SetUVs(n,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
}

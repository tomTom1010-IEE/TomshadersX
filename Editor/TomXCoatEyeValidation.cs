using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated GPU fixtures, invoked by CodexBridge. No user scenes/materials are modified.
public static class TomXCoatEyeValidation
{
    const int Size = 512;
    [Serializable] class Shot
    {
        public string name;
        public int nonFinite, brightPixels;
        public float mean, peak;
        [NonSerialized] public Color[] pixels;
    }
    [Serializable] class Check { public string name; public double value; public bool passed; }
    [Serializable] class Report
    {
        public string unity, device, api, colorSpace;
        public string display = "Same float readback -> x1.5 exposure -> Reinhard -> display gamma for every PNG. No per-shot normalization. Metrics use unmodified float pixels. See colorSpace: these previews are not calibrated KKS screenshots.";
        public string fixture = "Procedural iris on ONE curved UV surface, no corneal mesh. Synthetic HDR softboxes and Forward lights. Not a KKS character, anatomical lens model or measured game GPU benchmark.";
        public List<Shot> shots = new List<Shot>();
        public List<Check> checks = new List<Check>();
    }
    sealed class Fixture : IDisposable
    {
        public string output;
        public Report report;
        public Camera camera;
        public Light sun, point, spot;
        public Renderer subject;
        public GameObject sphere, patch, stencilProbe;
        public Texture2D iris, empty, normal, blackMap;
        public int layer;
        readonly Scene previous, scene;
        readonly RenderTexture oldRT, rt;
        readonly int oldLights;
        readonly ShadowQuality oldShadows;
        readonly float oldShadowDistance;
        readonly Dictionary<Light,int> lightMasks = new Dictionary<Light,int>();
        readonly List<Object> garbage = new List<Object>();
        readonly Texture2D read, png;

        public T Own<T>(T item) where T : Object { garbage.Add(item); return item; }
        public Fixture()
        {
            previous = SceneManager.GetActiveScene(); oldRT = RenderTexture.active; oldLights = QualitySettings.pixelLightCount;
            oldShadows=QualitySettings.shadows;oldShadowDistance=QualitySettings.shadowDistance;
            var used = new HashSet<int>(); foreach(GameObject g in Object.FindObjectsOfType<GameObject>()) used.Add(g.layer);
            for(layer=31;layer>=8 && used.Contains(layer);layer--) {}
            if(layer<8) throw new Exception("No free test layer.");
            foreach(Light l in Object.FindObjectsOfType<Light>()) { lightMasks[l]=l.cullingMask; l.cullingMask &= ~(1<<layer); }
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"CodexBridge/Reports/XCoatEye-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            report=new Report {unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString()};
            QualitySettings.pixelLightCount=8;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=20;
            RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=Color.black;
            RenderSettings.fog=false; RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
            RenderSettings.customReflection=Own(Environment()); RenderSettings.reflectionIntensity=1;
            camera=new GameObject("Coat and optics camera").AddComponent<Camera>(); camera.enabled=false;
            camera.cullingMask=1<<layer; camera.renderingPath=RenderingPath.Forward; camera.allowHDR=true; camera.allowMSAA=false;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.012f,.016f,.022f,0);
            camera.nearClipPlane=.03f; camera.farClipPlane=30; camera.orthographic=true; camera.orthographicSize=.66f; Orbit(0);
            rt=Own(new RenderTexture(Size,Size,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)); rt.Create(); camera.targetTexture=rt;
            read=Own(new Texture2D(Size,Size,TextureFormat.RGBAFloat,false,true)); png=Own(new Texture2D(Size,Size,TextureFormat.RGB24,false));
            sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.layer=layer;
            subject=sphere.GetComponent<Renderer>(); subject.shadowCastingMode=ShadowCastingMode.Off;
            patch=new GameObject("Single spherical UV patch"); patch.layer=layer;
            patch.AddComponent<MeshFilter>().sharedMesh=Own(EyePatch()); patch.AddComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off; patch.SetActive(false);
            sun=Light("Key",LightType.Directional); sun.transform.rotation=Quaternion.Euler(25,-25,0); sun.intensity=1;
            point=Light("Additional point",LightType.Point); point.transform.position=new Vector3(.5f,.3f,-1.4f); point.range=4; point.intensity=1.5f; point.color=new Color(.55f,.75f,1); point.enabled=false;
            spot=Light("Additional spot",LightType.Spot); spot.transform.position=new Vector3(-.5f,-.2f,-1.2f); spot.transform.LookAt(Vector3.zero); spot.range=4; spot.spotAngle=95; spot.intensity=2; spot.enabled=false;
            empty=Own(Solid(Color.clear)); blackMap=Own(Solid(new Color(0,1,0,1))); iris=Own(Iris()); normal=Own(Normal());
            stencilProbe=GameObject.CreatePrimitive(PrimitiveType.Quad); stencilProbe.layer=layer; stencilProbe.transform.position=new Vector3(0,0,-.1f);
            stencilProbe.transform.localScale=new Vector3(4,4,1); stencilProbe.GetComponent<Renderer>().sharedMaterial=Own(new Material(Shader.Find("Hidden/TomX/EyeStencilProbe"))); stencilProbe.SetActive(false);
        }
        Light Light(string name,LightType type)
        {
            var light=new GameObject(name).AddComponent<Light>(); light.type=type; light.cullingMask=1<<layer;
            light.renderMode=LightRenderMode.ForcePixel; light.shadows=LightShadows.None; return light;
        }
        public void Orbit(float angle)
        {
            camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-3);
            camera.transform.LookAt(Vector3.zero);
        }
        public Material Material(string name)
        {
            var m=Own(new Material(Shader.Find("tom/"+name)));
            m.SetColor("_BaseColor",new Color(.06f,.26f,.19f,1)); m.SetFloat("_SpecularStrength",0);
            m.SetFloat("_LightProbeBlend",0); m.SetFloat("_CustomSHVolumeBlend",0); m.SetFloat("_VertexLightIntensity",0);
            m.SetColor("_AmbientColor",new Color(.12f,.12f,.12f,1)); m.SetFloat("_UseRamp",0);
            m.SetFloat("_IndirectDiffuseIntensity",1); // Fixed regression exposure, independent of authoring defaults.
            if(m.HasProperty("_HairFrontMode")) m.SetFloat("_HairFrontMode",0);
            if(m.HasProperty("_OutlineOn")) m.SetFloat("_OutlineOn",0);
            if(m.HasProperty("_Alpha")) m.SetFloat("_Alpha",1);
            if(name=="EyeX")
            {
                m.SetTexture("_expression",empty); m.SetTexture("_overtex1",empty); m.SetTexture("_overtex2",empty);
                m.SetFloat("_IrisRadiusX",.37f); m.SetFloat("_IrisRadiusY",.37f);
                m.SetFloat("_IrisDepth",.25f); m.SetFloat("_ClearCoatIOR",1.376f); m.SetFloat("_EyeRefractionIOR",1.376f);
            }
            return m;
        }
        public Shot Capture(string name,Material m)
        {
            subject.sharedMaterial=m; camera.Render(); camera.Render(); RenderTexture.active=rt;
            read.ReadPixels(new Rect(0,0,Size,Size),0,0); read.Apply(false);
            return Record(name,read.GetPixels());
        }
        public Shot CaptureResized(string name,Material m,int resolution)
        {
            var small=Own(new RenderTexture(resolution,resolution,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)); small.Create();
            var data=Own(new Texture2D(resolution,resolution,TextureFormat.RGBAFloat,false,true));
            camera.targetTexture=small; subject.sharedMaterial=m;
            try
            {
                camera.Render(); camera.Render(); RenderTexture.active=small;
                data.ReadPixels(new Rect(0,0,resolution,resolution),0,0);data.Apply(false);
                var pixels=new Color[Size*Size];
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)pixels[y*Size+x]=data.GetPixelBilinear((x+.5f)/Size,(y+.5f)/Size);
                return Record(name,pixels);
            }
            finally {camera.targetTexture=rt;RenderTexture.active=rt;}
        }
        Shot Record(string name,Color[] pixels)
        {
            var s=new Shot {name=name,pixels=pixels}; double total=0;
            var colors=new Color[s.pixels.Length];
            for(int n=0;n<s.pixels.Length;n++)
            {
                Color p=s.pixels[n];
                if(!Finite(p.r)||!Finite(p.g)||!Finite(p.b)||!Finite(p.a)) { s.nonFinite++; continue; }
                float l=p.r*.2126f+p.g*.7152f+p.b*.0722f; total+=l; s.peak=Mathf.Max(s.peak,l);
                if(l>.04f) s.brightPixels++;
                colors[n]=Display(p);
            }
            s.mean=(float)(total/s.pixels.Length); report.shots.Add(s);
            png.SetPixels(colors); png.Apply(false); File.WriteAllBytes(Path.Combine(output,name+".png"),png.EncodeToPNG());
            Check("Finite: "+name,s.nonFinite,s.nonFinite==0);
            return s;
        }
        public void Check(string name,double value,bool passed) { report.checks.Add(new Check {name=name,value=value,passed=passed}); }
        public void Same(string name,Shot a,Shot b,double tolerance=0.000001) { double d=Difference(a,b); Check(name,d,d<tolerance); }
        public void Different(string name,Shot a,Shot b,double tolerance=0.00001) { double d=Difference(a,b); Check(name,d,d>tolerance); }
        public void Sheet(string name,int columns,params Shot[] shots)
        {
            int rows=(shots.Length+columns-1)/columns;
            var sheet=Own(new Texture2D(Size*columns,Size*rows,TextureFormat.RGB24,false));
            for(int n=0;n<shots.Length;n++)
            {
                var colors=new Color[Size*Size]; for(int p=0;p<colors.Length;p++) colors[p]=Display(shots[n].pixels[p]);
                sheet.SetPixels((n%columns)*Size,(rows-1-n/columns)*Size,Size,Size,colors);
            }
            sheet.Apply(false); File.WriteAllBytes(Path.Combine(output,name+".png"),sheet.EncodeToPNG());
        }
        public void Save() { File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true)); }
        public void Dispose()
        {
            RenderTexture.active=oldRT;
            EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach(var p in lightMasks) if(p.Key) p.Key.cullingMask=p.Value;
            QualitySettings.pixelLightCount=oldLights;
            QualitySettings.shadows=oldShadows;QualitySettings.shadowDistance=oldShadowDistance;
            foreach(Object item in garbage) if(item) Object.DestroyImmediate(item);
        }
    }

    public static string Run()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play Mode before automated captures.");
        string compilation=TomXCoatEyeBuild.Validate();
        using(var f=new Fixture())
        {
            try
            {
                foreach(string entry in TomXCoatEyeBuild.Entries)
                {
                    Material m=f.Material(entry); Shot off=f.Capture(entry+"-coat-off",m);
                    m.SetFloat("_ClearCoat",1); Shot on=f.Capture(entry+"-coat-on",m);
                    f.Different(entry+": coat visible",off,on);
                    m.SetTexture("_ClearCoatMap",f.blackMap); f.Same(entry+": zero coat map preserves base",off,f.Capture(entry+"-coat-zero-mask",m));
                    m.SetTexture("_ClearCoatMap",null); m.SetFloat("_ClearCoatIOR",1);
                    f.Same(entry+": IOR one index matched",off,f.Capture(entry+"-coat-ior-one",m));
                }
                Material coat=f.Material("MainOpaqueX");
                Shot dry=f.Capture("coat-demo-dry",coat);
                coat.SetFloat("_ClearCoat",1); coat.SetFloat("_ClearCoatRoughness",.08f);
                Shot smooth=f.Capture("coat-demo-smooth",coat);
                coat.SetFloat("_ClearCoatRoughness",.35f); Shot rough=f.Capture("coat-demo-rough",coat);
                f.Different("Coat roughness changes reflection",smooth,rough);
                f.Sheet("clearcoat-comparison",3,dry,smooth,rough);
                coat.SetFloat("_ClearCoatDebugView",2); Shot env=f.Capture("coat-env-only",coat);
                f.point.enabled=f.spot.enabled=true; Shot envAdd=f.Capture("coat-env-with-add-lights",coat);
                f.Same("Environment is Base-only",env,envAdd);
                coat.SetFloat("_ClearCoatDebugView",1); Shot directAdd=f.Capture("coat-direct-with-add-lights",coat);
                f.point.enabled=f.spot.enabled=false; Shot direct=f.Capture("coat-direct-main-only",coat);
                f.Different("Coat receives additional lights",direct,directAdd);
                f.sun.shadows=LightShadows.Hard;
                var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);occluder.layer=f.layer;
                occluder.transform.position=new Vector3(.29f,.33f,-.89f);occluder.transform.localScale=Vector3.one*.35f;
                var blocker=occluder.GetComponent<Renderer>();blocker.sharedMaterial=f.Material("MainOpaqueX");blocker.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                f.Different("Coat direct receives physical main shadow",direct,f.Capture("coat-direct-shadowed",coat));
                occluder.SetActive(false);f.sun.shadows=LightShadows.None;
                coat.SetFloat("_MainLightIntensity",0);f.spot.enabled=true;
                Shot spot=f.Capture("coat-spot-only",coat);f.spot.cookie=f.empty;
                f.Different("Coat additional light respects cookie",spot,f.Capture("coat-spot-black-cookie",coat));
                f.spot.cookie=null;f.spot.enabled=false;coat.SetFloat("_MainLightIntensity",1);
                coat.SetFloat("_ClearCoatDebugView",0); coat.SetFloat("_ClearCoatNormalSource",3); coat.SetTexture("_ClearCoatNormalMap",f.normal);
                f.Different("Independent coat normal changes reflection",rough,f.Capture("coat-independent-normal",coat));
                coat.SetFloat("_ClearCoatNormalSource",0); coat.SetFloat("_ClearCoatEnergyBlend",0);
                f.Different("Energy blend changes body",rough,f.Capture("coat-toon-body-preservation",coat));

                f.sphere.SetActive(false); f.patch.SetActive(true); f.subject=f.patch.GetComponent<Renderer>(); f.camera.orthographicSize=.94f;
                Material eye=f.Material("EyeX"); eye.SetTexture("_MainTex",f.iris); eye.SetColor("_BaseColor",Color.white);
                eye.SetFloat("_ClearCoatRoughness",.09f); eye.SetFloat("_UseRamp",0); eye.SetColor("_AmbientColor",new Color(.2f,.2f,.2f,1));
                var eyeRows=new List<Shot>();
                foreach(float angle in new[]{-40f,0f,40f})
                {
                    f.Orbit(angle); string a=angle.ToString("0",System.Globalization.CultureInfo.InvariantCulture);
                    eye.SetFloat("_EyeOpticsMode",0); eye.SetFloat("_ClearCoat",0);
                    Shot flat=f.Capture("eye-"+a+"-flat",eye); eyeRows.Add(flat);
                    eye.SetFloat("_EyeOpticsMode",1); Shot refracted=f.Capture("eye-"+a+"-refracted",eye); eyeRows.Add(refracted);
                    f.Different("Refraction moves interior at "+a,flat,refracted);
                    eye.SetFloat("_ClearCoat",1); Shot both=f.Capture("eye-"+a+"-refracted-coat",eye); eyeRows.Add(both);
                    f.Different("Coat is independent at "+a,refracted,both);
                }
                f.Sheet("eye-angle-comparison",3,eyeRows.ToArray());
                f.Orbit(45); eye.SetFloat("_ClearCoat",0);
                Shot noDispersion=f.Capture("eye-dispersion-zero",eye);
                eye.SetFloat("_EyeDispersion",0.00001f);
                f.Same("Dispersion converges continuously to zero",noDispersion,f.Capture("eye-dispersion-near-zero",eye),0.00005);
                eye.SetFloat("_EyeDispersion",1); Shot dispersion=f.Capture("eye-dispersion-one",eye);
                f.Different("RGB dispersion changes interior",noDispersion,dispersion,0.0000001);
                f.Sheet("dispersion-comparison",2,noDispersion,dispersion);
                eye.SetFloat("_EyeDispersion",0); eye.SetFloat("_EyeOpticsMode",0);
                Shot reference=f.Capture("eye-off-reference",eye);
                eye.SetFloat("_EyeOpticsMode",1); eye.SetFloat("_IrisDepth",0);
                f.Same("Zero depth equals optics off",reference,f.Capture("eye-zero-depth",eye));
                eye.SetFloat("_IrisDepth",.25f); eye.SetFloat("_EyeOpticsStrength",0);
                f.Same("Zero strength equals optics off",reference,f.Capture("eye-zero-strength",eye));
                eye.SetFloat("_EyeOpticsStrength",1); eye.SetFloat("_EyeUVMaxX",0);
                f.Same("Invalid safe bounds fall back",reference,f.Capture("eye-invalid-bounds",eye));
                eye.SetFloat("_EyeUVMaxX",1);
                foreach(float angle in new[]{70f,82f,88f}) { f.Orbit(angle); f.Capture("eye-grazing-"+angle,eye); }
                f.Orbit(35); eye.SetFloat("_EyeDebugView",1); eye.SetFloat("_EyeOpticsMode",0);
                Shot alpha=f.Capture("eye-alpha-original",eye);
                eye.SetFloat("_EyeOpticsMode",1); eye.SetFloat("_EyeDispersion",1); eye.SetFloat("_ClearCoat",1);
                f.Same("Optics/coat preserve alpha pixels",alpha,f.Capture("eye-alpha-optical",eye));
                eye.SetFloat("_EyeDebugView",0); eye.SetFloat("_ClearCoat",0); eye.SetFloat("_EyeDispersion",0);
                eye.SetFloat("_EyeOpticsMode",0); eye.SetColor("_BaseColor",Color.black); eye.SetFloat("_IndirectDiffuseIntensity",0);
                f.stencilProbe.SetActive(true);
                Shot stencil=f.Capture("eye-stencil-original",eye);
                eye.SetFloat("_EyeOpticsMode",1); eye.SetFloat("_EyeDispersion",1);
                Shot stencilOptical=f.Capture("eye-stencil-optical",eye);
                f.Same("Actual stencil invariant under refraction",stencil,stencilOptical);
                f.Check("Actual named StencilMask writes pixels",stencil.brightPixels,stencil.brightPixels>10000);
                f.stencilProbe.SetActive(false);

                // Orthographic rays stay parallel when moving along the viewing axis.
                eye.SetColor("_BaseColor",Color.white); eye.SetFloat("_IndirectDiffuseIntensity",1); eye.SetFloat("_EyeDispersion",0);
                eye.SetFloat("_EyeDebugView",4); f.Orbit(25);
                Shot near=f.Capture("eye-ortho-near-shift",eye);
                f.camera.transform.position*=2;
                f.Same("Orthographic UV shift independent of camera distance",near,f.Capture("eye-ortho-far-shift",eye),0.0001);
                f.Orbit(25);
                f.Same("UV shift stable at half resolution",near,f.CaptureResized("eye-256-shift",eye,256),0.004);
                f.Same("UV shift stable at double resolution",near,f.CaptureResized("eye-1024-shift",eye,1024),0.004);
                f.patch.transform.localScale=Vector3.one*2;f.camera.orthographicSize*=2;f.camera.transform.position*=2;
                f.Same("UV chart invariant to uniform world scale",near,f.Capture("eye-double-world-scale",eye),0.0001);
                f.patch.transform.localScale=Vector3.one;f.camera.orthographicSize/=2;
                f.camera.orthographic=false; f.Orbit(25); f.camera.fieldOfView=36;
                Shot perspective=f.Capture("eye-perspective-shift",eye);
                f.camera.fieldOfView=58; Shot wide=f.Capture("eye-perspective-wide-shift",eye);
                // The central surface point/ray is the same; compare a small interior neighborhood, not image size.
                double centerDelta=0;
                for(int y=Size/2-2;y<Size/2+2;y++)for(int x=Size/2-2;x<Size/2+2;x++)
                {int n=y*Size+x;centerDelta+=Math.Abs(perspective.pixels[n].r-wide.pixels[n].r)+Math.Abs(perspective.pixels[n].g-wide.pixels[n].g);}
                centerDelta/=32;f.Check("Same central ray independent of FOV",centerDelta,centerDelta<.002);
                f.camera.fieldOfView=36;
                eye.SetFloat("_EyeDebugView",2);Shot fullViewport=f.Capture("eye-viewport-full",eye);
                f.camera.rect=new Rect(.33f,0,1,1);f.Capture("eye-viewport-maker",eye);
                f.camera.rect=new Rect(0,0,1,1);f.Same("Viewport restore preserves eye mask",fullViewport,f.Capture("eye-viewport-restored",eye));
                eye.SetFloat("_EyeDebugView",0); eye.SetFloat("_ClearCoat",1);
                Shot hero=f.Capture("eye-final-perspective",eye); f.Check("Final eye image nonblank",hero.brightPixels,hero.brightPixels>10000);
                SurfaceChecks(f);
                // Actual X mask passes feed the existing union-distance provider.
                f.camera.orthographic=true; f.camera.orthographicSize=1.05f; f.Orbit(0);
                Material sclera=f.Material("EyeWX"); sclera.SetTexture("_MainTex",f.iris); sclera.SetColor("_BaseColor",Color.black);
                f.patch.GetComponent<Renderer>().sharedMaterial=sclera;
                var inner=Object.Instantiate(f.patch); inner.name="Nested EyeX writer"; inner.transform.localScale=Vector3.one*.45f;
                inner.transform.position=new Vector3(0,0,-.01f);
                eye.SetColor("_BaseColor",Color.black);eye.SetFloat("_ClearCoat",0);eye.SetFloat("_EyeOpticsMode",0);
                inner.GetComponent<Renderer>().sharedMaterial=eye;
                var hair=GameObject.CreatePrimitive(PrimitiveType.Quad);hair.layer=f.layer;hair.transform.position=new Vector3(0,0,-.2f);hair.transform.localScale=Vector3.one*2;
                Material hairMaterial=f.Material("HairX"); hairMaterial.SetFloat("_DebugView",6);hairMaterial.SetTexture("_EmissionMask",Texture2D.whiteTexture);
                hairMaterial.SetFloat("_EmissionIntensity",1);hairMaterial.SetColor("_EmissionColor",new Color(.4f,.2f,.1f,1));
                hairMaterial.SetFloat("_HairFrontMode",2);hairMaterial.SetFloat("_HairFrontOpacity",.2f);hairMaterial.SetFloat("_HairFeatherWidth",24);
                f.subject=hair.GetComponent<Renderer>();
                var helper=f.camera.gameObject.AddComponent<TomShadersX.TomHairFeatherCamera>();
                helper.fieldShader=Shader.Find("Hidden/TomX/HairFeather");helper.receivers=new[]{f.subject};
                var outerSource=new TomShadersX.TomHairFeatherCamera.Source {renderer=f.patch.GetComponent<Renderer>(),passName="StencilMask"};
                var innerSource=new TomShadersX.TomHairFeatherCamera.Source {renderer=inner.GetComponent<Renderer>(),passName="StencilMask"};
                helper.sources=new[]{outerSource,innerSource};
                Shot union=f.Capture("hair-eyeX-eyeWX-union",hairMaterial);
                f.Check("Both X writers replay named mask passes",helper.SourceDraws,helper.Active&&helper.SourceDraws==2);
                helper.sources=new[]{outerSource};
                f.Same("Nested iris does not create an internal feather edge",union,f.Capture("hair-eyeWX-outer-union",hairMaterial));
                hairMaterial.SetFloat("_HairFrontMode",1);Shot hard=f.Capture("hair-X-writers-hard",hairMaterial);
                f.Different("X eye union actually feathers",hard,union);
                hairMaterial.SetFloat("_HairFrontMode",2);innerSource.passName="MissingXMask";helper.sources=new[]{outerSource,innerSource};
                f.Same("Missing named X mask falls back to Hard",hard,f.Capture("hair-X-writer-missing-pass",hairMaterial));
                f.Check("Missing mask fallback reported",helper.MissingPassSources,!helper.Active&&helper.MissingPassSources==1);
                File.WriteAllText(Path.Combine(f.output,"compilation.txt"),compilation+"\n"+TomXCoatEyeBuild.Validate());
            }
            finally { f.Save(); }
            int failed=f.report.checks.FindAll(c=>!c.passed).Count;
            if(failed>0) throw new Exception(failed+" coat/eye checks failed. "+f.output);
            return f.report.checks.Count+" checks passed; "+f.report.shots.Count+" Unity GPU captures. "+f.output;
        }
    }

    static void SurfaceChecks(Fixture f)
    {
        var eye=f.Material("EyeX");eye.SetTexture("_MainTex",f.iris);eye.SetColor("_BaseColor",Color.white);
        eye.SetFloat("_EyeOpticsMode",1);eye.SetFloat("_ClearCoat",0);eye.SetFloat("_EyeDebugView",4);
        eye.SetFloat("_IrisDepth",.12f);eye.SetFloat("_EyeRefractionIOR",1);
        f.camera.orthographic=true;f.camera.orthographicSize=.94f;f.Orbit(40);
        Shot refractionLow=f.Capture("surface-ior-refraction-1",eye);
        double last=CenterShift(refractionLow);
        foreach(float ior in new[]{1.33f,1.5f,2.5f})
        {
            eye.SetFloat("_EyeRefractionIOR",ior);Shot shift=f.Capture("surface-ior-refraction-"+ior.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),eye);
            double current=CenterShift(shift);
            f.Check("Higher refraction IOR reduces central parallax: "+ior,current,current<last && current>0);last=current;
        }
        eye.SetFloat("_EyeRefractionIOR",1.376f);eye.SetFloat("_ClearCoat",1);eye.SetFloat("_ClearCoatIOR",1);
        Shot coatLow=f.Capture("surface-coat-ior-1-shift",eye);
        eye.SetFloat("_ClearCoatIOR",2.5f);
        f.Same("Coat IOR cannot change optical UV",coatLow,f.Capture("surface-coat-ior-2.5-shift",eye));
        eye.SetFloat("_EyeDebugView",0);eye.SetFloat("_ClearCoat",0);eye.SetFloat("_EyeDispersion",1);
        Shot spectral=f.Capture("surface-coat-ior-2.5-dispersion",eye);eye.SetFloat("_ClearCoatIOR",1);
        f.Same("Coat IOR cannot change dispersed iris RGB",spectral,f.Capture("surface-coat-ior-1-dispersion",eye));
        eye.SetFloat("_ClearCoat",1);eye.SetFloat("_ClearCoatDebugView",2);
        Shot reflectionLow=f.Capture("surface-coat-reflection-ior-1",eye);eye.SetFloat("_ClearCoatIOR",2.5f);
        f.Different("Coat IOR still changes surface reflection",reflectionLow,f.Capture("surface-coat-reflection-ior-2.5",eye));
        eye.SetFloat("_EyeRefractionIOR",1);Shot reflection=f.Capture("surface-coat-refraction-ior-1",eye);
        eye.SetFloat("_EyeRefractionIOR",2.5f);
        f.Same("Refraction IOR cannot change coat reflection",reflection,f.Capture("surface-coat-refraction-ior-2.5",eye));
        eye.SetFloat("_ClearCoatDebugView",0);eye.SetFloat("_ClearCoat",0);eye.SetFloat("_EyeDispersion",0);
        eye.SetFloat("_EyeRefractionIOR",1.376f);eye.SetFloat("_ClearCoatIOR",1.376f);eye.SetFloat("_IrisDepth",.25f);

        var shapes=new List<Shot>();var maps=new List<Shot>();
        var heightMaps=new Texture2D[3];var packedMaps=new Texture2D[3];
        string[] names={"flat","cone","bowl"};
        for(int shape=0;shape<3;shape++)
        {
            heightMaps[shape]=f.Own(SurfaceMap(shape,false,false));packedMaps[shape]=f.Own(SurfaceMap(shape,true,false));
            File.WriteAllBytes(Path.Combine(f.output,"template-"+names[shape]+"-height.png"),heightMaps[shape].EncodeToPNG());
            File.WriteAllBytes(Path.Combine(f.output,"template-"+names[shape]+"-depthRA.png"),packedMaps[shape].EncodeToPNG());
        }
        foreach(float angle in new[]{-40f,0f,40f})
        {
            f.Orbit(angle);
            for(int shape=0;shape<3;shape++)
            {
                eye.SetFloat("_IrisDepthShape",shape);eye.SetFloat("_UseEyeSurfaceMask",0);
                shapes.Add(f.Capture("surface-procedural-"+angle+"-"+names[shape],eye));
                eye.SetTexture("_EyeSurfaceMap",heightMaps[shape]);eye.SetFloat("_UseEyeSurfaceMask",1);
                maps.Add(f.Capture("surface-height-map-"+angle+"-"+names[shape],eye));
            }
        }
        f.Sheet("surface-procedural-shapes",3,shapes.ToArray());f.Sheet("surface-painted-shapes",3,maps.ToArray());
        f.Different("Shallow cone differs from flat",shapes[6],shapes[7]);
        f.Different("Shallow bowl differs from cone",shapes[7],shapes[8]);

        f.Orbit(35);eye.SetFloat("_EyeDebugView",4);
        for(int shape=0;shape<3;shape++)
        {
            eye.SetFloat("_IrisDepthShape",shape);eye.SetFloat("_UseEyeSurfaceMask",0);
            Shot procedural=f.Capture("surface-shift-procedural-"+names[shape],eye);
            eye.SetFloat("_UseEyeSurfaceMask",1);eye.SetTexture("_EyeSurfaceMap",heightMaps[shape]);eye.SetFloat("_EyeSurfaceMapMode",0);
            Shot gray=f.Capture("surface-shift-height-"+names[shape],eye);
            double delta=CenterDifference(procedural,gray,48);
            f.Check("Painted/procedural interior agrees: "+names[shape],delta,delta<.001);
            eye.SetTexture("_EyeSurfaceMap",packedMaps[shape]);eye.SetFloat("_EyeSurfaceMapMode",1);
            Shot packed=f.Capture("surface-shift-depthRA-"+names[shape],eye);
            delta=CenterDifference(gray,packed,48);
            f.Check("Height and depthRA encoding agree: "+names[shape],delta,delta<.001);
            eye.SetFloat("_EyeSurfaceMapMode",0);
        }

        // Rotation and nonuniform/negative MainTex ST must affect both chart and template.
        eye.SetFloat("_EyeDebugView",7);eye.SetFloat("_rotation",.173f);
        eye.SetTextureScale("_MainTex",new Vector2(-.83f,.76f));eye.SetTextureOffset("_MainTex",new Vector2(.93f,.09f));
        eye.SetFloat("_UseEyeSurfaceMask",0);eye.SetFloat("_IrisDepthShape",2);
        Shot transformed=f.Capture("surface-alignment-procedural",eye);
        eye.SetFloat("_UseEyeSurfaceMask",1);eye.SetTexture("_EyeSurfaceMap",heightMaps[2]);
        f.Same("Height map follows iris rotation and MainTex ST",transformed,f.Capture("surface-alignment-height-map",eye),.001);
        eye.SetTextureScale("_MainTex",Vector2.one);eye.SetTextureOffset("_MainTex",Vector2.zero);eye.SetFloat("_rotation",0);

        var custom=f.Own(SurfaceMap(2,true,true));eye.SetTexture("_EyeSurfaceMap",custom);eye.SetFloat("_EyeSurfaceMapMode",1);
        File.WriteAllBytes(Path.Combine(f.output,"template-asymmetric-depthRA.png"),custom.EncodeToPNG());
        eye.SetFloat("_EyeDebugView",4);Shot customShift=f.Capture("surface-custom-shift",eye);
        eye.SetFloat("_IrisCenterX",.02f);eye.SetFloat("_IrisCenterY",.02f);eye.SetFloat("_IrisDepthShape",0);eye.SetFloat("_EyeOpticsEdgeFade",.5f);
        f.Same("Custom area replaces ellipse center, shape and edge cap",customShift,f.Capture("surface-custom-ignores-ellipse",eye));
        eye.SetFloat("_EyeDebugView",0);Shot customRGB=f.Capture("surface-custom-final",eye);
        eye.SetFloat("_ClearCoat",1);Shot customWet=f.Capture("surface-custom-clearcoat",eye);eye.SetFloat("_ClearCoat",0);
        eye.SetFloat("_EyeOpticsMode",0);Shot noOptics=f.Capture("surface-custom-optics-off",eye);
        f.Different("Custom painted area moves interior",noOptics,customRGB);
        f.Sheet("surface-custom-comparison",3,noOptics,customRGB,customWet);
        eye.SetFloat("_EyeOpticsMode",1);eye.SetTexture("_EyeSurfaceMap",f.Own(Solid(new Color(1,0,0,0))));
        f.Same("Zero A disables optics with nonzero R depth",noOptics,f.Capture("surface-zero-region",eye));
        eye.SetFloat("_EyeSurfaceMapMode",0);eye.SetTexture("_EyeSurfaceMap",Texture2D.whiteTexture);
        f.Same("White height gives zero recess",noOptics,f.Capture("surface-white-height",eye));
        eye.SetTexture("_EyeSurfaceMap",f.Own(Solid(new Color(1,1,1,0))));
        eye.SetFloat("_EyeSurfaceMapMode",1);eye.SetFloat("_EyeDebugView",8);
        Shot noArea=f.Capture("surface-debug-empty-region",eye);
        eye.SetTexture("_EyeSurfaceMap",custom);
        f.Different("SurfaceMask debug displays custom A",noArea,f.Capture("surface-debug-custom-region",eye));

        eye.SetFloat("_IrisCenterX",.5f);eye.SetFloat("_IrisCenterY",.5f);eye.SetFloat("_EyeOpticsEdgeFade",.08f);
        eye.SetTexture("_EyeSurfaceMap",heightMaps[0]);eye.SetFloat("_EyeSurfaceMapMode",0);
        for(int mode=0;mode<2;mode++)
        {
            eye.SetFloat("_UseEyeSurfaceMask",mode);eye.SetFloat("_EyeDebugView",4);eye.SetFloat("_IrisDepth",.06f);
            double low=CenterShift(f.Capture("surface-gain-"+mode+"-low",eye));
            eye.SetFloat("_IrisDepth",.12f);double high=CenterShift(f.Capture("surface-gain-"+mode+"-high",eye));
            f.Check("Shared IrisDepth doubles flat displacement in mode "+mode,high/low,Math.Abs(high/low-2)<.002);
            eye.SetFloat("_IrisDepth",0);eye.SetFloat("_EyeDebugView",0);
            f.Same("Shared zero depth disables mode "+mode,noOptics,f.Capture("surface-gain-"+mode+"-zero",eye));
        }
        eye.SetFloat("_IrisDepth",.25f);eye.SetFloat("_EyeOpticsStrength",0);
        f.Same("Zero strength disables painted surface",noOptics,f.Capture("surface-map-zero-strength",eye));
        eye.SetFloat("_EyeOpticsStrength",1);eye.SetTexture("_EyeSurfaceMap",heightMaps[2]);
        eye.SetFloat("_EyeDebugView",4);
        for(int mode=0;mode<3;mode++)
        {
            eye.SetFloat("_UseEyeSurfaceMask",mode==2 ? 1 : 0);eye.SetFloat("_IrisDepthShape",mode==0 ? 1 : 2);
            eye.SetFloat("_EyeRefractionIOR",1.33f);eye.SetFloat("_ClearCoatIOR",1);
            Shot low=f.Capture("surface-new-mode-"+mode+"-ior-low",eye);
            eye.SetFloat("_ClearCoatIOR",2.5f);
            f.Same("New surface mode ignores coat IOR: "+mode,low,f.Capture("surface-new-mode-"+mode+"-coat-high",eye));
            eye.SetFloat("_EyeRefractionIOR",2.5f);Shot high=f.Capture("surface-new-mode-"+mode+"-refraction-high",eye);
            f.Check("New surface parallax weakens with refraction IOR: "+mode,CenterShift(high),CenterShift(high)<CenterShift(low));
        }
        eye.SetFloat("_EyeRefractionIOR",1.376f);eye.SetFloat("_ClearCoatIOR",1.376f);eye.SetFloat("_EyeDebugView",0);
        eye.SetFloat("_EyeUVMaxX",0);
        f.Same("Painted surface honors invalid atlas fallback",noOptics,f.Capture("surface-map-invalid-atlas",eye));eye.SetFloat("_EyeUVMaxX",1);

        eye.SetFloat("_EyeDebugView",1);eye.SetFloat("_EyeOpticsMode",0);Shot alpha=f.Capture("surface-alpha-reference",eye);
        eye.SetFloat("_EyeOpticsMode",1);eye.SetFloat("_EyeDispersion",1);eye.SetFloat("_ClearCoat",1);
        f.Same("Painted surface preserves alpha",alpha,f.Capture("surface-alpha-painted",eye));
        eye.SetFloat("_EyeDebugView",0);eye.SetColor("_BaseColor",Color.black);eye.SetFloat("_ClearCoat",0);eye.SetFloat("_IndirectDiffuseIntensity",0);
        f.stencilProbe.SetActive(true);eye.SetFloat("_EyeOpticsMode",0);Shot stencil=f.Capture("surface-stencil-reference",eye);
        eye.SetFloat("_EyeOpticsMode",1);
        f.Same("Painted surface preserves actual stencil",stencil,f.Capture("surface-stencil-painted",eye));f.stencilProbe.SetActive(false);
        eye.SetColor("_BaseColor",Color.white);eye.SetFloat("_IndirectDiffuseIntensity",1);eye.SetFloat("_EyeDispersion",0);
        Shot dispersedOff=f.Capture("surface-map-dispersion-off",eye);eye.SetFloat("_EyeDispersion",.00001f);
        f.Same("Painted dispersion continuous at zero",dispersedOff,f.Capture("surface-map-dispersion-near-zero",eye),.00005);
        eye.SetFloat("_EyeDispersion",1);f.Different("Painted surface disperses",dispersedOff,f.Capture("surface-map-dispersion-on",eye),.0000001);
        eye.SetFloat("_EyeDispersion",0);eye.SetFloat("_EyeDebugView",4);f.Orbit(25);
        Shot shiftReference=f.Capture("surface-map-shift-reference",eye);
        f.camera.transform.position*=2;f.Same("Painted orthographic distance invariant",shiftReference,f.Capture("surface-map-ortho-far",eye),.0001);f.Orbit(25);
        f.Same("Painted shift at half resolution",shiftReference,f.CaptureResized("surface-map-256",eye,256),.004);
        f.Same("Painted shift at double resolution",shiftReference,f.CaptureResized("surface-map-1024",eye,1024),.004);
        f.patch.transform.localScale=Vector3.one*2;f.camera.orthographicSize*=2;f.camera.transform.position*=2;
        f.Same("Painted shift uniform world scale invariant",shiftReference,f.Capture("surface-map-scale2",eye),.0001);
        f.patch.transform.localScale=Vector3.one;f.camera.orthographicSize/=2;f.Orbit(25);
        f.camera.orthographic=false;f.camera.fieldOfView=36;
        Shot perspective=f.Capture("surface-map-perspective",eye);f.camera.transform.position*=2;
        double centerError=CenterDifference(perspective,f.Capture("surface-map-perspective-far",eye),2);
        f.Check("Painted same central ray at different distances",centerError,centerError<.002);f.Orbit(25);
        f.camera.fieldOfView=58;centerError=CenterDifference(perspective,f.Capture("surface-map-perspective-wide",eye),2);
        f.Check("Painted same central ray at different FOV",centerError,centerError<.002);
        f.camera.fieldOfView=36;f.camera.orthographic=true;
        f.camera.rect=new Rect(.33f,0,1,1);f.Capture("surface-map-maker-viewport",eye);f.camera.rect=new Rect(0,0,1,1);
        f.Same("Painted viewport restore",shiftReference,f.Capture("surface-map-viewport-restored",eye));
        eye.SetFloat("_EyeDebugView",0);
        foreach(float angle in new[]{70f,82f,88f}){f.Orbit(angle);f.Capture("surface-map-grazing-"+angle,eye);}

        // Identical direct lighting through Base and Add must use the same surface hit.
        f.Orbit(35);eye.SetFloat("_IndirectDiffuseIntensity",0);eye.SetFloat("_MainLightIntensity",1);
        Shot directBase=f.Capture("surface-map-light-base",eye);
        var add=f.Own(new GameObject("Matched optical Add light")).AddComponent<Light>();
        add.type=LightType.Directional;add.renderMode=LightRenderMode.ForcePixel;add.cullingMask=1<<f.layer;
        add.transform.rotation=f.sun.transform.rotation;add.intensity=.5f;add.color=f.sun.color;
        eye.SetFloat("_MainLightIntensity",0);eye.SetFloat("_AdditionalLightIntensity",2);
        f.Same("Base and Add share painted optical mapping",directBase,f.Capture("surface-map-light-add",eye),.0001);add.enabled=false;
    }

    static double CenterShift(Shot shot)
    {
        double total=0;
        for(int y=Size/2-4;y<Size/2+4;y++)for(int x=Size/2-4;x<Size/2+4;x++)
        { Color p=shot.pixels[y*Size+x];total+=Math.Sqrt((p.r-.5)*(p.r-.5)+(p.g-.5)*(p.g-.5)); }
        return total/64;
    }
    static double CenterDifference(Shot a,Shot b,int radius)
    {
        double total=0;
        for(int y=Size/2-radius;y<Size/2+radius;y++)for(int x=Size/2-radius;x<Size/2+radius;x++)
        {int n=y*Size+x;total+=Math.Max(Math.Abs(a.pixels[n].r-b.pixels[n].r),Math.Abs(a.pixels[n].g-b.pixels[n].g));}
        return total/(4*radius*radius);
    }
    internal static Texture2D SurfaceMap(int shape,bool packed,bool asymmetric)
    {
        const int n=1024;var texture=new Texture2D(n,n,TextureFormat.RGBA32,true,true);texture.wrapMode=TextureWrapMode.Clamp;
        var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float u=(x+.5f)/n,v=(y+.5f)/n;
            float qx=(u-(asymmetric ? .60f : .5f))/(asymmetric ? .27f : .37f);
            float qy=(v-.5f)/(asymmetric ? .32f : .37f);if(asymmetric)qx+=.2f*Mathf.Sin(qy*3);
            float r=Mathf.Sqrt(qx*qx+qy*qy);
            float mask=1-Mathf.SmoothStep(0,1,(r-.92f)/.08f);
            float depth=shape==0 ? mask : Mathf.Clamp01(shape==1 ? 1-r : 1-r*r);
            // Curved grayscale profiles already taper to zero; alpha is independent in packed mode.
            pixels[y*n+x]=packed ? new Color(shape==0 ? 1 : depth,0,0,mask) : new Color(1-depth,1-depth,1-depth,1);
        }
        texture.SetPixels(pixels);texture.Apply(true);return texture;
    }

    static bool Finite(float v) { return !float.IsNaN(v)&&!float.IsInfinity(v); }
    static Color Display(Color p)
    {
        p*=1.5f;
        return new Color(Mathf.LinearToGammaSpace(Mathf.Max(0,p.r)/(1+Mathf.Max(0,p.r))),
            Mathf.LinearToGammaSpace(Mathf.Max(0,p.g)/(1+Mathf.Max(0,p.g))),Mathf.LinearToGammaSpace(Mathf.Max(0,p.b)/(1+Mathf.Max(0,p.b))),1);
    }
    static double Difference(Shot a,Shot b)
    {
        double sum=0; for(int n=0;n<a.pixels.Length;n++)
            sum+=Math.Max(Math.Abs(a.pixels[n].r-b.pixels[n].r),Math.Max(Math.Abs(a.pixels[n].g-b.pixels[n].g),Math.Abs(a.pixels[n].b-b.pixels[n].b)));
        return sum/a.pixels.Length;
    }
    internal static Texture2D Solid(Color color)
    {
        var t=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true); t.SetPixel(0,0,color); t.Apply(); return t;
    }
    static Texture2D Normal()
    {
        const int n=128; var t=new Texture2D(n,n,TextureFormat.RGBA32,true,true); var p=new Color[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++) p[y*n+x]=new Color(1,.5f+.18f*Mathf.Cos(y*.18f),1,.5f+.18f*Mathf.Sin(x*.18f));
        t.SetPixels(p);t.Apply(true);return t;
    }
    internal static Texture2D Iris()
    {
        const int n=1024; var t=new Texture2D(n,n,TextureFormat.RGBA32,true,true);t.wrapMode=TextureWrapMode.Clamp;
        var p=new Color[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            float u=(x+.5f)/n-.5f,v=(y+.5f)/n-.5f,r=Mathf.Sqrt(u*u+v*v)/.37f,a=Mathf.Atan2(v,u);
            float fibers=.5f+.22f*Mathf.Sin(a*107+Mathf.Sin(r*27)*1.2f)+.15f*Mathf.Sin(a*173+r*14)+.1f*Mathf.Cos(a*59-r*41);
            Color c=Color.Lerp(new Color(.025f,.14f,.19f),new Color(.08f,.55f,.40f),Mathf.Clamp01(fibers));
            c=Color.Lerp(c,new Color(.38f,.20f,.045f),Mathf.Exp(-Mathf.Pow((r-.38f)*7,2))*.7f);
            c*=Mathf.Lerp(.16f,1,Mathf.SmoothStep(0,1,(1-r)/.12f));
            c=Color.Lerp(new Color(.0015f,.003f,.004f),c,Mathf.SmoothStep(0,1,(r-.26f)/.025f));
            c=Color.Lerp(c,new Color(.78f,.81f,.79f),Mathf.SmoothStep(0,1,(r-1)/.025f));
            float aperture=Mathf.Sqrt(u*u/(.49f*.49f)+v*v/(.45f*.45f));
            c.a=1-Mathf.SmoothStep(0,1,(aperture-.96f)/.03f);p[y*n+x]=c;
        }
        t.SetPixels(p);t.Apply(true);return t;
    }
    internal static Mesh EyePatch(int n = 128)
    {
        var vertices=new List<Vector3>(); var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int y=0;y<=n;y++) for(int x=0;x<=n;x++)
        {
            float u=(float)x/n,v=(float)y/n,px=(u-.5f)*1.7f,py=(v-.5f)*1.7f;
            float z=Mathf.Sqrt(Mathf.Max(.01f,1.6f*1.6f-px*px-py*py));
            vertices.Add(new Vector3(px,py,1.6f-z)); normals.Add(new Vector3(px,py,-z).normalized);uv.Add(new Vector2(u,v));
        }
        for(int y=0;y<n;y++)for(int x=0;x<n;x++) {int a=y*(n+1)+x,b=a+1,c=a+n+1;triangles.AddRange(new[]{a,c,b,b,c,c+1});}
        var mesh=new Mesh {name="Single continuous spherical iris chart"};mesh.SetVertices(vertices);mesh.SetNormals(normals);
        mesh.SetUVs(0,uv);mesh.SetUVs(1,uv);mesh.SetUVs(2,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    internal static Cubemap Environment()
    {
        const int n=128;var cube=new Cubemap(n,TextureFormat.RGBAHalf,true);cube.wrapMode=TextureWrapMode.Clamp;
        for(int face=0;face<6;face++)
        {
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=2f*(x+.5f)/n-1,v=2f*(y+.5f)/n-1;Vector3 d;
                switch(face) {case 0:d=new Vector3(1,-v,-u);break;case 1:d=new Vector3(-1,-v,u);break;case 2:d=new Vector3(u,1,v);break;case 3:d=new Vector3(u,-1,-v);break;case 4:d=new Vector3(u,-v,1);break;default:d=new Vector3(-u,-v,-1);break;}
                d.Normalize();float front=Mathf.Max(0,-d.z);
                float key=Mathf.Exp(-Mathf.Pow((d.x-.25f)*9,8)-Mathf.Pow((d.y-.4f)*6,8))*front;
                float fill=Mathf.Exp(-Mathf.Pow((d.x+.5f)*18,8)-Mathf.Pow((d.y-.15f)*3,8))*front;
                pixels[y*n+x]=new Color(.025f,.035f,.05f,1)+new Color(12,11,9,0)*key+new Color(3,5,8,0)*fill;
            }
            cube.SetPixels(pixels,(CubemapFace)face);
        }
        cube.Apply(true,false);return cube;
    }
}

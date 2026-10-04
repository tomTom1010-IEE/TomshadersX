using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TomShadersX;
using Object = UnityEngine.Object;

public static class TomXHairValidation
{
    const int Size = 256;
    [Serializable] class Check { public string name; public double value; public bool passed; }
    [Serializable] class Timing { public string mode; public double cameraRenderCpuMedianMs; public int sourceDraws, filterPasses; }
    [Serializable] class Report
    {
        public string unity, api;
        public List<Check> checks = new List<Check>();
        public List<string> images = new List<string>();
        public List<Timing> timings = new List<Timing>();
    }
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before HairX tests.");
        string compiled = TomXStageTwoValidation.Validate("tom/HairX");
        int layer = -1; var occupied = new HashSet<int>();
        foreach (GameObject g in Object.FindObjectsOfType<GameObject>()) occupied.Add(g.layer);
        for (int n = 31; n >= 8; n--) if (!occupied.Contains(n)) { layer = n; break; }
        if (layer < 0) throw new Exception("No unused render layer.");
        Scene previous = SceneManager.GetActiveScene();
        var masks = new Dictionary<Light, int>();
        foreach (Light l in Object.FindObjectsOfType<Light>()) { masks.Add(l, l.cullingMask); l.cullingMask &= ~(1 << layer); }
        int oldLights = QualitySettings.pixelLightCount;
        ShadowQuality oldShadows = QualitySettings.shadows;
        float oldDistance = QualitySettings.shadowDistance;
        RenderTexture oldRT = RenderTexture.active;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var garbage = new List<Object>();
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "CodexBridge/Reports/XHair-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        var report = new Report { unity = Application.unityVersion, api = SystemInfo.graphicsDeviceType.ToString() };
        Action<string, double, bool> check = (name, value, passed) => report.checks.Add(new Check {name=name,value=value,passed=passed});
        Action<string, Color[], Color[]> equal = (name, a, b) => { double e=Difference(a,b); check(name,e,e<0.003); };
        try
        {
            SceneManager.SetActiveScene(scene);
            QualitySettings.pixelLightCount = 8; QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 30;
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black; RenderSettings.fog = false;
            Camera camera = new GameObject("Hair regression camera").AddComponent<Camera>();
            camera.enabled = false; camera.cullingMask = 1 << layer;
            camera.transform.position = new Vector3(0,0,-3); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.12f,.2f,1);
            camera.renderingPath = RenderingPath.Forward; camera.orthographic = true; camera.orthographicSize = .75f;
            camera.allowHDR = true; camera.allowMSAA = false; camera.nearClipPlane = .05f; camera.farClipPlane = 20;
            var rt = new RenderTexture(Size,Size,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            rt.Create(); garbage.Add(rt); camera.targetTexture=rt;
            var read = new Texture2D(Size,Size,TextureFormat.RGBAFloat,false,true); garbage.Add(read);
            var png = new Texture2D(Size,Size,TextureFormat.RGB24,false); garbage.Add(png);
            Func<string,Color[]> capture = name => {
                camera.Render(); camera.Render(); RenderTexture.active=rt;
                read.ReadPixels(new Rect(0,0,Size,Size),0,0); read.Apply(false);
                Color[] pixels=read.GetPixels();
                foreach (Color p in pixels) if (!Finite(p.r)||!Finite(p.g)||!Finite(p.b)) throw new Exception("Nonfinite pixels: "+name);
                png.SetPixels(pixels); png.Apply(false); File.WriteAllBytes(Path.Combine(folder,name+".png"),png.EncodeToPNG());
                report.images.Add(name); return pixels;
            };
            var hair=GameObject.CreatePrimitive(PrimitiveType.Quad); hair.layer=layer;
            var renderer=hair.GetComponent<Renderer>();
            var material=new Material(Shader.Find("tom/HairX")); garbage.Add(material); renderer.sharedMaterial=material;
            check("Feather midpoint defaults to 0.5",material.GetFloat("_HairFeatherThreshold"),material.GetFloat("_HairFeatherThreshold")==.5f);
            check("Feather power defaults to 1",material.GetFloat("_HairFeatherPower"),material.GetFloat("_HairFeatherPower")==1f);
            material.SetFloat("_DebugView",6); material.SetTexture("_EmissionMask",Texture2D.whiteTexture);
            material.SetFloat("_EmissionIntensity",1); material.SetColor("_EmissionColor",new Color(.7f,.35f,.15f,1));
            material.SetFloat("_VertexLightIntensity",0); material.SetFloat("_LightProbeBlend",0); material.SetFloat("_CustomSHVolumeBlend",0);
            var eye=GameObject.CreatePrimitive(PrimitiveType.Quad); eye.layer=layer;
            eye.transform.position=new Vector3(.12f,.11f,.25f); eye.transform.localScale=new Vector3(.65f,.5f,1);
            var eyeMaterial=new Material(Shader.Find("Hidden/TomX/HairStencilFixture")); garbage.Add(eyeMaterial);
            eye.GetComponent<Renderer>().sharedMaterial=eyeMaterial;
            var helper=camera.gameObject.AddComponent<TomHairFeatherCamera>();
            helper.enabled=false; helper.receivers=new[]{renderer};
            helper.sources=new[]{new TomHairFeatherCamera.Source {renderer=eye.GetComponent<Renderer>()}};
            hair.SetActive(false); Color[] background=capture("00-background"); hair.SetActive(true);
            Color[] full=capture("01-off");
            material.SetFloat("_HairFrontMode",1); material.SetFloat("_HairFrontOpacity",1);
            equal("Hard opacity 1 equals Off",full,capture("02-hard-one"));
            material.SetFloat("_HairFrontOpacity",0); Color[] zero=capture("03-hard-zero");
            double area=Difference(full,zero); check("Stencil writer visibly opens hair",area,area>.01);
            material.SetFloat("_HairFrontOpacity",.5f); Color[] half=capture("04-hard-half");
            equal("Single coverage, not repeated base compositing",Mix(zero,full,.5f),half);
            material.SetFloat("_HairFeatherThreshold",.25f); material.SetFloat("_HairFeatherPower",4);
            equal("Curve controls do not affect Hard",half,capture("04-hard-custom-curve"));
            material.SetFloat("_HairFrontMode",2);
            equal("Missing helper falls back to Hard",half,capture("05-no-provider"));
            helper.enabled=true; material.SetFloat("_HairFeatherWidth",0);
            equal("Width zero falls back to Hard",half,capture("06-width-zero"));
            check("Width zero has no auxiliary work",helper.FilterPasses,!helper.Active&&helper.FilterPasses==0);
            check("Width zero diagnostics explain fallback",0,helper.Status=="Hard: no visible HairX with Mode=2 and Width>0");
            material.SetFloat("_HairFeatherWidth",16);
            material.SetFloat("_HairFeatherThreshold",.5f); material.SetFloat("_HairFeatherPower",1);
            Color[] feather=capture("07-feather");
            check("Feather provider active",helper.SourceDraws,helper.Active&&helper.SourceDraws==1&&helper.FilterPasses>0);
            check("Active diagnostics report scheduling, not pixel acceptance",helper.VisibleSources,
                helper.Status.StartsWith("Scheduled:")&&helper.VisibleSources==1&&helper.LateSources==0&&helper.MissingPassSources==0);
            double featherChange=Difference(feather,half); check("Feather changes boundary",featherChange,featherChange>.001);
            int inward=0,outsideChanged=0,centerStable=0;
            for(int n=0;n<full.Length;n++)
            {
                bool inside=(full[n]-zero[n]).maxColorComponent>.02f;
                if (!inside && Delta(feather[n],full[n])>.01f) outsideChanged++;
                if (inside && Delta(feather[n],half[n])>.01f && Delta(feather[n],full[n])>.01f) inward++;
                if (inside && Delta(feather[n],half[n])<.005f) centerStable++;
            }
            check("Feather never leaks outside actual stencil",outsideChanged,outsideChanged==0);
            check("Feather has intermediate inward coverage",inward,inward>20);
            check("Feather interior retains selected opacity",centerStable,centerStable>50);
            int minX=Size,minY=Size,maxX=0,maxY=0;
            for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
                if(Delta(full[y*Size+x],zero[y*Size+x])>.02f)
                { minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y); }
            var expectedFeather=(Color[])full.Clone();
            for(int y=minY;y<=maxY;y++) for(int x=minX;x<=maxX;x++)
            {
                float d=Math.Min(Math.Min(x-minX+.5f,maxX-x+.5f),Math.Min(y-minY+.5f,maxY-y+.5f));
                expectedFeather[y*Size+x]=Color.Lerp(full[y*Size+x],half[y*Size+x],Mathf.SmoothStep(0,1,d/16f));
            }
            double featherShapeError=Difference(expectedFeather,feather);
            check("Feather matches inward rectangle-distance reference",featherShapeError,featherShapeError<.003);
            if(helper.MaskTexture) DumpField(helper.MaskTexture,folder+"/mask.png",1);
            if(helper.DistanceTexture) DumpField(helper.DistanceTexture,folder+"/distance.png",32);
            Color[] early=null,lateCurve=null,soft=null,hard=null,lightingCurve=null;
            RenderTexture originalDistance=helper.DistanceTexture;
            int originalSources=helper.SourceDraws,originalFilters=helper.FilterPasses;
            foreach(float threshold in new[]{.05f,.25f,.5f,.75f,.95f}) foreach(float power in new[]{.5f,1f,2f,4f})
            {
                material.SetFloat("_HairFeatherThreshold",threshold); material.SetFloat("_HairFeatherPower",power);
                string label="07-curve-t"+threshold.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)
                    +"-p"+power.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
                Color[] curved=capture(label);
                double error=Difference(RectangleFeather(full,zero,half,16,threshold,power),curved);
                check("Feather rectangle curve reference: "+label,error,error<.003);
                if(threshold==.25f&&power==1) early=curved;
                if(threshold==.75f&&power==1) lateCurve=curved;
                if(threshold==.5f&&power==.5f) soft=curved;
                if(threshold==.5f&&power==4) hard=curved;
                if(threshold==.25f&&power==2) lightingCurve=curved;
            }
            check("Midpoint control visibly changes feather",Difference(early,lateCurve),Difference(early,lateCurve)>.001);
            check("Power control visibly changes feather",Difference(soft,hard),Difference(soft,hard)>.001);
            check("Curve controls reuse the same distance target and draw count",helper.FilterPasses,
                helper.DistanceTexture==originalDistance&&helper.SourceDraws==originalSources&&helper.FilterPasses==originalFilters);
            material.SetFloat("_HairFeatherThreshold",.5f); material.SetFloat("_HairFeatherPower",1);
            equal("Reset curve defaults preserves original feather",feather,capture("07-curve-reset"));
            // Optional integration check against the installed xukmi writer, not a package dependency.
            Shader installedEye = Shader.Find("xukmi/EyePlus");
            if (installedEye)
            {
                var installedEyeMaterial = new Material(installedEye); garbage.Add(installedEyeMaterial);
                installedEyeMaterial.renderQueue=2474;
                installedEyeMaterial.SetFloat("_exppower",0);
                installedEyeMaterial.SetFloat("_isHighLight",0);
                eye.GetComponent<Renderer>().sharedMaterial=installedEyeMaterial;
                helper.sources[0].passName="Forward";
                material.SetFloat("_HairFrontMode",1);
                Color[] actualEyeHard=capture("07-xukmi-eye-hard");
                material.SetFloat("_HairFrontMode",2);
                Color[] actualEyeFeather=capture("07-xukmi-eye-feather");
                check("Installed EyePlus Forward pass exists",installedEyeMaterial.FindPass("Forward"),installedEyeMaterial.FindPass("Forward")>=0);
                check("Installed EyePlus 2474 / HairX 2475 schedules proxy",helper.SourceDraws,
                    helper.Active&&helper.SourceDraws==1&&helper.EarliestHairQueue==2475);
                check("Installed EyePlus has a visible feather transition",Difference(actualEyeHard,actualEyeFeather),Difference(actualEyeHard,actualEyeFeather)>.001);
                eye.GetComponent<Renderer>().sharedMaterial=eyeMaterial;
                helper.sources[0].passName="StencilMask";
            }
            camera.rect=new Rect(0,0,.75f,1); helper.Prepare();
            check("Partial viewport schedules field",helper.SourceDraws,helper.Active&&helper.SourceDraws==1);
            camera.rect=new Rect(0,0,1,1);
            helper.sources[0].passName="MissingDiagnosticPass"; helper.Prepare();
            check("Missing writer pass diagnostics",helper.MissingPassSources,
                !helper.Active&&helper.MissingPassSources==1&&helper.Status=="Hard: no eligible eye writer draws");
            helper.sources[0].passName="StencilMask";
            var unknownEye=GameObject.CreatePrimitive(PrimitiveType.Quad); unknownEye.layer=layer;
            unknownEye.transform.position=new Vector3(-.3f,-.3f,.2f);unknownEye.transform.localScale=new Vector3(.15f,.15f,1);
            unknownEye.GetComponent<Renderer>().sharedMaterial=eyeMaterial;
            material.SetFloat("_HairFrontMode",1); Color[] unknownHard=capture("07-unregistered-hard");
            material.SetFloat("_HairFrontMode",2); Color[] unknownFeather=capture("07-unregistered-feather");
            Vector3 unknownPixel=camera.WorldToScreenPoint(unknownEye.transform.position);
            int unknownIndex=(int)unknownPixel.y*Size+(int)unknownPixel.x;
            check("Unregistered isolated writer retains Hard",Delta(unknownHard[unknownIndex],unknownFeather[unknownIndex]),Delta(unknownHard[unknownIndex],unknownFeather[unknownIndex])<.003);
            unknownEye.SetActive(false);
            camera.orthographic=false;camera.fieldOfView=28;
            material.SetFloat("_HairFrontMode",0);Color[] perspectiveFull=capture("07-perspective-full");
            material.SetFloat("_HairFrontMode",1);material.SetFloat("_HairFrontOpacity",0);Color[] perspectiveZero=capture("07-perspective-zero");
            material.SetFloat("_HairFrontOpacity",.5f);Color[] perspectiveHalf=capture("07-perspective-half");
            material.SetFloat("_HairFrontMode",2);Color[] perspectiveFeather=capture("07-perspective-feather");
            double perspectiveError=Difference(RectangleFeather(perspectiveFull,perspectiveZero,perspectiveHalf,16),perspectiveFeather);
            check("Perspective camera feather alignment",perspectiveError,perspectiveError<.003);
            camera.orthographic=true;
            foreach(int mode in new[]{0,1,2})
            {
                material.SetFloat("_HairFrontMode",mode); camera.Render();
                var times=new List<double>();
                for(int n=0;n<9;n++) { var clock=Stopwatch.StartNew(); camera.Render(); clock.Stop(); times.Add(clock.Elapsed.TotalMilliseconds); }
                times.Sort(); report.timings.Add(new Timing {mode=mode.ToString(),cameraRenderCpuMedianMs=times[4],sourceDraws=helper.SourceDraws,filterPasses=helper.FilterPasses});
            }
            material.SetFloat("_HairFrontMode",1);
            equal("Turning feather off restores Hard",half,capture("08-feather-disabled"));
            check("Off releases auxiliary targets and commands",helper.FilterPasses,!helper.Active&&helper.DistanceTexture==null&&helper.FilterPasses==0&&helper.SourceDraws==0);
            check("Per-camera globals reset after rendering",Shader.GetGlobalVector("_TomHairFeatherParams").x,Shader.GetGlobalVector("_TomHairFeatherParams").x==0);
            eyeMaterial.renderQueue=2480; material.SetFloat("_HairFrontMode",2);
            equal("Late eye writer cannot affect earlier hair",full,capture("08-late-writer"));
            check("Late writers do not create proxy mask",helper.SourceDraws,!helper.Active&&helper.SourceDraws==0);
            check("Late writer diagnostics include rejection count",helper.LateSources,
                helper.LateSources==1&&helper.Status=="Hard: no eligible eye writer draws");
            eyeMaterial.renderQueue=2460; material.SetFloat("_HairFrontMode",1);
            helper.enabled=false;

            var late=GameObject.CreatePrimitive(PrimitiveType.Quad); late.layer=layer; late.transform.position=new Vector3(0,0,.1f);
            var lateMat=new Material(Shader.Find("Unlit/Color")); garbage.Add(lateMat); lateMat.color=new Color(.1f,.8f,.3f,1); lateMat.renderQueue=2480;
            late.GetComponent<Renderer>().sharedMaterial=lateMat;
            material.SetFloat("_HairFrontOpacity",1);
            equal("Nonzero hair writes depth",full,capture("09-depth-opaque"));
            material.SetFloat("_HairFrontOpacity",0);
            Color[] lateZero=capture("10-depth-zero");
            check("Zero local alpha does not write depth",Difference(lateZero,zero),Difference(lateZero,zero)>.01);
            helper.enabled=true; material.SetFloat("_HairFrontMode",2);
            material.SetFloat("_HairFeatherThreshold",.25f); material.SetFloat("_HairFeatherPower",4);
            Color[] featherZero=capture("10-feather-zero-depth");
            Vector3 eyePixel=camera.WorldToScreenPoint(eye.transform.position);
            int eyeIndex=(int)eyePixel.y*Size+(int)eyePixel.x;
            check("Custom curve fully faded interior leaves no depth",Delta(featherZero[eyeIndex],lateZero[eyeIndex]),Delta(featherZero[eyeIndex],lateZero[eyeIndex])<.003);
            helper.enabled=false; material.SetFloat("_HairFrontMode",1);
            material.SetFloat("_HairFeatherThreshold",.5f); material.SetFloat("_HairFeatherPower",1);
            late.SetActive(false);
            var clear=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true); clear.SetPixel(0,0,new Color(1,1,1,0)); clear.Apply(); garbage.Add(clear);
            material.SetTexture("_MainTex",clear); material.SetFloat("_Cutoff",0);
            equal("Zero texture alpha always clips",background,capture("11-zero-texture"));
            material.SetTexture("_MainTex",null); material.SetFloat("_Cutoff",.5f);

            var sun=new GameObject("Main").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=.6f;
            sun.transform.rotation=Quaternion.Euler(20,-25,0); sun.renderMode=LightRenderMode.ForcePixel; sun.cullingMask=1<<layer;
            var point=new GameObject("Point").AddComponent<Light>(); point.type=LightType.Point; point.range=5; point.intensity=1.2f;
            point.transform.position=new Vector3(.3f,.2f,-1); point.renderMode=LightRenderMode.ForcePixel; point.cullingMask=1<<layer;
            var spot=new GameObject("Spot").AddComponent<Light>(); spot.type=LightType.Spot; spot.range=5; spot.spotAngle=100; spot.intensity=1;
            spot.transform.position=new Vector3(-.4f,.3f,-1); spot.transform.LookAt(Vector3.zero); spot.renderMode=LightRenderMode.ForcePixel; spot.cullingMask=1<<layer;
            material.SetFloat("_DebugView",0); material.SetFloat("_EmissionIntensity",0);
            material.SetFloat("_HairFrontOpacity",1); Color[] lit=capture("12-multi-light-full");
            material.SetFloat("_HairFrontOpacity",0); Color[] litZero=capture("13-multi-light-zero");
            material.SetFloat("_HairFrontOpacity",.5f);
            Color[] litHalf=capture("14-multi-light-half");
            equal("ForwardAdd weighted once under stencil",Mix(litZero,lit,.5f),litHalf);
            helper.enabled=true; material.SetFloat("_HairFrontMode",2);
            material.SetFloat("_HairFeatherThreshold",.25f); material.SetFloat("_HairFeatherPower",2);
            // Measured unlit coverage isolates Add weighting from half-resolution distance error.
            equal("Custom curve weights Base and ForwardAdd together",TransferFeather(full,half,lightingCurve,lit,litHalf),capture("14-multi-light-feather"));
            helper.enabled=false; material.SetFloat("_HairFeatherThreshold",.5f); material.SetFloat("_HairFeatherPower",1);
            material.SetFloat("_HairFrontMode",0);
            equal("Multi-light hard one equals Off",lit,capture("15-multi-light-off"));
            sun.enabled=spot.enabled=false; point.transform.position=new Vector3(0,0,1);
            material.SetFloat("_DebugView",2);
            Color[] backlit=capture("16-backlight-specular");
            int leaks=0; for(int y=80;y<176;y++) for(int x=80;x<176;x++) if(backlit[y*Size+x].maxColorComponent>.005f) leaks++;
            check("Back light has no front specular",leaks,leaks==0);

            hair.SetActive(false); eye.SetActive(false);
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.layer=layer; sphere.GetComponent<Renderer>().sharedMaterial=material;
            point.transform.position=new Vector3(.25f,.2f,-1); material.SetFloat("_SpecularSize",.8f);
            material.SetFloat("_SpecularThreshold",.15f); material.SetFloat("_SpecularStrength",4);
            Color[] tangent=capture("17-strand-tangent");
            var flow=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true); garbage.Add(flow);
            flow.SetPixel(0,0,new Color(1,.5f,0,1)); flow.Apply(); material.SetTexture("_StrandDirectionMap",flow);
            material.SetFloat("_StrandDirectionBlend",1); Color[] mapped=capture("18-strand-map");
            check("Flow map changes anisotropic shape",Difference(tangent,mapped),Difference(tangent,mapped)>.0005);
            material.SetFloat("_StrandDirectionBlend",.5f); Color[] middle=capture("19-strand-half");
            check("Continuous flow midpoint differs from endpoints",Math.Min(Difference(middle,tangent),Difference(middle,mapped)),Difference(middle,tangent)>.0001&&Difference(middle,mapped)>.0001);
            flow.SetPixel(0,0,new Color(.5f,0,0,1)); flow.Apply();
            equal("Opposite flow axis cannot cancel at midpoint",tangent,capture("20-opposite-flow"));
            flow.SetPixel(0,0,new Color(.5f,.5f,0,1)); flow.Apply(); material.SetFloat("_StrandDirectionBlend",1);
            equal("Zero flow falls back to mesh strand",tangent,capture("21-zero-flow"));
            foreach(float blend in new[]{0f,.5f,1f}) foreach(float rough in new[]{.04f,.1f,.5f,1f})
            {
                material.SetFloat("_SpecularToonBlend",blend); material.SetFloat("_Roughness",rough);
                capture("22-lobe-"+blend.ToString("0.0")+"-rough-"+rough.ToString("0.00"));
            }
            check("Extreme roughness/lobe images are finite",12,true);
            material.SetFloat("_SpecularToonBlend",1); material.SetFloat("_DebugView",0);
            material.SetFloat("_OutlineOn",1); sun.enabled=true; sun.shadows=LightShadows.Soft;
            point.shadows=LightShadows.Soft; spot.enabled=true; spot.shadows=LightShadows.Soft;
            foreach(Vector3 scale in new[]{Vector3.one,new Vector3(-1,1,1),new Vector3(-1,-1,1)})
            {
                sphere.transform.localScale=scale;
                foreach(int cull in new[]{0,1,2}) { material.SetFloat("_CullOption",cull); capture("23-cull-"+cull+"-scale-"+scale.x+"-"+scale.y); }
            }
            sphere.SetActive(false); hair.SetActive(true); eye.SetActive(true);
            material.SetFloat("_CullOption",0); material.SetFloat("_OutlineOn",0); material.SetFloat("_HairFrontMode",1);
            // Compare camera-depth shadowcaster silhouettes; HairFront must not change either.
            camera.depthTextureMode=DepthTextureMode.Depth;
            material.SetFloat("_HairFrontOpacity",1); capture("24-shadow-front-one");
            // Actual light-space shadow is tested by a receiver below, not assumed from depth state.
            var floor=GameObject.CreatePrimitive(PrimitiveType.Quad); floor.layer=layer;
            floor.transform.position=new Vector3(0,0,1); floor.transform.localScale=new Vector3(3,3,1);
            var floorMaterial=new Material(Shader.Find("tom/MainOpaqueX")); garbage.Add(floorMaterial);
            floorMaterial.SetFloat("_SpecularStrength",0); floorMaterial.SetFloat("_UseRamp",0); floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly; eye.SetActive(false);
            material.SetFloat("_HairFrontOpacity",1); Color[] shadowOne=capture("25-shadow-only-one");
            material.SetFloat("_HairFrontOpacity",0); equal("HairFront opacity never cuts shadow",shadowOne,capture("26-shadow-only-zero"));
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            Color[] noShadow=capture("27-shadow-off");
            check("Shadow fixture has a real shadow",Difference(shadowOne,noShadow),Difference(shadowOne,noShadow)>.001);
            renderer.shadowCastingMode=ShadowCastingMode.On;
            Mesh cards=HairCards(); garbage.Add(cards); hair.GetComponent<MeshFilter>().sharedMesh=cards;
            var cardTexture=new Texture2D(128,128,TextureFormat.RGBA32,false,true); garbage.Add(cardTexture);
            for(int y=0;y<128;y++) for(int x=0;x<128;x++)
            {
                float u=(x+.5f)/128f,v=(y+.5f)/128f;
                float width=.48f*(.3f+.7f*Mathf.Sin(v*Mathf.PI*.5f));
                float edge=Mathf.Clamp01((width-Mathf.Abs(u-.5f))*70);
                float strand=.8f+.2f*Mathf.Sin(u*120+v*8);
                cardTexture.SetPixel(x,y,new Color(strand,strand,strand,edge));
            }
            cardTexture.Apply(); material.SetTexture("_MainTex",cardTexture);
            material.SetColor("_BaseColor",new Color(.25f,.12f,.07f,1)); material.SetColor("_AmbientColor",new Color(.25f,.25f,.25f,1));
            material.SetFloat("_HairFrontMode",0); material.SetFloat("_HairFrontOpacity",.25f);
            material.SetFloat("_SpecularSize",.65f); material.SetFloat("_SpecularThreshold",.3f);
            material.SetFloat("_SpecularStrength",3); material.SetFloat("_RimStrength",.35f); material.SetFloat("_OutlineOn",1);
            material.SetFloat("_StrandDirectionBlend",0);
            point.transform.position=new Vector3(.25f,.1f,-.35f); point.intensity=.6f;
            eye.SetActive(true); helper.enabled=true; helper.receivers=new[]{renderer};
            foreach(Vector3 scale in new[]{Vector3.one,new Vector3(-1,1,1),new Vector3(-1,-1,1)})
            {
                hair.transform.localScale=scale;
                foreach(int cull in new[]{0,1,2})
                { material.SetFloat("_CullOption",cull); capture("28-crossing-cards-"+cull+"-"+scale.x+"-"+scale.y); }
            }
            hair.transform.localScale=Vector3.one; material.SetFloat("_CullOption",0);
            foreach(int mode in new[]{0,1,2})
            { material.SetFloat("_HairFrontMode",mode); capture("29-hairfront-cards-"+mode); }
            check("Crossing cards / negative-scale / cull images finite",12,true);
            material.SetFloat("_HairFrontMode",0);
            var zeroTangents=new Vector4[cards.vertexCount]; cards.tangents=zeroTangents;
            capture("30-missing-tangents"); check("Missing tangent fallback finite",1,true);
            cards.RecalculateTangents();
            var benchRT=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            benchRT.Create();garbage.Add(benchRT);camera.targetTexture=benchRT;
            var benchReceivers=new List<Renderer>();var benchSources=new List<TomHairFeatherCamera.Source>();
            for(int n=0;n<6;n++)
            {
                Vector3 offset=new Vector3((n%3-1)*.85f,(n/3==0?-.33f:.33f),0);
                GameObject copy=Object.Instantiate(hair);copy.name="Benchmark hair "+n;copy.transform.position=offset;copy.transform.localScale=Vector3.one*.55f;
                GameObject eyeCopy=Object.Instantiate(eye);eyeCopy.name="Benchmark eye "+n;
                eyeCopy.transform.position=offset+new Vector3(.12f,.11f,.25f)*.55f;eyeCopy.transform.localScale=eye.transform.localScale*.55f;
                benchReceivers.Add(copy.GetComponent<Renderer>());
                benchSources.Add(new TomHairFeatherCamera.Source {renderer=eyeCopy.GetComponent<Renderer>()});
            }
            hair.SetActive(false);eye.SetActive(false);helper.receivers=benchReceivers.ToArray();helper.sources=benchSources.ToArray();
            var extraPoint=new GameObject("Benchmark extra point").AddComponent<Light>();extraPoint.type=LightType.Point;
            extraPoint.cullingMask=1<<layer;extraPoint.renderMode=LightRenderMode.ForcePixel;extraPoint.shadows=LightShadows.Soft;
            extraPoint.transform.position=new Vector3(-.8f,.5f,-.4f);extraPoint.range=4;extraPoint.intensity=.3f;
            var opaqueBench=new Material(Shader.Find("tom/MainOpaqueX"));garbage.Add(opaqueBench);opaqueBench.CopyPropertiesFromMaterial(material);
            foreach(int lightCount in new[]{1,4})
            {
                point.enabled=spot.enabled=extraPoint.enabled=lightCount==4;
                foreach(int mode in new[]{-1,0,1,2})
                {
                    foreach(Renderer r in benchReceivers) r.sharedMaterial=mode<0?opaqueBench:material;
                    material.SetFloat("_HairFrontMode",Mathf.Max(mode,0));
                    for(int n=0;n<4;n++)camera.Render();
                    var times=new List<double>();
                    for(int n=0;n<9;n++){var timer=Stopwatch.StartNew();camera.Render();timer.Stop();times.Add(timer.Elapsed.TotalMilliseconds);}
                    times.Sort();report.timings.Add(new Timing {mode="1080p/6 meshes/"+lightCount+" lights/"+(mode<0?"Opaque":mode.ToString()),
                        cameraRenderCpuMedianMs=times[4],sourceDraws=helper.SourceDraws,filterPasses=helper.FilterPasses});
                    check("1080p auxiliary scheduling: "+lightCount+" lights mode "+mode,helper.SourceDraws,
                        mode==2?helper.Active&&helper.SourceDraws==6&&helper.FilterPasses==7:!helper.Active&&helper.FilterPasses==0);
                }
            }
            RenderTexture.active=benchRT;
            var gallery=new Texture2D(1920,1080,TextureFormat.RGB24,false);garbage.Add(gallery);
            gallery.ReadPixels(new Rect(0,0,1920,1080),0,0);gallery.Apply(false);
            File.WriteAllBytes(Path.Combine(folder,"hair-gallery-1080p.png"),gallery.EncodeToPNG());report.images.Add("hair-gallery-1080p");
            compiled += "\n" + TomXStageTwoValidation.Validate("tom/HairX");
            File.WriteAllText(Path.Combine(folder,"compilation.txt"),compiled);
        }
        finally
        {
            RenderTexture.active=oldRT;
            EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach(var item in masks) if(item.Key) item.Key.cullingMask=item.Value;
            QualitySettings.pixelLightCount=oldLights; QualitySettings.shadows=oldShadows; QualitySettings.shadowDistance=oldDistance;
            foreach(Object o in garbage) if(o) Object.DestroyImmediate(o);
        }
        File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));
        int failures=report.checks.FindAll(c=>!c.passed).Count;
        if(failures>0) throw new Exception(failures+" HairX checks failed. "+folder);
        return report.checks.Count+" HairX checks passed; "+report.images.Count+" renders. "+folder;
    }
    static bool Finite(float f) { return !float.IsNaN(f)&&!float.IsInfinity(f); }
    static void DumpField(RenderTexture rt,string path,float scale)
    {
        RenderTexture old=RenderTexture.active; RenderTexture.active=rt;
        var read=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);
        read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);read.Apply(false);
        Color[] values=read.GetPixels();for(int n=0;n<values.Length;n++) values[n]=new Color(values[n].r/scale,values[n].r/scale,values[n].r/scale,1);
        var png=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);png.SetPixels(values);png.Apply(false);
        File.WriteAllBytes(path,png.EncodeToPNG()); Object.DestroyImmediate(read);Object.DestroyImmediate(png);RenderTexture.active=old;
    }
    static float Delta(Color a,Color b) { return Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))); }
    static double Difference(Color[] a,Color[] b) { double sum=0;for(int i=0;i<a.Length;i++)sum+=Delta(a[i],b[i]);return sum/a.Length; }
    static Color[] Mix(Color[] a,Color[] b,float t) { var c=new Color[a.Length];for(int i=0;i<c.Length;i++)c[i]=Color.Lerp(a[i],b[i],t);return c; }
    static Color[] TransferFeather(Color[] full,Color[] half,Color[] curved,Color[] litFull,Color[] litHalf)
    {
        var expected=new Color[full.Length];
        for(int n=0;n<full.Length;n++)
        {
            Color span=full[n]-half[n],delta=full[n]-curved[n];
            float denominator=span.r*span.r+span.g*span.g+span.b*span.b;
            float weight=denominator>1e-8f?(delta.r*span.r+delta.g*span.g+delta.b*span.b)/denominator:0;
            expected[n]=Color.Lerp(litFull[n],litHalf[n],weight);
        }
        return expected;
    }
    static float FeatherWeight(float u,float threshold,float power)
    {
        u=Mathf.Clamp01(u); threshold=Mathf.Clamp(threshold,.05f,.95f); power=Mathf.Clamp(power,.5f,4);
        float before=u*(1-threshold),after=(1-u)*threshold;
        float biased=before/(before+after);
        float rising=Mathf.Pow(biased,power),falling=Mathf.Pow(1-biased,power);
        return Mathf.SmoothStep(0,1,rising/(rising+falling));
    }
    static Color[] RectangleFeather(Color[] full,Color[] zero,Color[] half,float width,float threshold=.5f,float power=1)
    {
        int minX=Size,minY=Size,maxX=0,maxY=0;
        for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            if(Delta(full[y*Size+x],zero[y*Size+x])>.02f)
            {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
        var expected=(Color[])full.Clone();
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
        {
            float d=Math.Min(Math.Min(x-minX+.5f,maxX-x+.5f),Math.Min(y-minY+.5f,maxY-y+.5f));
            expected[y*Size+x]=Color.Lerp(full[y*Size+x],half[y*Size+x],FeatherWeight(d/width,threshold,power));
        }
        return expected;
    }
    internal static Mesh HairCards()
    {
        const int Rows=32,Columns=8,Strips=6;
        var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int k=0;k<Strips;k++)
        {
            int start=v.Count;
            for(int y=0;y<=Rows;y++) for(int x=0;x<=Columns;x++)
            {
                float u=(float)x/Columns,t=(float)y/Rows;
                float angle=(k%2==0?1:-1)*(t-.5f)*1.2f;
                float across=(u-.5f)*.34f;
                v.Add(new Vector3((k-2.5f)*.14f+Mathf.Sin(t*4+k)*.08f+across*Mathf.Cos(angle),
                    (t-.5f)*1.05f,Mathf.Cos(t*3+k)*.12f+across*Mathf.Sin(angle)));
                uv.Add(new Vector2(u,t));
            }
            for(int y=0;y<Rows;y++) for(int x=0;x<Columns;x++)
            {
                int a=start+y*(Columns+1)+x,b=a+1,c=a+Columns+1,d=c+1;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
        }
        var mesh=new Mesh {name="Curved crossing hair cards"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
}

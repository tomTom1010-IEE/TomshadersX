using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TomXHairStyleValidation
{
    const int Size = 384;
    const float Exposure = 4;
    [Serializable] class Shot
    {
        public string name, group, caption;
        public float peak, mean, varianceX, varianceY;
        public int nonFinite, brightPixels, softPixels;
        [NonSerialized] public Color[] pixels;
    }
    [Serializable] class Check { public string name; public double value; public bool passed; }
    [Serializable] class Report
    {
        public string unity, device, api, colorSpace;
        public string preview = "All PNGs use the same x4 exposure and RGB Reinhard display mapping. No per-image normalization. Metrics use unmodified ARGBFloat readback.";
        public string fixture = "Stencil Off; no environment, fog or post effects. Curved UV-aligned patch for isolated direct specular, then cutout crossing hair cards for final layers. No scene or material asset saved.";
        public List<Shot> shots = new List<Shot>();
        public List<Check> checks = new List<Check>();
    }

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before HairX style tests.");
        string compilation = TomXStageTwoValidation.Validate("tom/HairX");
        Scene previous = SceneManager.GetActiveScene();
        int layer = UnusedLayer();
        int oldLights = QualitySettings.pixelLightCount;
        RenderTexture oldRT = RenderTexture.active;
        var lightMasks = new Dictionary<Light,int>();
        foreach (Light light in Object.FindObjectsOfType<Light>())
        { lightMasks.Add(light,light.cullingMask); light.cullingMask &= ~(1 << layer); }
        var garbage = new List<Object>();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "CodexBridge/Reports/XHairStyle-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(output);
        var report = new Report {unity=Application.unityVersion,device=SystemInfo.graphicsDeviceName,
            api=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString()};
        Action<string,double,bool> check = (name,value,passed) => report.checks.Add(new Check {name=name,value=value,passed=passed});
        Action<string,Shot,Shot> different = (name,a,b) => {
            double delta=Difference(a.pixels,b.pixels); check(name,delta,delta>0.00005);
        };
        try
        {
            SceneManager.SetActiveScene(scene);
            QualitySettings.pixelLightCount=8;
            RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=Color.black; RenderSettings.fog=false;
            var camera=new GameObject("Hair style camera").AddComponent<Camera>();
            camera.enabled=false; camera.cullingMask=1 << layer;
            camera.transform.position=new Vector3(0,0,-3); camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true; camera.orthographicSize=.64f;
            camera.nearClipPlane=.05f; camera.farClipPlane=20;
            camera.renderingPath=RenderingPath.Forward; camera.allowHDR=true; camera.allowMSAA=false;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            var rt=new RenderTexture(Size,Size,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            rt.Create(); garbage.Add(rt); camera.targetTexture=rt;
            var read=new Texture2D(Size,Size,TextureFormat.RGBAFloat,false,true); garbage.Add(read);
            var png=new Texture2D(Size,Size,TextureFormat.RGB24,false); garbage.Add(png);
            var baseline=new Material(Shader.Find("tom/HairX")); garbage.Add(baseline);
            baseline.SetFloat("_HairFrontMode",0); baseline.SetFloat("_DebugView",2);
            baseline.SetFloat("_VertexLightIntensity",0); baseline.SetFloat("_LightProbeBlend",0);
            baseline.SetFloat("_CustomSHVolumeBlend",0); baseline.SetFloat("_IndirectDiffuseIntensity",0);
            baseline.SetFloat("_ReflectionMode",0); baseline.SetFloat("_RimStrength",0);
            baseline.SetFloat("_EmissionIntensity",0); baseline.SetFloat("_OutlineOn",0);
            baseline.SetFloat("_SpecularStrength",4); baseline.SetFloat("_SpecularToonBlend",1);
            baseline.SetFloat("_SpecularSize",.55f); baseline.SetFloat("_SpecularThreshold",.45f);
            baseline.SetFloat("_SpecularSoftness",.12f); baseline.SetFloat("_SpecularAA",0);
            baseline.SetFloat("_HairAnisotropy",.85f); baseline.SetFloat("_Roughness",.5f);
            baseline.SetFloat("_StrandAngle",90); baseline.SetFloat("_StrandDirectionBlend",0);
            var material=new Material(baseline); garbage.Add(material);
            var subject=new GameObject("Curved specular patch"); subject.layer=layer;
            var meshFilter=subject.AddComponent<MeshFilter>();
            Mesh patch=CurvedPatch(); garbage.Add(patch); meshFilter.sharedMesh=patch;
            var renderer=subject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            Light sun=MakeLight("Directional",LightType.Directional,layer);
            sun.intensity=1; sun.transform.rotation=Quaternion.identity;
            Light point=MakeLight("Additional point",LightType.Point,layer);
            point.transform.position=new Vector3(.55f,.2f,-.7f); point.range=3; point.intensity=.6f; point.enabled=false;
            Light spot=MakeLight("Additional spot",LightType.Spot,layer);
            spot.transform.position=new Vector3(-.55f,-.2f,-.7f); spot.transform.LookAt(Vector3.zero);
            spot.range=3; spot.spotAngle=110; spot.intensity=.6f; spot.enabled=false;
            Func<string,string,string,Action<Material>,Shot> capture = (name,group,caption,configure) => {
                material.CopyPropertiesFromMaterial(baseline); configure(material);
                camera.Render(); camera.Render(); RenderTexture.active=rt;
                read.ReadPixels(new Rect(0,0,Size,Size),0,0); read.Apply(false);
                Shot shot=Measure(name,group,caption,read.GetPixels()); report.shots.Add(shot);
                var display=new Color[shot.pixels.Length];
                for(int n=0;n<display.Length;n++)
                {
                    Color p=shot.pixels[n]*Exposure;
                    display[n]=new Color(p.r/(1+p.r),p.g/(1+p.g),p.b/(1+p.b),1);
                }
                png.SetPixels(display); png.Apply(false);
                File.WriteAllBytes(Path.Combine(output,name+".png"),png.EncodeToPNG());
                return shot;
            };

            Shot isotropic=capture("01-anisotropy-0","directions","Anisotropy = 0",m=>m.SetFloat("_HairAnisotropy",0));
            capture("02-anisotropy-05","directions","Anisotropy = 0.5",m=>m.SetFloat("_HairAnisotropy",.5f));
            Shot anisotropic=capture("03-anisotropy-09","directions","Anisotropy = 0.9",m=>m.SetFloat("_HairAnisotropy",.9f));
            double ratio=anisotropic.varianceX/Math.Max(anisotropic.varianceY,1e-8f);
            check("Anisotropy elongates highlight across vertical strands",ratio,ratio>2);
            check("Isotropic fixture has balanced axes",isotropic.varianceX/isotropic.varianceY,
                Math.Abs(isotropic.varianceX/isotropic.varianceY-1)<.1);
            Shot angle0=capture("04-angle-0","directions","Strand angle = 0 deg (U)",m=>m.SetFloat("_StrandAngle",0));
            capture("05-angle-45","directions","Strand angle = 45 deg",m=>m.SetFloat("_StrandAngle",45));
            Shot angle90=capture("06-angle-90","directions","Strand angle = 90 deg (V)",m=>m.SetFloat("_StrandAngle",90));
            different("Strand rotation changes highlight orientation",angle0,angle90);
            check("Angle zero rotates the elongated axis",angle0.varianceY/angle0.varianceX,angle0.varianceY>2*angle0.varianceX);
            var flow=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true); garbage.Add(flow);
            flow.SetPixel(0,0,new Color(1,.5f,0,1)); flow.Apply();
            var blends=new List<Shot>();
            foreach(float value in new[]{0f,.5f,1f})
                blends.Add(capture("07-flow-"+Key(value),"directions","Direction map blend = "+Key(value),m=>{
                    m.SetTexture("_StrandDirectionMap",flow);m.SetFloat("_StrandDirectionBlend",value);
                }));
            different("Flow midpoint differs from mesh endpoint",blends[0],blends[1]);
            different("Flow midpoint differs from map endpoint",blends[1],blends[2]);
            check("Map endpoint matches its U-axis reference",Difference(blends[2].pixels,angle0.pixels),Difference(blends[2].pixels,angle0.pixels)<.00001);

            var lobes=new List<Shot>();
            foreach(float value in new[]{0f,.5f,1f})
                lobes.Add(capture("08-toon-blend-"+Key(value),"models","Specular Toon blend = "+Key(value),m=>m.SetFloat("_SpecularToonBlend",value)));
            different("Continuous and Toon lobes are distinct",lobes[0],lobes[2]);
            double mixError=0;
            for(int n=0;n<lobes[0].pixels.Length;n++) mixError=Math.Max(mixError,Delta(lobes[1].pixels[n],Color.Lerp(lobes[0].pixels[n],lobes[2].pixels[n],.5f)));
            check("Half Toon blend matches RGB interpolation",mixError,mixError<.0001);
            var sizes=new List<Shot>();
            foreach(float value in new[]{.25f,.55f,.8f})
                sizes.Add(capture("09-size-"+Key(value),"shape","Highlight size = "+Key(value),m=>m.SetFloat("_SpecularSize",value)));
            check("Highlight size increases visible area",sizes[2].brightPixels,sizes[0].brightPixels<sizes[1].brightPixels&&sizes[1].brightPixels<sizes[2].brightPixels);
            var thresholds=new List<Shot>();
            foreach(float value in new[]{.2f,.5f,.8f})
                thresholds.Add(capture("10-threshold-"+Key(value),"shape","Highlight threshold = "+Key(value),m=>m.SetFloat("_SpecularThreshold",value)));
            check("Highlight threshold decreases visible area",thresholds[2].brightPixels,thresholds[0].brightPixels>thresholds[1].brightPixels&&thresholds[1].brightPixels>thresholds[2].brightPixels);
            var softness=new List<Shot>();
            foreach(float value in new[]{.02f,.2f,.6f})
                softness.Add(capture("11-softness-"+Key(value),"shape","Highlight softness = "+Key(value),m=>m.SetFloat("_SpecularSoftness",value)));
            check("Softness increases intermediate edge pixels",softness[2].softPixels,softness[0].softPixels<softness[1].softPixels&&softness[1].softPixels<softness[2].softPixels);
            var bands=new List<Shot>();
            foreach(int value in new[]{1,2,4})
                bands.Add(capture("12-bands-"+value,"models","Highlight bands = "+value,m=>{
                    m.SetFloat("_SpecularBands",value);m.SetFloat("_SpecularSoftness",.6f);
                }));
            different("Two bands differ from smooth lobe",bands[0],bands[1]);
            different("Four bands differ from two bands",bands[1],bands[2]);
            var roughness=new List<Shot>();
            foreach(float value in new[]{.04f,.5f,1f})
                roughness.Add(capture("13-toon-roughness-"+Key(value),"diagnostics","Toon endpoint: roughness = "+Key(value),m=>m.SetFloat("_Roughness",value)));
            check("Toon endpoint size is independent of physical roughness",Difference(roughness[0].pixels,roughness[2].pixels),Difference(roughness[0].pixels,roughness[2].pixels)<.00001);

            var normal=new Texture2D(256,256,TextureFormat.RGBA32,true,true); garbage.Add(normal);
            var packed=new Color[256*256];
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
                packed[y*256+x]=new Color(1,.5f+.12f*Mathf.Sin(y*.63f),1,.5f+.12f*Mathf.Sin(x*.81f));
            normal.SetPixels(packed); normal.Apply(true);
            Shot aaOff=null,aaOn=null;
            foreach(float move in new[]{0f,.5f}) foreach(int aa in new[]{0,1})
            {
                camera.transform.position=new Vector3(move*(camera.orthographicSize*2/Size),0,-3);
                Shot shot=capture("14-aa-"+aa+"-move-"+Key(move),"antialiasing","AA = "+aa+" / camera shift "+Key(move)+" px",m=>{
                    m.SetTexture("_NormalMap",normal);m.SetTextureScale("_NormalMap",new Vector2(6,6));m.SetFloat("_SpecularAA",aa);
                });
                if(move==0) {if(aa==0) aaOff=shot;else aaOn=shot;}
            }
            different("Specular AA changes the high-frequency-normal response",aaOff,aaOn);
            camera.transform.position=new Vector3(0,0,-3);
            capture("15-sun","lights","Directional light",m=>{});
            point.enabled=true;
            Shot pointShot=capture("16-sun-point","lights","Directional + point",m=>{});
            point.enabled=false;spot.enabled=true;
            Shot spotShot=capture("17-sun-spot","lights","Directional + spot",m=>{});
            different("Point light adds the stylized lobe",angle90,pointShot);
            different("Spot light adds the stylized lobe",angle90,spotShot);
            spot.enabled=false;

            Mesh cards=TomXHairValidation.HairCards(); garbage.Add(cards); meshFilter.sharedMesh=cards;
            var hairTexture=new Texture2D(128,128,TextureFormat.RGBA32,true,true); garbage.Add(hairTexture);
            for(int y=0;y<128;y++) for(int x=0;x<128;x++)
            {
                float u=(x+.5f)/128,t=(y+.5f)/128;
                float edge=Mathf.Clamp01((.48f*(.3f+.7f*Mathf.Sin(t*Mathf.PI*.5f))-Mathf.Abs(u-.5f))*70);
                float strand=.78f+.22f*Mathf.Sin(u*120+t*8);
                hairTexture.SetPixel(x,y,new Color(strand,strand,strand,edge));
            }
            hairTexture.Apply(true);
            var matcap=new Texture2D(64,64,TextureFormat.RGBA32,false,true); garbage.Add(matcap);
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                float dx=(x+.5f)/64-.35f,dy=(y+.5f)/64-.65f;
                float v=Mathf.Exp(-(dx*dx+dy*dy)*14)*.2f;
                matcap.SetPixel(x,y,new Color(v*.5f,v*.7f,v,1));
            }
            matcap.Apply();
            baseline.SetFloat("_DebugView",0);baseline.SetTexture("_MainTex",hairTexture);
            baseline.SetColor("_BaseColor",new Color(.2f,.065f,.03f,1));
            baseline.SetFloat("_SpecularSize",.65f);baseline.SetFloat("_SpecularSoftness",.2f);
            baseline.SetFloat("_SpecularStrength",2);baseline.SetFloat("_SpecularAA",.5f);
            sun.transform.rotation=Quaternion.Euler(20,-20,0);sun.intensity=.75f;
            var layerShots=new List<Shot>();
            layerShots.Add(capture("18-body","layers","Toon body / specular off",m=>m.SetFloat("_SpecularStrength",0)));
            layerShots.Add(capture("19-specular","layers","+ Anisotropic Toon specular",m=>{}));
            baseline.SetFloat("_RimStrength",.2f);baseline.SetFloat("_RimWidth",.55f);
            Shot frontalRim=capture("20a-frontal-rim","diagnostics","Facing cards: Rim width = 0.55",m=>{});
            check("Near-facing cards suppress narrow rim",Difference(layerShots[1].pixels,frontalRim.pixels),Difference(layerShots[1].pixels,frontalRim.pixels)<.00001);
            baseline.SetFloat("_RimWidth",.9f);
            layerShots.Add(capture("20-rim","layers","+ Wide Rim (width = 0.9)",m=>{}));
            baseline.SetFloat("_ReflectionMode",1);baseline.SetFloat("_MatCapIntensity",.4f);baseline.SetTexture("_MatCap",matcap);
            layerShots.Add(capture("21-matcap","layers","+ MatCap Add",m=>{}));
            baseline.SetFloat("_OutlineOn",1);baseline.SetFloat("_OutlineWidth",2);
            baseline.SetColor("_OutlineColor",new Color(.025f,.015f,.03f,1));
            Shot openOutline=capture("22-outline","layers","+ Outline (front-facing cards)",m=>{});
            check("Inverted hull cannot outline front-only open cards",Difference(layerShots[3].pixels,openOutline.pixels),Difference(layerShots[3].pixels,openOutline.pixels)<.00001);
            point.enabled=spot.enabled=true;
            layerShots.Add(capture("23-multiple-lights","layers","All layers + point + spot",m=>{}));
            for(int n=1;n<layerShots.Count;n++) different("Layer contributes: "+layerShots[n].caption,layerShots[n-1],layerShots[n]);

            // A closed surface supplies the back-facing hull that single front cards lack.
            subject.SetActive(false);point.enabled=spot.enabled=false;
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.layer=layer;
            sphere.GetComponent<Renderer>().sharedMaterial=material;
            baseline.SetTexture("_MainTex",null);baseline.SetFloat("_ReflectionMode",0);
            baseline.SetFloat("_RimStrength",0);baseline.SetFloat("_OutlineOn",0);
            baseline.SetColor("_OutlineColor",new Color(.04f,.15f,.3f,1));
            Shot closed=capture("24-closed-base","geometry","Closed mesh: base",m=>{});
            Shot closedRim=capture("25-closed-rim","geometry","Closed mesh: Rim width = 0.35",m=>{
                m.SetFloat("_RimStrength",.2f);m.SetFloat("_RimWidth",.35f);
            });
            Shot closedOutline=capture("26-closed-outline","geometry","Closed mesh: Outline = 2 px",m=>m.SetFloat("_OutlineOn",1));
            capture("27-closed-both","geometry","Closed mesh: Rim + Outline",m=>{
                m.SetFloat("_RimStrength",.2f);m.SetFloat("_RimWidth",.35f);m.SetFloat("_OutlineOn",1);
            });
            different("Rim appears at grazing angles on closed geometry",closed,closedRim);
            different("Outline appears with a closed back-facing hull",closed,closedOutline);

            foreach(Shot shot in report.shots)
            {
                check("Finite pixels: "+shot.name,shot.nonFinite,shot.nonFinite==0);
                check("Nonblank image: "+shot.name,shot.brightPixels,shot.brightPixels>20);
            }
            compilation+="\n"+TomXStageTwoValidation.Validate("tom/HairX");
            File.WriteAllText(Path.Combine(output,"compilation.txt"),compilation);
        }
        finally
        {
            RenderTexture.active=oldRT;
            EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);
            foreach(var pair in lightMasks)if(pair.Key)pair.Key.cullingMask=pair.Value;
            QualitySettings.pixelLightCount=oldLights;
            foreach(Object item in garbage)if(item)Object.DestroyImmediate(item);
        }
        File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
        int failed=report.checks.FindAll(c=>!c.passed).Count;
        if(failed>0)throw new Exception(failed+" HairX style checks failed. "+output);
        return report.checks.Count+" HairX style checks passed; "+report.shots.Count+" captures. "+output;
    }

    static string Key(float value) { return value.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture); }
    static float Luminance(Color p) { return p.r*.2126f+p.g*.7152f+p.b*.0722f; }
    static bool Finite(float f) { return !float.IsNaN(f)&&!float.IsInfinity(f); }
    static float Delta(Color a,Color b) { return Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))); }
    static double Difference(Color[] a,Color[] b) { double d=0;for(int n=0;n<a.Length;n++)d+=Delta(a[n],b[n]);return d/a.Length; }
    static Shot Measure(string name,string group,string caption,Color[] pixels)
    {
        var shot=new Shot {name=name,group=group,caption=caption,pixels=pixels};
        double total=0,mx=0,my=0;
        for(int n=0;n<pixels.Length;n++)
        {
            Color p=pixels[n];
            if(!Finite(p.r)||!Finite(p.g)||!Finite(p.b)) {shot.nonFinite++;continue;}
            float v=Luminance(p);shot.peak=Mathf.Max(shot.peak,v);total+=v;mx+=(n%Size)*v;my+=(n/Size)*v;
        }
        shot.mean=(float)(total/pixels.Length);mx/=Math.Max(total,1e-12);my/=Math.Max(total,1e-12);
        double vx=0,vy=0;
        for(int n=0;n<pixels.Length;n++)
        {
            float v=Luminance(pixels[n]);if(!Finite(v))continue;
            if(v>Mathf.Max(.00001f,shot.peak*.2f))shot.brightPixels++;
            if(v>shot.peak*.1f&&v<shot.peak*.9f)shot.softPixels++;
            vx+=(n%Size-mx)*(n%Size-mx)*v;vy+=(n/Size-my)*(n/Size-my)*v;
        }
        shot.varianceX=(float)(vx/Math.Max(total,1e-12));shot.varianceY=(float)(vy/Math.Max(total,1e-12));
        return shot;
    }
    static int UnusedLayer()
    {
        var used=new HashSet<int>();foreach(GameObject g in Object.FindObjectsOfType<GameObject>())used.Add(g.layer);
        for(int n=31;n>=8;n--)if(!used.Contains(n))return n;
        throw new Exception("No unused layer for isolated rendering.");
    }
    static Light MakeLight(string name,LightType type,int layer)
    {
        Light light=new GameObject(name).AddComponent<Light>();light.type=type;
        light.cullingMask=1<<layer;light.renderMode=LightRenderMode.ForcePixel;light.shadows=LightShadows.None;
        return light;
    }
    static Mesh CurvedPatch()
    {
        const int Count=96;var vertices=new List<Vector3>();var normals=new List<Vector3>();
        var uv=new List<Vector2>();var triangles=new List<int>();
        for(int y=0;y<=Count;y++)for(int x=0;x<=Count;x++)
        {
            float u=(float)x/Count,v=(float)y/Count,px=u-.5f,py=v-.5f;
            vertices.Add(new Vector3(px,py,.8f*(px*px+py*py)));
            normals.Add(new Vector3(1.6f*px,1.6f*py,-1).normalized);uv.Add(new Vector2(u,v));
        }
        for(int y=0;y<Count;y++)for(int x=0;x<Count;x++)
        {int a=y*(Count+1)+x,b=a+1,c=a+Count+1;triangles.AddRange(new[]{a,c,b,b,c,c+1});}
        var mesh=new Mesh {name="UV-aligned curved highlight patch"};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
}

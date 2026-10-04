using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TomXCrossMeshAlphaPreview
{
    const string Root="Assets/Mods/TomShadersX/Tests/ReferenceAssets";
    const int Width=1800,Height=1000;
    static readonly string[] Names={"MainAlphaX","MainAlphaX2Pass","MainAlphaXBackFront"};
    [Serializable] class Shot { public string file,mode;public int rearQueue;public float yaw; }
    [Serializable] class Check { public string name;public double maxError;public bool passed; }
    [Serializable] class Report
    {
        public string unity,api,colorSpace,scene,sourceScene,queueProfile;
        public bool mergedPrepass;
        public int foregroundQueue=3000,rearQueue=2900,backgroundQueue=2000;
        public float foregroundAlpha=.45f,rearAlpha=.5f,rearZ=2.2f,backgroundZ=4;
        public string geometry="Three columns share the twisted open-cube mesh. Each column has a separate ordinary-alpha Quad fully behind the twisted mesh and a farther opaque grid Quad.";
        public string settings="Front: Alpha/Cull Off/ZWrite Off; Depth2Pass/Cull Off/prepass On; BackFront/Cull Back/both color ZWrite On. Rear: MainAlphaX, Cull Off, ZWrite Off. Continuous alpha, no shadows, Outline, Rim, probes or MatCap. Background: opaque Unlit/Texture, ZWrite On.";
        public string validation="Cross-renderer over-compositing checked as frontBlack + (frontWhite-frontBlack)*rearOverBackground, per RGB pixel in the object regions. Checks do not establish correct triangle sorting inside the front object, camera depth-texture contents or post-processing stability. The late-rear image is an intentionally incorrect-order control.";
        public List<Shot> shots=new List<Shot>();public List<Check> checks=new List<Check>();
    }

    public static string Run(string sourceScene,bool cutoutQueues=false,bool mergedPrepass=false)
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before rendering.");
        if(string.IsNullOrEmpty(sourceScene)||!sourceScene.StartsWith(Root+"/TwistedAlpha-",StringComparison.Ordinal)
            ||!sourceScene.EndsWith("/TwistedAlphaComparison.unity",StringComparison.Ordinal)||sourceScene.Contains(".."))
            throw new Exception("Provide a generated twisted-alpha comparison scene.");
        if(SceneManager.GetSceneByPath(sourceScene).isLoaded)throw new Exception("Close the source comparison scene before generating the new test.");
        Scene previous=SceneManager.GetActiveScene();RenderTexture oldRT=RenderTexture.active;
        int oldPixelLights=QualitySettings.pixelLightCount;
        var oldMasks=new Dictionary<Light,int>();foreach(Light l in Object.FindObjectsOfType<Light>())oldMasks.Add(l,l.cullingMask);
        Scene scene=EditorSceneManager.OpenScene(sourceScene,OpenSceneMode.Additive);
        var temporary=new List<Object>();
        int foregroundQueue=cutoutQueues?2452:3000,rearQueue=cutoutQueues?2451:2900,backgroundQueue=cutoutQueues?2450:2000;
        int lateRearQueue=cutoutQueues?2453:3100;
        string id=(mergedPrepass?"CrossMeshAlphaMerged-":cutoutQueues?"CrossMeshAlpha2450-":"CrossMeshAlpha-")+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string assets=Root+"/"+id;
        string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"CodexBridge/Reports/"+id);
        Directory.CreateDirectory(output);
        var report=new Report {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),
            colorSpace=QualitySettings.activeColorSpace.ToString(),sourceScene=sourceScene,
            mergedPrepass=mergedPrepass,
            queueProfile=cutoutQueues?"2450/2451/2452":"2000/2900/3000",
            foregroundQueue=foregroundQueue,rearQueue=rearQueue,backgroundQueue=backgroundQueue};
        try
        {
            SceneManager.SetActiveScene(scene);QualitySettings.pixelLightCount=8;
            var front=new Renderer[3];var background=new Renderer[3];
            Camera camera=null;TextMesh title=null,subtitle=null,bottom=null;
            foreach(GameObject obj in scene.GetRootGameObjects())
            {
                var cam=obj.GetComponent<Camera>();if(cam!=null)camera=cam;
                for(int n=0;n<3;n++) {if(obj.name==Names[n])front[n]=obj.GetComponent<Renderer>();if(obj.name=="Grid "+n)background[n]=obj.GetComponent<Renderer>();}
                var text=obj.GetComponent<TextMesh>();if(text==null)continue;
                if(mergedPrepass&&text.text==Names[1])text.text="MainAlphaX + Depth Prepass";
                if(text.transform.position.y>2.5f)title=text;
                else if(text.transform.position.y>2.2f)subtitle=text;
                else if(text.transform.position.y< -2.5f)bottom=text;
            }
            if(camera==null||title==null||subtitle==null||bottom==null)throw new Exception("Incomplete template scene.");
            for(int n=0;n<3;n++)if(front[n]==null||background[n]==null)throw new Exception("Missing comparison column "+n);
            foreach(var pair in oldMasks)if(pair.Key!=null)pair.Key.cullingMask &= ~camera.cullingMask;
            AssetDatabase.CreateFolder(Root,id);AssetDatabase.CreateFolder(assets,"Materials");
            string sourceAssets=sourceScene.Substring(0,sourceScene.LastIndexOf('/'));
            var frontLit=new Material[3];var frontFlat=new Material[3];var rear=new Renderer[3];
            var grid=new Material(background[0].sharedMaterial);grid.renderQueue=backgroundQueue;
            AssetDatabase.CreateAsset(grid,assets+"/Materials/OpaqueBackground.mat");
            Texture2D pattern=MakeRearPattern();AssetDatabase.CreateAsset(pattern,assets+"/RearPattern.asset");
            var rearLit=new Material(Shader.Find("tom/MainAlphaX"));
            rearLit.CopyPropertiesFromMaterial(front[0].sharedMaterial);
            rearLit.SetFloat("_Alpha",.5f);rearLit.SetFloat("_AlphaOptionZWrite",0);rearLit.SetFloat("_CullOption",0);
            rearLit.SetFloat("_AlphaOptionCutoff",0);rearLit.SetFloat("_DebugView",0);
            rearLit.SetColor("_BaseColor",Color.white);rearLit.SetTexture("_MainTex",pattern);rearLit.renderQueue=rearQueue;
            AssetDatabase.CreateAsset(rearLit,assets+"/Materials/RearAlpha_Lit.mat");
            var rearFlat=new Material(rearLit);rearFlat.SetFloat("_DebugView",6);
            rearFlat.SetTexture("_EmissionMask",pattern);rearFlat.SetColor("_EmissionColor",Color.white);rearFlat.SetFloat("_EmissionIntensity",1);
            AssetDatabase.CreateAsset(rearFlat,assets+"/Materials/RearAlpha_Coverage.mat");
            for(int n=0;n<3;n++)
            {
                frontLit[n]=new Material(AssetDatabase.LoadAssetAtPath<Material>(sourceAssets+"/Materials/"+Names[n]+"_Lit.mat"));
                frontFlat[n]=new Material(AssetDatabase.LoadAssetAtPath<Material>(sourceAssets+"/Materials/"+Names[n]+"_Coverage.mat"));
                if(mergedPrepass&&n==1)
                    foreach(Material m in new[] {frontLit[n],frontFlat[n]})
                    {
                        m.shader=Shader.Find("tom/MainAlphaX");
                        m.SetFloat("_DepthPrepass",1);m.SetFloat("_AlphaOptionZWrite",0);
                    }
                frontLit[n].renderQueue=foregroundQueue;frontFlat[n].renderQueue=foregroundQueue;
                string materialName=mergedPrepass&&n==1?"MainAlphaXPrepass":Names[n];
                AssetDatabase.CreateAsset(frontLit[n],assets+"/Materials/"+materialName+"_Lit.mat");
                AssetDatabase.CreateAsset(frontFlat[n],assets+"/Materials/"+materialName+"_Coverage.mat");
                background[n].sharedMaterial=grid;
                Vector3 p=background[n].transform.position;p.z=4;background[n].transform.position=p;
                var card=GameObject.CreatePrimitive(PrimitiveType.Quad);Object.DestroyImmediate(card.GetComponent<Collider>());
                card.name="Rear ordinary alpha "+n;card.layer=front[n].gameObject.layer;
                card.transform.position=new Vector3(front[n].transform.position.x+.08f,-.13f,2.2f);
                card.transform.localScale=new Vector3(2.32f,2.92f,1);card.transform.rotation=Quaternion.Euler(0,0,-11);
                rear[n]=card.GetComponent<Renderer>();rear[n].sharedMaterial=rearLit;
                rear[n].shadowCastingMode=ShadowCastingMode.Off;rear[n].receiveShadows=false;
                rear[n].lightProbeUsage=LightProbeUsage.Off;rear[n].reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            bottom.text="Front alpha 0.45 | Rear striped alpha 0.50, ZWrite Off | Opaque grid, ZWrite On | No intersecting meshes";
            var solid=new Material(Shader.Find("Unlit/Color"));solid.renderQueue=backgroundQueue;temporary.Add(solid);
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            rt.Create();temporary.Add(rt);camera.targetTexture=rt;camera.enabled=false;
            var read=new Texture2D(Width,Height,TextureFormat.RGBAFloat,false,true);temporary.Add(read);
            var png=new Texture2D(Width,Height,TextureFormat.RGB24,false);temporary.Add(png);
            Action<bool,float,int> setup=(flat,yaw,queue)=>{
                title.text="CROSS-MESH / "+(flat?"COVERAGE ONLY":"TOON + DIRECTIONAL + POINT");
                subtitle.text="Front Q"+foregroundQueue+" | Rear Q"+queue+" | Opaque background Q"+backgroundQueue+(queue>foregroundQueue?" | LATE-REAR CONTROL":"");
                for(int n=0;n<3;n++)
                {
                    front[n].enabled=true;front[n].sharedMaterial=flat?frontFlat[n]:frontLit[n];
                    front[n].transform.rotation=Quaternion.Euler(16,yaw,0);
                    rear[n].enabled=true;rear[n].sharedMaterial=flat?rearFlat:rearLit;
                    background[n].sharedMaterial=grid;
                }
                rearLit.renderQueue=queue;rearFlat.renderQueue=queue;
            };
            Func<string,string,float,Color[]> capture=(file,mode,yaw)=>{
                camera.Render();camera.Render();RenderTexture.active=rt;
                read.ReadPixels(new Rect(0,0,Width,Height),0,0);read.Apply(false);
                Color[] pixels=read.GetPixels(),display=new Color[pixels.Length];
                bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
                for(int p=0;p<pixels.Length;p++)
                {
                    Color c=pixels[p];if(!Finite(c.r)||!Finite(c.g)||!Finite(c.b))throw new Exception("Nonfinite pixel in "+file);
                    if(linear)c=c.gamma;display[p]=new Color(Mathf.Clamp01(c.r),Mathf.Clamp01(c.g),Mathf.Clamp01(c.b),1);
                }
                png.SetPixels(display);png.Apply(false);File.WriteAllBytes(Path.Combine(output,file+".png"),png.EncodeToPNG());
                report.shots.Add(new Shot {file=file+".png",mode=mode,rearQueue=rearLit.renderQueue,yaw=yaw});return pixels;
            };
            // Determine actual foreground transmission with black/white controls, then check rear compositing.
            for(int mode=0;mode<2;mode++)
            {
                bool flat=mode==0;string prefix=flat?"coverage":"toon";setup(flat,-20,rearQueue);
                Color[] full=capture(flat?"01-full-coverage":"02-full-toon",prefix,-20);
                foreach(Renderer r in front)r.enabled=false;
                Color[] rearOnly=capture(prefix+"-rear-and-background",prefix,-20);
                foreach(Renderer r in rear)r.enabled=false;
                capture(prefix+"-background-only",prefix,-20);
                foreach(Renderer r in front)r.enabled=true;
                foreach(Renderer r in background)r.sharedMaterial=solid;
                solid.color=Color.black;Color[] black=capture(prefix+"-front-over-black",prefix,-20);
                solid.color=Color.white;Color[] white=capture(prefix+"-front-over-white",prefix,-20);
                for(int column=0;column<3;column++)
                {
                    double error=0;
                    for(int y=200;y<780;y++)for(int x=column*600+25;x<column*600+575;x++)
                    {
                        int p=y*Width+x;
                        for(int channel=0;channel<3;channel++)
                        {
                            float expected=black[p][channel]+(white[p][channel]-black[p][channel])*rearOnly[p][channel];
                            error=Math.Max(error,Math.Abs(expected-full[p][channel]));
                        }
                    }
                    report.checks.Add(new Check {name=prefix+" rear over background through "+(mergedPrepass&&column==1?"MainAlphaX + DepthPrepass":Names[column]),maxError=error,passed=error<.003});
                }
                setup(flat,-20,rearQueue);
                foreach(Renderer r in front)r.sharedMaterial.SetFloat("_Alpha",0);
                Color[] zero=capture(prefix+"-zero-foreground",prefix,-20);
                report.checks.Add(new Check {name=prefix+" zero foreground leaves rear intact",maxError=Difference(zero,rearOnly),passed=Difference(zero,rearOnly)<.003});
                foreach(Renderer r in front) {r.sharedMaterial.SetFloat("_Alpha",.45f);r.sharedMaterial.SetFloat("_AlphaOptionCutoff",1);r.sharedMaterial.SetFloat("_Cutoff",.5f);}
                Color[] clipped=capture(prefix+"-clipped-foreground",prefix,-20);
                report.checks.Add(new Check {name=prefix+" clipped foreground leaves rear intact",maxError=Difference(clipped,rearOnly),passed=Difference(clipped,rearOnly)<.003});
                foreach(Renderer r in front)r.sharedMaterial.SetFloat("_AlphaOptionCutoff",0);
            }
            setup(true,80,rearQueue);capture("03-side-coverage","coverage",80);
            setup(false,80,rearQueue);capture("04-side-toon","toon",80);
            setup(true,-20,lateRearQueue);capture("05-control-late-rear","coverage-control",-20);
            setup(false,-20,rearQueue);
            camera.targetTexture=null;camera.enabled=true;
            report.scene=assets+"/CrossMeshAlphaComparison.unity";
            // Only new test materials are persisted. The source scene and materials remain unchanged.
            foreach(Material m in frontLit)EditorUtility.SetDirty(m);
            foreach(Material m in frontFlat)EditorUtility.SetDirty(m);
            EditorUtility.SetDirty(rearLit);EditorUtility.SetDirty(rearFlat);
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,report.scene))throw new Exception("Could not save comparison scene.");
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            int failed=report.checks.FindAll(c=>!c.passed).Count;
            if(failed!=0)throw new Exception(output+"; failed cross-mesh checks="+failed);
            return "Rendered "+report.shots.Count+" images: "+output+"; "+report.checks.Count+" checks, "+failed+" failed. Scene: "+report.scene;
        }
        finally
        {
            RenderTexture.active=oldRT;QualitySettings.pixelLightCount=oldPixelLights;
            foreach(var pair in oldMasks)if(pair.Key!=null)pair.Key.cullingMask=pair.Value;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
            foreach(Object item in temporary)if(item!=null)Object.DestroyImmediate(item);
        }
    }
    static bool Finite(float value) {return !float.IsNaN(value)&&!float.IsInfinity(value);}
    static double Difference(Color[] a,Color[] b)
    {
        double error=0;
        for(int y=200;y<780;y++)for(int x=25;x<1775;x++)
        {
            int p=y*Width+x;for(int c=0;c<3;c++)error=Math.Max(error,Math.Abs(a[p][c]-b[p][c]));
        }
        return error;
    }
    static Texture2D MakeRearPattern()
    {
        const int size=128;var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true);t.name="Rear translucent stripe marker";
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            pixels[y*size+x]=((x+y)/16)%2==0?new Color(.88f,.36f,.10f,1):new Color(.45f,.10f,.04f,1);
        t.SetPixels(pixels);t.Apply();t.wrapMode=TextureWrapMode.Clamp;return t;
    }
}

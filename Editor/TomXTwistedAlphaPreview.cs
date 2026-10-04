using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Persistent test assets are created by Unity, not serialized outside the Editor.
public static class TomXTwistedAlphaPreview
{
    const string Root = "Assets/Mods/TomShadersX/Tests/ReferenceAssets";
    const int Width = 1800, Height = 1000;
    static readonly string[] Names = {"MainAlphaX", "MainAlphaX2Pass", "MainAlphaXBackFront"};
    [Serializable] class Shot { public string file, mode; public float yaw, pitch; public int nonFinite; }
    [Serializable] class Report
    {
        public string unity, api, colorSpace, scene, mesh;
        public int vertices, triangles;
        public float alpha = .45f, totalTwistDegrees = 130;
        public string geometry = "Subdivided cube with local -Z face removed, twisted about Y and tapered at the waist. Single mesh/submesh/renderer per variant; no duplicate back-facing polygons.";
        public string settings = "Same material core, alpha .45, global cutoff off, depth/shadow threshold .01, no outline/rim/probes/MatCap or cast shadows. Alpha: Cull Off ZWrite off. Depth: Cull Off prepass on. BackFront: Cull Back, both color depth writes on. Queue 3000, straight output.";
        public string limitation = "Visual comparison, not a correctness oracle. Back/front grouping cannot sort arbitrary folds. Flat views isolate coverage; lit views add directional and per-object point light. Reverse-order view changes only triangle submission order.";
        public List<Shot> shots = new List<Shot>();
    }

    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before rendering.");
        int[] layers = FindLayers();
        int mask = (1 << layers[0]) | (1 << layers[1]) | (1 << layers[2]);
        var oldMasks = new Dictionary<Light,int>();
        foreach(Light light in Object.FindObjectsOfType<Light>()) { oldMasks.Add(light,light.cullingMask);light.cullingMask &= ~mask; }
        Scene previous = SceneManager.GetActiveScene();
        RenderTexture previousRT = RenderTexture.active;
        int oldPixelLights = QualitySettings.pixelLightCount;
        var temporary = new List<Object>();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        string id = "TwistedAlpha-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string assets = Root + "/" + id;
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName,"CodexBridge/Reports/"+id);
        Directory.CreateDirectory(output);
        var report = new Report {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString()};
        try
        {
            SceneManager.SetActiveScene(scene);
            QualitySettings.pixelLightCount = 8;
            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black;
            AssetDatabase.CreateFolder(Root,id);
            AssetDatabase.CreateFolder(assets,"Materials");
            Mesh mesh = OpenTwistedCube();
            report.vertices=mesh.vertexCount;report.triangles=mesh.triangles.Length/3;
            report.mesh=assets+"/OpenTwistedCube.asset";
            AssetDatabase.CreateAsset(mesh,report.mesh);
            Mesh reversed = Object.Instantiate(mesh);temporary.Add(reversed);
            int[] source=mesh.triangles, flipped=new int[source.Length];
            for(int i=0;i<source.Length;i+=3)for(int j=0;j<3;j++)flipped[i+j]=source[source.Length-3-i+j];
            reversed.triangles=flipped;

            var camera = new GameObject("Comparison Camera").AddComponent<Camera>();
            camera.enabled=false;camera.transform.position=new Vector3(0,0,-8);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.975f,.98f,.985f,1);
            camera.cullingMask=mask;camera.renderingPath=RenderingPath.Forward;
            camera.orthographic=true;camera.orthographicSize=3;
            camera.nearClipPlane=.1f;camera.farClipPlane=30;camera.allowHDR=true;camera.allowMSAA=false;
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            rt.Create();temporary.Add(rt);camera.targetTexture=rt;
            var read=new Texture2D(Width,Height,TextureFormat.RGBAFloat,false,true);temporary.Add(read);
            var png=new Texture2D(Width,Height,TextureFormat.RGB24,false);temporary.Add(png);

            var sun=new GameObject("Shared directional light").AddComponent<Light>();
            sun.type=LightType.Directional;sun.renderMode=LightRenderMode.ForcePixel;
            sun.intensity=.8f;sun.transform.rotation=Quaternion.Euler(30,-25,0);sun.cullingMask=mask;
            sun.shadows=LightShadows.None;
            Texture2D grid=MakeGrid();AssetDatabase.CreateAsset(grid,assets+"/BackgroundGrid.asset");
            var gridMat=new Material(Shader.Find("Unlit/Texture"));gridMat.mainTexture=grid;
            AssetDatabase.CreateAsset(gridMat,assets+"/Materials/Background.mat");
            var geometryMat=new Material(Shader.Find("tom/MainOpaqueX"));Configure(geometryMat);
            geometryMat.SetFloat("_CullOption",0);geometryMat.SetColor("_BaseColor",new Color(.28f,.49f,.56f,1));
            AssetDatabase.CreateAsset(geometryMat,assets+"/Materials/GeometryReference.mat");
            var objects=new GameObject[3];var lit=new Material[3];var flat=new Material[3];
            var labels=new TextMesh[3];var footers=new TextMesh[3];
            var title=Text("OPEN TWISTED CUBE / ALPHA COMPARISON",new Vector3(0,2.64f,-4),.19f,layers[0]);
            var subtitle=Text("",new Vector3(0,2.30f,-4),.12f,layers[0]);
            Text("One open shell per object. Same mesh, color and coverage. No dither or screen-color sampling.",new Vector3(0,-2.65f,-4),.115f,layers[0]);
            string[] settings={"Cull Off | ZWrite Off","Cull Off | Depth prepass On","Cull Back | Back + Front ZWrite On"};
            for(int n=0;n<3;n++)
            {
                float x=(n-1)*3.6f;
                Shader shader=Shader.Find("tom/"+Names[n]);if(shader==null)throw new Exception("Missing shader "+Names[n]);
                lit[n]=new Material(shader);Configure(lit[n]);
                lit[n].SetFloat("_Alpha",.45f);lit[n].SetFloat("_AlphaOptionCutoff",0);
                lit[n].SetFloat("_CullOption",n==2?2:0);lit[n].SetFloat("_AlphaOptionZWrite",n==0?0:1);
                AssetDatabase.CreateAsset(lit[n],assets+"/Materials/"+Names[n]+"_Lit.mat");
                flat[n]=new Material(lit[n]);flat[n].SetFloat("_DebugView",6);
                flat[n].SetTexture("_EmissionMask",Texture2D.whiteTexture);
                flat[n].SetColor("_EmissionColor",new Color(.13f,.17f,.20f,1));flat[n].SetFloat("_EmissionIntensity",1);
                AssetDatabase.CreateAsset(flat[n],assets+"/Materials/"+Names[n]+"_Coverage.mat");
                objects[n]=new GameObject(Names[n]);objects[n].layer=layers[n];objects[n].transform.position=new Vector3(x,-.13f,0);
                objects[n].AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=objects[n].AddComponent<MeshRenderer>();renderer.sharedMaterial=lit[n];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                var point=new GameObject("Point "+Names[n]).AddComponent<Light>();
                point.type=LightType.Point;point.renderMode=LightRenderMode.ForcePixel;point.range=5;point.intensity=.8f;
                point.transform.position=new Vector3(x+.8f,.8f,-2);point.cullingMask=1<<layers[n];point.shadows=LightShadows.None;
                var bg=GameObject.CreatePrimitive(PrimitiveType.Quad);bg.name="Grid "+n;bg.layer=layers[n];
                Object.DestroyImmediate(bg.GetComponent<Collider>());bg.transform.position=new Vector3(x,-.13f,3);
                bg.transform.localScale=new Vector3(3.35f,3.62f,1);bg.GetComponent<Renderer>().sharedMaterial=gridMat;
                labels[n]=Text(Names[n],new Vector3(x,1.94f,-4),.15f,layers[n]);
                footers[n]=Text(settings[n],new Vector3(x,-2.17f,-4),.107f,layers[n]);
            }
            Action<string,bool,float,float,bool> capture=(file,coverage,yaw,pitch,reverse)=>{
                title.text="OPEN TWISTED CUBE / "+(coverage?"COVERAGE ONLY":"TOON + DIRECTIONAL + POINT");
                subtitle.text="Alpha 0.45 | "+yaw+" deg yaw | "+pitch+" deg tilt | "+(reverse?"REVERSED TRIANGLE ORDER":"Original triangle order");
                for(int n=0;n<3;n++)
                {
                    objects[n].transform.rotation=Quaternion.Euler(pitch,yaw,0);
                    objects[n].GetComponent<MeshRenderer>().sharedMaterial=coverage?flat[n]:lit[n];
                    objects[n].GetComponent<MeshFilter>().sharedMesh=reverse?reversed:mesh;
                }
                Capture(camera,rt,read,png,output,file,report,coverage?"coverage":"lit",yaw,pitch);
            };
            capture("01-coverage-open-view",true,-20,16,false);
            capture("02-toon-open-view",false,-20,16,false);
            capture("03-coverage-side-view",true,80,16,false);
            capture("04-toon-side-view",false,80,16,false);
            capture("05-coverage-reversed-triangles",true,-20,16,true);
            title.text="GEOMETRY REFERENCE / OPAQUE";
            subtitle.text="One side removed | 130 deg total twist | 3125 vertices | 5760 triangles";
            for(int n=0;n<3;n++)
            {
                objects[n].GetComponent<MeshFilter>().sharedMesh=mesh;
                objects[n].GetComponent<MeshRenderer>().sharedMaterial=geometryMat;
                objects[n].transform.rotation=Quaternion.Euler(16,-20+n*90,0);
                labels[n].text="Yaw "+(-20+n*90)+" deg";footers[n].text="Same open shell / no added inner surface";
            }
            Capture(camera,rt,read,png,output,"06-opaque-geometry",report,"geometry",0,16);

            // Save a usable lit comparison, not the diagnostic capture state or a RenderTexture reference.
            for(int n=0;n<3;n++) {labels[n].text=Names[n];footers[n].text=settings[n];}
            capture("07-saved-scene-view",false,-20,16,false);
            camera.targetTexture=null;camera.enabled=true;
            report.scene=assets+"/TwistedAlphaComparison.unity";
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,report.scene))throw new Exception("Cannot save comparison scene.");
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            return "Rendered "+report.shots.Count+" images: "+output+"; saved scene: "+report.scene+". No production shaders or existing materials changed.";
        }
        finally
        {
            RenderTexture.active=previousRT;QualitySettings.pixelLightCount=oldPixelLights;
            foreach(var pair in oldMasks)if(pair.Key!=null)pair.Key.cullingMask=pair.Value;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
            foreach(Object item in temporary)if(item!=null)Object.DestroyImmediate(item);
        }
    }

    static void Configure(Material material)
    {
        material.SetColor("_BaseColor",new Color(.20f,.28f,.34f,1));
        material.SetFloat("_ReflectionMode",0);material.SetFloat("_OutlineOn",0);material.SetFloat("_RimStrength",0);
        material.SetFloat("_LightProbeBlend",0);material.SetFloat("_CustomSHVolumeBlend",0);material.SetFloat("_VertexLightIntensity",0);
        material.SetFloat("_IndirectDiffuseIntensity",.4f);material.SetFloat("_ToonShadeLevel",.35f);
        material.SetFloat("_SpecularStrength",.15f);material.SetFloat("_Roughness",.65f);
        if(material.HasProperty("_CastShadows"))material.SetFloat("_CastShadows",0);
    }
    static void Capture(Camera camera,RenderTexture rt,Texture2D read,Texture2D png,string folder,string file,Report report,string mode,float yaw,float pitch)
    {
        camera.Render();camera.Render();RenderTexture.active=rt;
        read.ReadPixels(new Rect(0,0,Width,Height),0,0);read.Apply(false);
        Color[] pixels=read.GetPixels();var shot=new Shot {file=file+".png",mode=mode,yaw=yaw,pitch=pitch};
        bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
        for(int n=0;n<pixels.Length;n++)
        {
            Color c=pixels[n];
            if(!Finite(c.r)||!Finite(c.g)||!Finite(c.b))shot.nonFinite++;
            if(linear)c=c.gamma;
            pixels[n]=new Color(Mathf.Clamp01(c.r),Mathf.Clamp01(c.g),Mathf.Clamp01(c.b),1);
        }
        if(shot.nonFinite!=0)throw new Exception(file+": nonfinite pixels");
        png.SetPixels(pixels);png.Apply(false);File.WriteAllBytes(Path.Combine(folder,shot.file),png.EncodeToPNG());
        report.shots.Add(shot);
    }
    static bool Finite(float f) {return !float.IsNaN(f)&&!float.IsInfinity(f);}
    static TextMesh Text(string text,Vector3 position,float size,int layer)
    {
        var obj=new GameObject("Label");obj.layer=layer;obj.transform.position=position;
        var t=obj.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize=72;t.characterSize=size*10f/t.fontSize;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
        t.color=new Color(.13f,.17f,.20f,1);obj.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        return t;
    }
    static Texture2D MakeGrid()
    {
        const int size=512;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false,true);tex.name="Quiet background grid";
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            pixels[y*size+x]=(x%64<2||y%64<2)?new Color(.84f,.86f,.87f,1):new Color(.94f,.95f,.96f,1);
        tex.SetPixels(pixels);tex.Apply();tex.wrapMode=TextureWrapMode.Clamp;return tex;
    }
    public static string Retake(string scenePath)
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before rendering.");
        if(string.IsNullOrEmpty(scenePath)||!scenePath.StartsWith(Root+"/TwistedAlpha-",StringComparison.Ordinal)
            ||!scenePath.EndsWith("/TwistedAlphaComparison.unity",StringComparison.Ordinal)||scenePath.Contains(".."))
            throw new Exception("Expected a generated twisted-alpha scene.");
        if(SceneManager.GetSceneByPath(scenePath).isLoaded)throw new Exception("Close the comparison scene before retaking it.");
        Scene previous=SceneManager.GetActiveScene();RenderTexture oldRT=RenderTexture.active;
        int oldLights=QualitySettings.pixelLightCount;
        var oldMasks=new Dictionary<Light,int>();foreach(Light l in Object.FindObjectsOfType<Light>())oldMasks.Add(l,l.cullingMask);
        Scene scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        var temporary=new List<Object>();
        string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"CodexBridge/Reports/TwistedAlpha-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(output);
        try
        {
            SceneManager.SetActiveScene(scene);QualitySettings.pixelLightCount=8;
            var objects=new GameObject[3];var labels=new TextMesh[3];var footers=new TextMesh[3];
            Camera camera=null;TextMesh title=null,subtitle=null;
            foreach(GameObject root in scene.GetRootGameObjects())
            {
                var c=root.GetComponent<Camera>();if(c!=null)camera=c;
                for(int n=0;n<3;n++)if(root.name==Names[n])objects[n]=root;
                var text=root.GetComponent<TextMesh>();if(text==null)continue;
                // Repair the first fixture's unnormalized TextMesh font-size scaling.
                if(text.characterSize>.1f)text.characterSize*=10f/text.fontSize;
                if(text.transform.position.y>2.5f)title=text;
                else if(text.transform.position.y>2.2f)subtitle=text;
                for(int n=0;n<3;n++)if(text.text==Names[n])labels[n]=text;
                if(Mathf.Abs(text.transform.position.y+2.17f)<.01f)
                    footers[Mathf.RoundToInt(text.transform.position.x/3.6f)+1]=text;
            }
            if(camera==null||title==null||subtitle==null)throw new Exception("Incomplete comparison scene.");
            foreach(var pair in oldMasks)if(pair.Key!=null)pair.Key.cullingMask &= ~camera.cullingMask;
            EditorSceneManager.SaveScene(scene);
            string assets=scenePath.Substring(0,scenePath.LastIndexOf('/'));
            var lit=new Material[3];var flat=new Material[3];
            for(int n=0;n<3;n++)
            {
                if(objects[n]==null||labels[n]==null)throw new Exception("Missing comparison object "+Names[n]);
                lit[n]=AssetDatabase.LoadAssetAtPath<Material>(assets+"/Materials/"+Names[n]+"_Lit.mat");
                flat[n]=AssetDatabase.LoadAssetAtPath<Material>(assets+"/Materials/"+Names[n]+"_Coverage.mat");
            }
            Mesh mesh=objects[0].GetComponent<MeshFilter>().sharedMesh;
            Mesh reverse=Object.Instantiate(mesh);temporary.Add(reverse);
            int[] indices=mesh.triangles;Array.Reverse(indices);
            for(int i=0;i<indices.Length;i+=3) {int swap=indices[i];indices[i]=indices[i+2];indices[i+2]=swap;}
            reverse.triangles=indices;
            var report=new Report {unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),
                colorSpace=QualitySettings.activeColorSpace.ToString(),scene=scenePath,mesh=AssetDatabase.GetAssetPath(mesh),
                vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3};
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            rt.Create();temporary.Add(rt);camera.targetTexture=rt;camera.enabled=false;
            var read=new Texture2D(Width,Height,TextureFormat.RGBAFloat,false,true);temporary.Add(read);
            var png=new Texture2D(Width,Height,TextureFormat.RGB24,false);temporary.Add(png);
            Action<string,bool,float,bool> take=(name,coverage,yaw,reversed)=>{
                title.text="OPEN TWISTED CUBE / "+(coverage?"COVERAGE ONLY":"TOON + DIRECTIONAL + POINT");
                subtitle.text="Alpha 0.45 | "+yaw+" deg yaw | 16 deg tilt | "+(reversed?"REVERSED TRIANGLE ORDER":"Original triangle order");
                for(int n=0;n<3;n++)
                {
                    objects[n].transform.rotation=Quaternion.Euler(16,yaw,0);
                    objects[n].GetComponent<MeshRenderer>().sharedMaterial=coverage?flat[n]:lit[n];
                    objects[n].GetComponent<MeshFilter>().sharedMesh=reversed?reverse:mesh;
                }
                Capture(camera,rt,read,png,output,name,report,coverage?"coverage":"lit",yaw,16);
            };
            take("01-coverage-open-view",true,-20,false);
            take("02-toon-open-view",false,-20,false);
            take("03-coverage-side-view",true,80,false);
            take("04-toon-side-view",false,80,false);
            take("05-coverage-reversed-triangles",true,-20,true);
            take("07-toon-reversed-triangles",false,-20,true);
            title.text="GEOMETRY REFERENCE / OPAQUE";
            subtitle.text="One side removed | 130 deg total twist | "+mesh.vertexCount+" vertices | "+mesh.triangles.Length/3+" triangles";
            Material geo=AssetDatabase.LoadAssetAtPath<Material>(assets+"/Materials/GeometryReference.mat");
            for(int n=0;n<3;n++)
            {
                objects[n].GetComponent<MeshFilter>().sharedMesh=mesh;objects[n].GetComponent<MeshRenderer>().sharedMaterial=geo;
                objects[n].transform.rotation=Quaternion.Euler(16,-20+n*90,0);labels[n].text="Yaw "+(-20+n*90)+" deg";
                if(footers[n]!=null)footers[n].text="Opaque | Same open shell";
            }
            Capture(camera,rt,read,png,output,"06-opaque-geometry",report,"geometry",0,16);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            return "Rendered "+report.shots.Count+" images: "+output+"; scene labels repaired: "+scenePath;
        }
        finally
        {
            RenderTexture.active=oldRT;QualitySettings.pixelLightCount=oldLights;
            foreach(var pair in oldMasks)if(pair.Key!=null)pair.Key.cullingMask=pair.Value;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
            foreach(Object item in temporary)if(item!=null)Object.DestroyImmediate(item);
        }
    }
    static Mesh OpenTwistedCube()
    {
        const int div=24;
        var vertices=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
        Vector3[] centers={Vector3.right,Vector3.left,Vector3.forward,Vector3.up,Vector3.down};
        Vector3[] axesU={Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right};
        Vector3[] axesV={Vector3.up,Vector3.up,Vector3.up,Vector3.back,Vector3.forward};
        for(int face=0;face<centers.Length;face++)
        {
            int start=vertices.Count;
            for(int y=0;y<=div;y++)for(int x=0;x<=div;x++)
            {
                float u=x/(float)div,v=y/(float)div;
                Vector3 p=centers[face]+axesU[face]*(2*u-1)+axesV[face]*(2*v-1);
                float angle=(65*p.y+8*Mathf.Sin(Mathf.PI*p.y))*Mathf.Deg2Rad;
                float scale=.72f+.28f*p.y*p.y;
                float px=(p.x*Mathf.Cos(angle)+p.z*Mathf.Sin(angle))*scale+.24f*Mathf.Sin(p.y*Mathf.PI*.75f);
                float pz=(-p.x*Mathf.Sin(angle)+p.z*Mathf.Cos(angle))*scale+.12f*(1-p.y*p.y);
                vertices.Add(new Vector3(px,p.y*1.22f,pz));uvs.Add(new Vector2(u,v));
            }
            for(int y=0;y<div;y++)for(int x=0;x<div;x++)
            {
                int a=start+y*(div+1)+x,b=a+1,c=a+div+1,d=c+1;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
                triangles.Add(b);triangles.Add(d);triangles.Add(c);
            }
        }
        var mesh=new Mesh {name="Open cube - twisted 130 degrees"};
        mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    static int[] FindLayers()
    {
        var used=new HashSet<int>();foreach(GameObject obj in Object.FindObjectsOfType<GameObject>())used.Add(obj.layer);
        var layers=new List<int>();for(int n=31;n>=8&&layers.Count<3;n--)if(!used.Contains(n))layers.Add(n);
        if(layers.Count!=3)throw new Exception("Need three unused render layers.");return layers.ToArray();
    }
}

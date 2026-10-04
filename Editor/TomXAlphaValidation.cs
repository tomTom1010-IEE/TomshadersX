using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Bridge-only transient render fixture. Never saves a scene or material.
public static class TomXAlphaValidation
{
    const int Size = 192;
    [Serializable] class Check { public string name; public double error; public bool passed; }
    [Serializable] class Report
    {
        public string unity, api;
        public List<Check> checks = new List<Check>();
        public List<string> images = new List<string>();
    }
    public static string Run(bool includeBackFront = false, bool includeMergedPrepass = false)
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before regression.");
        int layer = -1;
        var occupied = new HashSet<int>();
        foreach (GameObject g in Object.FindObjectsOfType<GameObject>()) occupied.Add(g.layer);
        for (int n = 31; n >= 8; n--) if (!occupied.Contains(n)) { layer = n; break; }
        if (layer < 0) throw new Exception("No unused render layer.");
        Scene previous = SceneManager.GetActiveScene();
        RenderTexture oldRT = RenderTexture.active;
        int oldLights = QualitySettings.pixelLightCount;
        ShadowQuality oldShadows = QualitySettings.shadows;
        float oldDistance = QualitySettings.shadowDistance;
        var masks = new Dictionary<Light, int>();
        foreach (Light l in Object.FindObjectsOfType<Light>())
        { masks.Add(l, l.cullingMask); l.cullingMask &= ~(1 << layer); }
        var garbage = new List<Object>();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "CodexBridge/Reports/" + (includeMergedPrepass ? "XAlphaMerged-" : includeBackFront ? "XBackFront-" : "XAlpha-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        var report = new Report { unity = Application.unityVersion, api = SystemInfo.graphicsDeviceType.ToString() };
        try
        {
            SceneManager.SetActiveScene(scene);
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 30;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;
            var camera = new GameObject("Alpha regression camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << layer;
            camera.transform.position = new Vector3(0, 0, -3);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f, .16f, .24f, 1);
            camera.renderingPath = RenderingPath.Forward;
            camera.orthographic = true; camera.orthographicSize = .75f;
            camera.allowHDR = true; camera.allowMSAA = false;
            camera.nearClipPlane = .05f; camera.farClipPlane = 20;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            rt.Create(); garbage.Add(rt); camera.targetTexture = rt;
            var read = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true);
            var png = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            garbage.Add(read); garbage.Add(png);
            Func<string, Color[]> capture = name => {
                camera.Render(); camera.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); read.Apply(false);
                Color[] pixels = read.GetPixels();
                foreach (Color p in pixels)
                    if (!Finite(p.r) || !Finite(p.g) || !Finite(p.b))
                        throw new Exception("Nonfinite render: " + name);
                png.SetPixels(pixels); png.Apply(false);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), png.EncodeToPNG());
                report.images.Add(name);
                return pixels;
            };
            Action<string, Color[], Color[]> equal = (name, a, b) =>
                report.checks.Add(new Check { name = name, error = Difference(a, b), passed = Difference(a, b) < .003 });
            Action<string, Color[], Color[]> differs = (name, a, b) =>
                report.checks.Add(new Check { name = name, error = Difference(a, b), passed = Difference(a, b) > .01 });
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.layer = layer;
            Mesh original = quad.GetComponent<MeshFilter>().sharedMesh;
            Mesh overlap = DoubleQuad(original); garbage.Add(overlap);
            var sun = new GameObject("Main light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.renderMode = LightRenderMode.ForcePixel;
            sun.cullingMask = 1 << layer; sun.intensity = .35f;
            sun.transform.rotation = Quaternion.Euler(15, -25, 0);
            var point = new GameObject("Additional point").AddComponent<Light>();
            point.type = LightType.Point; point.renderMode = LightRenderMode.ForcePixel;
            point.cullingMask = 1 << layer; point.range = 5; point.intensity = 1.4f;
            point.transform.position = new Vector3(.3f, .3f, -1);
            var spot = new GameObject("Additional spot").AddComponent<Light>();
            spot.type = LightType.Spot; spot.renderMode = LightRenderMode.ForcePixel;
            spot.cullingMask = 1 << layer; spot.range = 6; spot.spotAngle = 100; spot.intensity = 1;
            spot.transform.position = new Vector3(-.3f, .3f, -1); spot.transform.LookAt(Vector3.zero);
            foreach (Light l in new[] {sun, point, spot}) l.enabled = false;
            quad.SetActive(false);
            Color[] background = capture("background"); quad.SetActive(true);
            var variants = new List<string> {"MainAlphaX", "MainAlphaX2Pass"};
            if (includeBackFront) variants.Add("MainAlphaXBackFront");
            if (includeMergedPrepass) variants.Add("MainAlphaXPrepass");
            foreach (string variant in variants)
            {
                bool mergedPrepass = variant == "MainAlphaXPrepass";
                bool prepass = mergedPrepass || variant.EndsWith("2Pass");
                string depthProperty = mergedPrepass ? "_DepthPrepass" : "_AlphaOptionZWrite";
                Shader shader = Shader.Find("tom/" + (mergedPrepass ? "MainAlphaX" : variant));
                if (shader == null) throw new Exception("Missing " + variant);
                var m = new Material(shader); garbage.Add(m);
                if (mergedPrepass) { m.SetFloat("_DepthPrepass", 1); m.SetFloat("_AlphaOptionZWrite", 0); }
                quad.GetComponent<Renderer>().sharedMaterial = m;
                m.SetFloat("_CullOption", 0); m.SetFloat("_ReflectionMode", 0);
                m.SetFloat("_LightProbeBlend", 0); m.SetFloat("_CustomSHVolumeBlend", 0);
                m.SetFloat("_VertexLightIntensity", 0); m.SetFloat("_OutlineOn", 0);
                m.SetFloat("_DebugView", 6);
                m.SetTexture("_EmissionMask", Texture2D.whiteTexture);
                m.SetColor("_EmissionColor", new Color(.6f, .3f, .15f, 1));
                m.SetFloat("_EmissionIntensity", 1);
                m.SetFloat("_Alpha", 1);
                Color[] full = capture(variant + "-full");
                differs(variant + " emission fixture visible", background, full);
                foreach (float a in new[] {0f, .25f, .5f, 1f})
                {
                    m.SetFloat("_Alpha", a); m.SetFloat("_AlphaBlendMode", 5);
                    string label = variant + "-alpha-" + a.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    Color[] straight = capture(label);
                    var expected = new Color[full.Length];
                    for (int i=0;i<expected.Length;i++) expected[i] = Color.Lerp(background[i], full[i], a);
                    equal(label + " coverage", expected, straight);
                    m.SetFloat("_AlphaBlendMode", 1);
                    equal(label + " premult equivalence", straight, capture(label + "-premult"));
                }
                m.SetFloat("_AlphaBlendMode", 5);
                Texture2D main = Solid(new Color(1, 1, 1, .5f)), mask = Solid(new Color(.5f, .5f, .5f, 1));
                garbage.Add(main); garbage.Add(mask);
                m.SetTexture("_MainTex", main); m.SetTexture("_AlphaMask", mask);
                m.SetFloat("_Alpha", .5f);
                var product = new Color[full.Length];
                for (int i=0;i<product.Length;i++) product[i] = Color.Lerp(background[i], full[i], .125f);
                equal(variant + " main*mask*global", product, capture(variant + "-coverage-product"));
                m.SetFloat("_AlphaOptionCutoff", 1); m.SetFloat("_Cutoff", .2f);
                equal(variant + " global cutoff", background, capture(variant + "-global-cut"));
                m.SetTexture("_MainTex", null); m.SetTexture("_AlphaMask", null);
                m.SetFloat("_AlphaOptionCutoff", 0); m.SetFloat("_Alpha", .5f);

                // A later transparent-queue marker verifies actual framebuffer depth.
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad); marker.layer = layer;
                marker.transform.position = new Vector3(0, 0, .2f); marker.transform.localScale = Vector3.one * .4f;
                var markerMat = new Material(Shader.Find("Unlit/Color")); garbage.Add(markerMat);
                markerMat.color = Color.green; markerMat.renderQueue = 3100;
                marker.GetComponent<Renderer>().sharedMaterial = markerMat;
                m.SetFloat(depthProperty, 0);
                Color[] noDepth = capture(variant + "-no-depth");
                m.SetFloat(depthProperty, 1); m.SetFloat("_DepthShadowCutoff", .4f);
                differs(variant + " qualifying depth occludes marker", noDepth, capture(variant + "-depth"));
                m.SetFloat("_DepthShadowCutoff", .6f);
                Color[] highThreshold = capture(variant + "-depth-threshold");
                if (prepass) equal(variant + " depth threshold preserves color", noDepth, highThreshold);
                else { m.SetFloat("_Alpha", 0); equal(variant + " single-pass threshold also clips color", capture(variant + "-zero-marker"), highThreshold); }
                m.SetFloat("_Alpha", 0); m.SetFloat("_DepthShadowCutoff", 0);
                Color[] zeroMarker = capture(variant + "-zero-depth");
                quad.SetActive(false); equal(variant + " zero alpha leaves no depth", capture(variant + "-hidden-depth"), zeroMarker); quad.SetActive(true);
                m.SetFloat("_Alpha", .4f); m.SetFloat("_AlphaOptionCutoff", 1); m.SetFloat("_Cutoff", .5f);
                equal(variant + " global cutoff leaves no depth", zeroMarker, capture(variant + "-global-depth"));
                Object.DestroyImmediate(marker);
                m.SetFloat("_AlphaOptionCutoff", 0); m.SetFloat("_Alpha", .5f); m.SetFloat("_DepthShadowCutoff", .01f);
                m.SetFloat(depthProperty, prepass ? 1 : 0);
                m.SetFloat("_DebugView", 1);
                sun.enabled = true; point.enabled = false; spot.enabled = false;
                Color[] mainOnly = capture(variant + "-main-only");
                point.enabled = true; spot.enabled = true;
                Color[] multi = capture(variant + "-multi-light");
                differs(variant + " additional lights active", mainOnly, multi);
                m.SetFloat("_AlphaBlendMode", 1);
                equal(variant + " ForwardAdd premult equivalence", multi, capture(variant + "-multi-premult"));
                m.SetFloat("_AlphaBlendMode", 5);
                // Separate renderers must composite rear Base+Add before front Base+Add.
                var rear = GameObject.CreatePrimitive(PrimitiveType.Quad); rear.layer = layer;
                rear.transform.position = new Vector3(0,0,.15f);
                var rearMat = new Material(m); garbage.Add(rearMat);
                rearMat.SetFloat(depthProperty, 0); rearMat.SetColor("_BaseColor", Color.red);
                rear.GetComponent<Renderer>().sharedMaterial = rearMat;
                quad.SetActive(false); Color[] rearOnly = capture(variant + "-rear-only"); quad.SetActive(true);
                var layered = new Color[multi.Length];
                for(int i=0;i<layered.Length;i++) layered[i] = multi[i] + (rearOnly[i]-background[i])*.5f;
                equal(variant + " separate renderer Base/Add order", layered, capture(variant + "-two-renderers"));
                Object.DestroyImmediate(rear);
                // A merged self-overlap draws front first, intentionally bad for ordinary alpha.
                quad.GetComponent<MeshFilter>().sharedMesh = overlap;
                Color[] merged = capture(variant + "-merged-overlap");
                if(prepass) equal(variant + " prepass removes rear Base/Add", multi, merged);
                else differs(variant + " documented unsorted self-overlap", multi, merged);
                quad.GetComponent<MeshFilter>().sharedMesh = original;
                foreach(Light l in new[] {sun,point,spot}) l.enabled = false;
                m.SetFloat("_DebugView", 0); m.SetFloat("_Alpha", 0);
                m.SetFloat("_RimStrength", 5); m.SetFloat("_ReflectionMode", 1); m.SetFloat("_MatCapIntensity", 5);
                m.SetFloat("_OutlineOn", 1); m.SetColor("_OutlineColor", Color.white);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.layer = layer;
                sphere.GetComponent<Renderer>().sharedMaterial = m; quad.SetActive(false);
                equal(variant + " all art and outline vanish at zero", background, capture(variant + "-zero-art"));
                Object.DestroyImmediate(sphere); quad.SetActive(true);

                // Isolate shadow coverage from visible color using a shadows-only caster.
                var caster = quad.GetComponent<Renderer>();
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                quad.transform.localScale = Vector3.one * .4f;
                var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad); receiver.layer = layer;
                receiver.transform.position = new Vector3(0,0,.45f); receiver.transform.localScale = Vector3.one * 1.4f;
                var receiverMat = new Material(Shader.Find("tom/MainOpaqueX")); garbage.Add(receiverMat);
                receiverMat.SetFloat("_CullOption", 0); receiverMat.SetFloat("_SpecularStrength", 0);
                receiverMat.SetFloat("_IndirectDiffuseIntensity", 0); receiverMat.SetFloat("_VertexLightIntensity", 0);
                receiver.GetComponent<Renderer>().sharedMaterial = receiverMat;
                foreach(Light light in new[] {sun,point,spot})
                {
                    light.enabled=true; light.shadows=LightShadows.Hard;
                    light.shadowBias=.01f; light.shadowNormalBias=0;
                    string label=variant+"-shadow-"+light.type;
                    m.SetFloat("_CastShadows",0); m.SetFloat("_Alpha",1);
                    m.SetTexture("_MainTex",main); m.SetTexture("_AlphaMask",mask);
                    m.SetFloat("_AlphaOptionCutoff",0); m.SetFloat("_DepthShadowCutoff",.1f);
                    Color[] unshadowed=capture(label+"-disabled");
                    m.SetFloat("_CastShadows",1);
                    differs(label+" casts at coverage .25",unshadowed,capture(label+"-enabled"));
                    m.SetFloat("_DepthShadowCutoff",.3f);
                    equal(label+" shared threshold",unshadowed,capture(label+"-threshold"));
                    m.SetFloat("_DepthShadowCutoff",0); m.SetFloat("_Alpha",0);
                    equal(label+" zero alpha",unshadowed,capture(label+"-zero"));
                    m.SetFloat("_Alpha",1);m.SetFloat("_AlphaOptionCutoff",1);m.SetFloat("_Cutoff",.3f);
                    equal(label+" global cutoff",unshadowed,capture(label+"-global"));
                    light.enabled=false;light.shadows=LightShadows.None;
                }
                caster.shadowCastingMode=ShadowCastingMode.On;
                quad.transform.localScale=Vector3.one;
                Object.DestroyImmediate(receiver);
            }
            if(includeBackFront)
                BackFrontChecks(camera,quad,original,new[] {sun,point,spot},garbage,capture,equal,differs);
            if(includeMergedPrepass)
                MergedPrepassChecks(quad,garbage,capture,equal,differs);
            File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
            int failed = report.checks.FindAll(c => !c.passed).Count;
            if(failed != 0) throw new Exception(folder + "; failed checks=" + failed);
            return folder + "; " + report.checks.Count + " checks passed."
                + " This fixture is not game visual acceptance or DOF validation.";
        }
        finally
        {
            RenderTexture.active = oldRT;
            foreach(var pair in masks) if(pair.Key != null) pair.Key.cullingMask = pair.Value;
            QualitySettings.pixelLightCount = oldLights; QualitySettings.shadows = oldShadows;
            QualitySettings.shadowDistance = oldDistance;
            EditorSceneManager.CloseScene(scene, true);
            if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach(Object o in garbage) if(o != null) Object.DestroyImmediate(o);
        }
    }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static void MergedPrepassChecks(GameObject quad,List<Object> garbage,
        Func<string,Color[]> capture,Action<string,Color[],Color[]> equal,Action<string,Color[],Color[]> differs)
    {
        quad.SetActive(false);
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.layer=quad.layer;
        var renderer=sphere.GetComponent<Renderer>();
        var legacy=new Material(Shader.Find("tom/MainAlphaX2Pass"));garbage.Add(legacy);
        legacy.SetFloat("_Alpha",.35f);legacy.SetFloat("_OutlineOn",1);legacy.SetFloat("_OutlineWidth",5);
        legacy.SetColor("_OutlineColor",new Color(.05f,.15f,.35f,.6f));
        legacy.SetFloat("_CastShadows",0);legacy.SetFloat("_LightProbeBlend",0);legacy.SetFloat("_CustomSHVolumeBlend",0);
        legacy.SetFloat("_VertexLightIntensity",0);legacy.SetTexture("_EmissionMask",Texture2D.whiteTexture);
        legacy.SetColor("_EmissionColor",new Color(.2f,.1f,.05f,1));legacy.SetFloat("_EmissionIntensity",1);
        var merged=new Material(Shader.Find("tom/MainAlphaX"));garbage.Add(merged);
        foreach(float cull in new[] {0f,1f,2f}) foreach(float depth in new[] {0f,1f}) foreach(float cutoff in new[] {.2f,.5f})
        {
            sphere.transform.localScale=cull==1?new Vector3(-1,.8f,1):new Vector3(1,.8f,1);
            legacy.SetFloat("_CullOption",cull);legacy.SetFloat("_AlphaOptionZWrite",depth);
            legacy.SetFloat("_DepthShadowCutoff",cutoff);legacy.SetFloat("_AlphaBlendMode",depth==1?1:5);
            merged.CopyPropertiesFromMaterial(legacy);merged.SetFloat("_AlphaOptionZWrite",0);merged.SetFloat("_DepthPrepass",depth);
            string label="merged-outline-cull-"+cull+"-depth-"+depth+"-cut-"+cutoff.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
            renderer.sharedMaterial=legacy;Color[] reference=capture(label+"-legacy");
            renderer.sharedMaterial=merged;equal(label,reference,capture(label+"-merged"));
        }
        merged.SetFloat("_CullOption",0);merged.SetFloat("_DepthPrepass",1);merged.SetFloat("_AlphaOptionZWrite",0);
        merged.SetFloat("_DepthShadowCutoff",.5f);
        Color[] faint=capture("merged-faint-color");
        sphere.SetActive(false);Color[] hidden=capture("merged-hidden");sphere.SetActive(true);
        differs("Prepass preserves color below depth threshold",hidden,faint);
        merged.SetFloat("_AlphaOptionZWrite",1);
        equal("Both switches retain color-depth threshold clipping",hidden,capture("merged-both-switches"));
        merged.SetFloat("_AlphaOptionZWrite",0);
        equal("Color ZWrite can be switched off again without keyword synchronization",faint,capture("merged-switch-back"));
        Object.DestroyImmediate(sphere);
    }
    static void BackFrontChecks(Camera camera,GameObject quad,Mesh original,Light[] lights,List<Object> garbage,
        Func<string,Color[]> capture,Action<string,Color[],Color[]> equal,Action<string,Color[],Color[]> differs)
    {
        camera.backgroundColor=Color.white;
        var backMesh=Object.Instantiate(original); garbage.Add(backMesh);
        int[] triangles=backMesh.triangles;
        for(int i=0;i<triangles.Length;i+=3) { int v=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=v; }
        Vector3[] normals=backMesh.normals; for(int i=0;i<normals.Length;i++)normals[i]=-normals[i];
        backMesh.triangles=triangles;backMesh.normals=normals;
        var merged=new Mesh();garbage.Add(merged);
        merged.CombineMeshes(new[] {
            new CombineInstance {mesh=original,transform=Matrix4x4.identity},
            new CombineInstance {mesh=backMesh,transform=Matrix4x4.Translate(new Vector3(0,0,.15f))}
        },true,true);
        var m=new Material(Shader.Find("tom/MainAlphaXBackFront")); garbage.Add(m);
        m.SetFloat("_Alpha",.5f);m.SetFloat("_AlphaOptionCutoff",0);m.SetFloat("_OutlineOn",0);
        m.SetFloat("_LightProbeBlend",0);m.SetFloat("_CustomSHVolumeBlend",0);m.SetFloat("_VertexLightIntensity",0);
        m.SetFloat("_CastShadows",0);m.SetFloat("_DebugView",6);
        m.SetTexture("_EmissionMask",Texture2D.whiteTexture);m.SetColor("_EmissionColor",new Color(.1f,.1f,.1f));
        m.SetFloat("_EmissionIntensity",1);
        quad.GetComponent<MeshFilter>().sharedMesh=merged;quad.GetComponent<Renderer>().sharedMaterial=m;
        GameObject front=GameObject.CreatePrimitive(PrimitiveType.Quad), back=GameObject.CreatePrimitive(PrimitiveType.Quad);
        front.layer=back.layer=quad.layer;
        back.transform.position=new Vector3(0,0,.15f);back.GetComponent<MeshFilter>().sharedMesh=backMesh;
        Material mf=new Material(Shader.Find("tom/MainAlphaX")),mb=new Material(Shader.Find("tom/MainAlphaX"));
        garbage.Add(mf);garbage.Add(mb);
        mf.CopyPropertiesFromMaterial(m);mb.CopyPropertiesFromMaterial(m);
        mf.SetFloat("_CullOption",0);mb.SetFloat("_CullOption",0);
        mf.renderQueue=3001;mb.renderQueue=3000;
        front.GetComponent<Renderer>().sharedMaterial=mf;back.GetComponent<Renderer>().sharedMaterial=mb;
        front.SetActive(false);back.SetActive(false);
        Color[] dual=capture("pair-backfront-emission");
        quad.SetActive(false);front.SetActive(true);back.SetActive(true);
        equal("BackFront equals sorted two-surface coverage",dual,capture("pair-sorted-reference"));
        back.SetActive(false);Color[] single=capture("pair-single-reference");
        differs("Overlapping coverage accumulates",single,dual);
        front.SetActive(false);quad.SetActive(true);
        foreach(string name in new[] {"MainAlphaX","MainAlphaX2Pass"})
        {
            var comparison=new Material(Shader.Find("tom/"+name));garbage.Add(comparison);
            comparison.CopyPropertiesFromMaterial(m);comparison.SetFloat("_CullOption",0);
            comparison.SetFloat("_AlphaOptionZWrite",name.EndsWith("2Pass")?1:0);
            quad.GetComponent<Renderer>().sharedMaterial=comparison;
            Color[] pixels=capture("pair-compare-"+name);
            if(name.EndsWith("2Pass"))equal("Depth prepass intentionally keeps one surface",single,pixels);
            else equal("Sorted-identical colors ordinary alpha accumulation",dual,pixels);
        }
        quad.GetComponent<Renderer>().sharedMaterial=m;
        foreach(float cull in new[] {2f,1f,0f})
        {
            quad.GetComponent<MeshFilter>().sharedMesh=original;m.SetFloat("_CullOption",cull);
            equal("Single card drawn once cull "+cull,single,capture("single-card-cull-"+cull));
        }
        m.SetFloat("_CullOption",2);quad.GetComponent<MeshFilter>().sharedMesh=merged;
        // Distinct layer colors/coverage expose reversed order and unoccluded rear Add light.
        var frontMesh=Object.Instantiate(original);garbage.Add(frontMesh);
        Vector2[] frontUV=frontMesh.uv,backUV=backMesh.uv;
        for(int i=0;i<frontUV.Length;i++)frontUV[i]=new Vector2(.25f,.5f);
        for(int i=0;i<backUV.Length;i++)backUV[i]=new Vector2(.75f,.5f);
        frontMesh.uv=frontUV;backMesh.uv=backUV;
        front.GetComponent<MeshFilter>().sharedMesh=frontMesh;
        merged.CombineMeshes(new[] {
            new CombineInstance {mesh=frontMesh,transform=Matrix4x4.identity},
            new CombineInstance {mesh=backMesh,transform=Matrix4x4.Translate(new Vector3(0,0,.15f))}
        },true,true);
        var palette=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true);garbage.Add(palette);
        palette.filterMode=FilterMode.Point;palette.wrapMode=TextureWrapMode.Clamp;
        palette.SetPixels(new[] {new Color(.7f,.15f,.08f,.35f),new Color(.08f,.25f,.6f,.65f)});palette.Apply();
        foreach(Material material in new[] {m,mf,mb})material.SetTexture("_MainTex",palette);
        m.SetFloat("_DebugView",0);mf.SetFloat("_DebugView",0);mb.SetFloat("_DebugView",0);
        lights[0].enabled=true;lights[1].enabled=false;lights[2].enabled=false;
        Color[] mainOnly=capture("pair-backfront-main-only");
        for(int n=1;n<lights.Length;n++)
        {
            lights[n].enabled=true;
            Color[] lit=capture("pair-backfront-add-"+n);
            differs("BackFront additional light active "+n,mainOnly,lit);
            quad.SetActive(false);front.SetActive(true);back.SetActive(true);
            equal("BackFront colored layer order light "+n,capture("pair-sorted-add-"+n),lit);
            front.SetActive(false);back.SetActive(false);quad.SetActive(true);lights[n].enabled=false;
        }
        lights[0].enabled=true;
        lights[1].enabled=true;lights[2].enabled=true;
        Color[] allLights=capture("pair-backfront-multilight");
        quad.SetActive(false);front.SetActive(true);back.SetActive(true);
        equal("BackFront full Base/Add layer composition",capture("pair-sorted-multilight"),allLights);
        front.SetActive(false);back.SetActive(false);quad.SetActive(true);
        foreach(float backZ in new[] {0f,1f}) foreach(float frontZ in new[] {0f,1f})
        {
            m.SetFloat("_BackfaceZWrite",backZ);m.SetFloat("_AlphaOptionZWrite",frontZ);
            equal("Two-layer color with depth switches "+backZ+"/"+frontZ,allLights,
                capture("pair-depth-switches-"+backZ+"-"+frontZ));
        }
        m.SetFloat("_BackfaceZWrite",1);m.SetFloat("_AlphaOptionZWrite",1);
        m.SetFloat("_AlphaBlendMode",1);
        equal("BackFront multilight premult equivalence",allLights,capture("pair-backfront-multilight-premult"));
        m.SetFloat("_Alpha",0);
        Color[] zero=capture("pair-zero");quad.SetActive(false);
        equal("Both faces vanish at zero",capture("pair-hidden"),zero);
        Object.DestroyImmediate(front);Object.DestroyImmediate(back);
    }
    static double Difference(Color[] a, Color[] b)
    {
        double result=0;
        for(int i=0;i<a.Length;i++)
        { result=Math.Max(result,Math.Abs(a[i].r-b[i].r)); result=Math.Max(result,Math.Abs(a[i].g-b[i].g)); result=Math.Max(result,Math.Abs(a[i].b-b[i].b)); }
        return result;
    }
    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        t.SetPixel(0,0,c); t.Apply(); return t;
    }
    static Mesh DoubleQuad(Mesh source)
    {
        Vector3[] v=source.vertices, normals=source.normals; Vector2[] uv=source.uv; int[] t=source.triangles;
        var vertices=new Vector3[v.Length*2]; var n=new Vector3[v.Length*2]; var u=new Vector2[v.Length*2];
        var tangents=new Vector4[v.Length*2]; var tris=new int[t.Length*2];
        for(int i=0;i<v.Length;i++) for(int side=0;side<2;side++)
        { int j=i+side*v.Length; vertices[j]=v[i]+Vector3.forward*(side*.15f); n[j]=normals[i]; u[j]=uv[i]; tangents[j]=source.tangents[i]; }
        for(int i=0;i<t.Length;i++) { tris[i]=t[i]; tris[i+t.Length]=t[i]+v.Length; }
        var mesh=new Mesh { name="Transient alpha overlap", vertices=vertices, normals=n, uv=u, tangents=tangents, triangles=tris };
        mesh.RecalculateBounds(); return mesh;
    }
}

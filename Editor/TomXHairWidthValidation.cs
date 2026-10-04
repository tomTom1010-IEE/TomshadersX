using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TomShadersX;
using Object = UnityEngine.Object;

public static class TomXHairWidthValidation
{
    [Serializable] class Check { public string name; public double value; public bool passed; }
    [Serializable] class Report { public List<Check> checks = new List<Check>(); public List<string> captures = new List<string>(); }
    static double Difference(Color[] a, Color[] b)
    {
        double sum=0; for(int i=0;i<a.Length;i++) sum+=Delta(a[i],b[i]); return sum/a.Length;
    }
    static float Delta(Color a, Color b) { return Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))); }
    public static string Run()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play Mode first.");
        var report=new Report();
        Action<string,double,bool> check=(name,value,pass)=>report.checks.Add(new Check{name=name,value=value,passed=pass});
        string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"CodexBridge/Reports/XHairWidth-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        string compiled=TomXStageTwoValidation.Validate("tom/HairX");
        var occupied=new HashSet<int>(); foreach(GameObject g in Object.FindObjectsOfType<GameObject>()) occupied.Add(g.layer);
        int layer=-1; for(int n=31;n>=8;n--) if(!occupied.Contains(n)){layer=n;break;}
        if(layer<0) throw new Exception("No unused layer.");
        Scene previous=SceneManager.GetActiveScene();
        var masks=new Dictionary<Light,int>(); foreach(Light l in Object.FindObjectsOfType<Light>()){masks.Add(l,l.cullingMask);l.cullingMask&=~(1<<layer);}
        int oldLights=QualitySettings.pixelLightCount;
        RenderTexture previousRT=RenderTexture.active;
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var garbage=new List<Object>();
        try
        {
            SceneManager.SetActiveScene(scene); QualitySettings.pixelLightCount=8;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.skybox=null;RenderSettings.fog=false;
            Camera camera=new GameObject("Hair width camera").AddComponent<Camera>();camera.enabled=false;
            camera.cullingMask=1<<layer;camera.renderingPath=RenderingPath.Forward;camera.nearClipPlane=.01f;camera.farClipPlane=30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.2f,1);
            camera.allowHDR=true;camera.allowMSAA=false;camera.fieldOfView=28;camera.orthographicSize=.75f;
            camera.transform.position=new Vector3(0,0,-3);camera.transform.LookAt(Vector3.zero);
            var hair=GameObject.CreatePrimitive(PrimitiveType.Quad);hair.layer=layer;
            var mat=new Material(Shader.Find("tom/HairX"));garbage.Add(mat);hair.GetComponent<Renderer>().sharedMaterial=mat;
            mat.SetFloat("_DebugView",6);mat.SetTexture("_EmissionMask",Texture2D.whiteTexture);mat.SetColor("_EmissionColor",new Color(.7f,.35f,.15f,1));mat.SetFloat("_EmissionIntensity",1);
            mat.SetFloat("_HairFrontOpacity",.5f);mat.SetFloat("_HairFeatherWidth",16);mat.SetFloat("_HairFeatherWorldWidth",.05f);
            mat.SetFloat("_VertexLightIntensity",0);mat.SetFloat("_LightProbeBlend",0);mat.SetFloat("_CustomSHVolumeBlend",0);
            var eye=GameObject.CreatePrimitive(PrimitiveType.Quad);eye.layer=layer;
            eye.transform.position=new Vector3(.08f,.07f,.25f);eye.transform.localScale=new Vector3(.3f,.25f,1);
            var eyeMat=new Material(Shader.Find("Hidden/TomX/HairStencilFixture"));garbage.Add(eyeMat);eye.GetComponent<Renderer>().sharedMaterial=eyeMat;
            var helper=camera.gameObject.AddComponent<TomHairFeatherCamera>();
            helper.receivers=new[]{hair.GetComponent<Renderer>()};helper.sources=new[]{new TomHairFeatherCamera.Source{renderer=eye.GetComponent<Renderer>()}};
            int size=512;
            RenderTexture target=null;
            Action<int> resize=n=>{
                size=n; target=new RenderTexture(n,n,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                target.Create();garbage.Add(target);camera.targetTexture=target;
            };
            resize(size);
            Transform animatedBone=null;
            Vector3 desiredBonePosition=Vector3.zero;
            Func<string,Color[]> capture=name=>{
                RenderTexture.active=target;GL.Clear(true,true,camera.backgroundColor);
                if(animatedBone){animatedBone.localPosition=Vector3.zero;camera.Render();animatedBone.localPosition=desiredBonePosition;camera.Render();}
                else {camera.Render();camera.Render();}
                RenderTexture.active=target;
                var read=new Texture2D(size,size,TextureFormat.RGBAFloat,false,true);
                read.ReadPixels(new Rect(0,0,size,size),0,0);read.Apply();Color[] pixels=read.GetPixels();Object.DestroyImmediate(read);
                foreach(Color p in pixels) if(float.IsNaN(p.r)||float.IsInfinity(p.r)||float.IsNaN(p.g)||float.IsInfinity(p.g)||float.IsNaN(p.b)||float.IsInfinity(p.b)) throw new Exception("Nonfinite "+name);
                var png=new Texture2D(size,size,TextureFormat.RGB24,false);png.SetPixels(pixels);png.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),png.EncodeToPNG());Object.DestroyImmediate(png);
                report.captures.Add(name);return pixels;
            };
            Action<string> rectangle=name=>{
                File.AppendAllText(Path.Combine(folder,"camera.txt"),name+": rect="+camera.rect+" pixelRect="+camera.pixelRect+" pixels="+camera.pixelWidth+"x"+camera.pixelHeight+" aspect="+camera.aspect+"\n");
                mat.SetFloat("_HairFrontMode",0);Color[] full=capture(name+"-off");
                mat.SetFloat("_HairFrontMode",1);Color[] hard=capture(name+"-hard");
                mat.SetFloat("_HairFrontMode",2);Color[] feather=capture(name+"-feather");
                int minX=size,minY=size,maxX=-1,maxY=-1;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(Delta(full[y*size+x],hard[y*size+x])>.02f){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
                check(name+" stencil visible",maxX,maxX>minX&&maxY>minY);
                float depth=camera.orthographic?1:-camera.worldToCameraMatrix.MultiplyPoint(Vector3.zero).z;
                float width=mat.GetFloat("_HairFeatherWidthMode")>.5f?Mathf.Min(mat.GetFloat("_HairFeatherWorldWidth")*.5f*camera.pixelHeight*Mathf.Abs(camera.projectionMatrix.m11)/depth,helper.SearchRadiusPixels):mat.GetFloat("_HairFeatherWidth");
                double sum=0;int count=0,leaks=0;float worstEnvelopeError=0;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                    int i=y*size+x;bool inside=Delta(full[i],hard[i])>.02f;
                    if(!inside){if(Delta(full[i],feather[i])>.01f)leaks++;continue;}
                    float d=Mathf.Min(Mathf.Min(x-minX+.5f,maxX-x+.5f),Mathf.Min(y-minY+.5f,maxY-y+.5f));
                    Color expected=Color.Lerp(full[i],hard[i],Mathf.SmoothStep(0,1,d/width));sum+=Delta(expected,feather[i]);count++;
                    // Half-resolution binary edges have up to two display pixels of raster uncertainty.
                    // Exclude that edge strip, then bound the curve by its +/-2px analytic envelope.
                    if(d>2){
                        Color a=Color.Lerp(full[i],hard[i],Mathf.SmoothStep(0,1,(d-2)/width));
                        Color b=Color.Lerp(full[i],hard[i],Mathf.SmoothStep(0,1,(d+2)/width));
                        for(int channel=0;channel<3;channel++) worstEnvelopeError=Mathf.Max(worstEnvelopeError,
                            Mathf.Max(Mathf.Min(a[channel],b[channel])-feather[i][channel],feather[i][channel]-Mathf.Max(a[channel],b[channel])));
                    }
                }
                double error=sum/Math.Max(1,count);
                check(name+" active",helper.SourceDraws,helper.Active&&helper.SourceDraws==1);
                check(name+" projected width within 2px field precision",worstEnvelopeError,worstEnvelopeError<.01);
                check(name+" stays inside stencil",leaks,leaks==0);
                check(name+" differs from Hard",Difference(hard,feather),Difference(hard,feather)>.00005);
                File.AppendAllText(Path.Combine(folder,"camera.txt"),"  widthPixels="+width+" meanAnalyticColorError="+error+"\n");
                if(mat.GetFloat("_HairFeatherWidthMode")>.5f&&width<=32){
                    float oldPixelWidth=mat.GetFloat("_HairFeatherWidth");
                    mat.SetFloat("_HairFeatherWidthMode",0);mat.SetFloat("_HairFeatherWidth",width);
                    Color[] reference=capture(name+"-pixel-reference");
                    check(name+" equals independently projected pixel width",Difference(reference,feather),Difference(reference,feather)<.00001);
                    mat.SetFloat("_HairFeatherWidthMode",1);mat.SetFloat("_HairFeatherWidth",oldPixelWidth);
                }
            };
            foreach(Rect rect in new[]{new Rect(0,0,1,1),new Rect(.25f,0,.75f,1),new Rect(.33f,0,1,1),new Rect(.2f,.15f,.6f,.7f)}){
                camera.rect=rect;rectangle("pixels-viewport-"+rect.x+"-"+rect.y);
                mat.SetFloat("_HairFeatherWidthMode",1);rectangle("world-viewport-"+rect.x+"-"+rect.y);mat.SetFloat("_HairFeatherWidthMode",0);
            }
            camera.rect=new Rect(0,0,1,1);mat.SetFloat("_HairFeatherWidthMode",1);
            camera.allowHDR=false;camera.rect=new Rect(.33f,0,1,1);rectangle("world-viewport-ldr");camera.allowHDR=true;camera.rect=new Rect(0,0,1,1);
            Shader installedEye=Shader.Find("xukmi/EyePlus");
            if(installedEye){
                var actualEyeMat=new Material(installedEye);garbage.Add(actualEyeMat);actualEyeMat.renderQueue=2474;
                actualEyeMat.SetFloat("_exppower",0);actualEyeMat.SetFloat("_isHighLight",0);
                eye.GetComponent<Renderer>().sharedMaterial=actualEyeMat;helper.sources[0].passName="Forward";
                camera.rect=new Rect(.33f,0,1,1);rectangle("world-xukmi-partial");
                eye.GetComponent<Renderer>().sharedMaterial=eyeMat;helper.sources[0].passName="StencilMask";
            }
            // Compare a ring + overlapping iris + eyeliner to their single-writer union.
            // A missing iris source is a negative control: real stencil stays filled,
            // while its proxy gains an internal edge that only Feather exposes.
            {
                bool oldOrtho=camera.orthographic;Rect oldRect=camera.rect;
                Vector3 oldPosition=eye.transform.position,oldScale=eye.transform.localScale;
                camera.orthographic=true;camera.orthographicSize=.75f;
                eye.transform.position=new Vector3(0,0,.25f);eye.transform.localScale=new Vector3(.8f,.7f,1);
                var iris=GameObject.CreatePrimitive(PrimitiveType.Quad);iris.layer=layer;
                var liner=GameObject.CreatePrimitive(PrimitiveType.Quad);liner.layer=layer;
                foreach(var part in new[]{iris,liner}){part.transform.position=eye.transform.position;part.transform.localScale=eye.transform.localScale;}
                Func<int,Texture2D> makeMask=kind=>{
                    var tex=new Texture2D(256,256,TextureFormat.RGBA32,true,true);garbage.Add(tex);
                    tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
                    var pixels=new Color[256*256];
                    for(int y=0;y<256;y++)for(int x=0;x<256;x++){
                        float u=(x+.5f)/256,v=(y+.5f)/256;
                        bool inside=kind==0 ? (u<.25f||u>.75f||v<.25f||v>.75f)
                            :kind==1 ? (u>.2f&&u<.8f&&v>.2f&&v<.8f) : v>.65f;
                        pixels[y*256+x]=new Color(1,1,1,inside?1:0);
                    }
                    tex.SetPixels(pixels);tex.Apply(true);return tex;
                };
                Texture2D ringTexture=makeMask(0),irisTexture=makeMask(1),linerTexture=makeMask(2);
                Func<RenderTexture,string,Color[]> fieldCapture=(rt,name)=>{
                    RenderTexture.active=rt;var read=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);
                    read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);read.Apply();var pixels=read.GetPixels();Object.DestroyImmediate(read);
                    var png=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    Color[] display=(Color[])pixels.Clone();if(name.EndsWith("distance"))for(int i=0;i<display.Length;i++)display[i]=Color.white*Mathf.Max(0,display[i].r)/32;
                    png.SetPixels(display);png.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),png.EncodeToPNG());Object.DestroyImmediate(png);report.captures.Add(name);
                    return pixels;
                };
                foreach(bool actual in new[]{false,true}){
                    Shader whiteShader=Shader.Find(actual?"xukmi/EyeWPlus":"Hidden/TomX/HairStencilFixture");
                    Shader irisShader=Shader.Find(actual?"xukmi/EyePlus":"Hidden/TomX/HairStencilFixture");
                    if(!whiteShader||!irisShader)throw new Exception("Overlap integration requires installed EyePlus/EyeWPlus.");
                    var ringMat=new Material(whiteShader);var irisMat=new Material(irisShader);var linerMat=new Material(whiteShader);
                    foreach(Material m in new[]{ringMat,irisMat,linerMat}){garbage.Add(m);m.renderQueue=2474;}
                    if(actual){irisMat.SetFloat("_exppower",0);irisMat.SetFloat("_isHighLight",0);}
                    ringMat.SetTexture("_MainTex",ringTexture);irisMat.SetTexture("_MainTex",irisTexture);linerMat.SetTexture("_MainTex",linerTexture);
                    eye.GetComponent<Renderer>().sharedMaterial=ringMat;iris.GetComponent<Renderer>().sharedMaterial=irisMat;liner.GetComponent<Renderer>().sharedMaterial=linerMat;
                    string pass=actual?"Forward":"StencilMask";
                    var ringSource=new TomHairFeatherCamera.Source{renderer=eye.GetComponent<Renderer>(),passName=pass};
                    var irisSource=new TomHairFeatherCamera.Source{renderer=iris.GetComponent<Renderer>(),passName=pass};
                    var linerSource=new TomHairFeatherCamera.Source{renderer=liner.GetComponent<Renderer>(),passName=pass};
                    foreach(int sample in new[]{1,2})foreach(float rectX in new[]{0f,.33f}){
                        string label="overlap-"+(actual?"xukmi":"fixture")+"-sample"+sample+"-rect"+rectX;
                        camera.rect=new Rect(rectX,0,1,1);helper.downsample=sample;mat.SetFloat("_HairFrontMode",2);
                        iris.SetActive(false);liner.SetActive(false);ringMat.SetTexture("_MainTex",Texture2D.whiteTexture);helper.sources=new[]{ringSource};
                        Color[] reference=capture(label+"-union");
                        Color[] referenceMask=fieldCapture(helper.MaskTexture,label+"-union-mask");
                        Color[] referenceDistance=fieldCapture(helper.DistanceTexture,label+"-union-distance");
                        iris.SetActive(true);liner.SetActive(true);ringMat.SetTexture("_MainTex",ringTexture);helper.sources=new[]{ringSource,irisSource,linerSource};
                        Color[] joined=capture(label+"-joined");
                        Color[] joinedMask=fieldCapture(helper.MaskTexture,label+"-joined-mask");
                        Color[] joinedDistance=fieldCapture(helper.DistanceTexture,label+"-joined-distance");
                        check(label+" union mask exact",Difference(referenceMask,joinedMask),Difference(referenceMask,joinedMask)==0);
                        check(label+" union distance exact",Difference(referenceDistance,joinedDistance),Difference(referenceDistance,joinedDistance)==0);
                        if(!actual)check(label+" color exact",Difference(reference,joined),Difference(reference,joined)<.00001);
                        helper.sources=new[]{linerSource,irisSource,ringSource};capture(label+"-reordered");
                        var reordered=fieldCapture(helper.DistanceTexture,label+"-reordered-distance");
                        check(label+" writer order independent",Difference(joinedDistance,reordered),Difference(joinedDistance,reordered)==0);
                        if(!actual){
                            helper.sources=new[]{ringSource,linerSource};var missing=capture(label+"-missing-iris");
                            fieldCapture(helper.MaskTexture,label+"-missing-mask");fieldCapture(helper.DistanceTexture,label+"-missing-distance");
                            check(label+" missing writer causes Feather artifact",Difference(joined,missing),Difference(joined,missing)>.001);
                            mat.SetFloat("_HairFrontMode",1);var hardMissing=capture(label+"-hard-missing");
                            helper.sources=new[]{ringSource,irisSource,linerSource};var hardComplete=capture(label+"-hard-complete");
                            check(label+" missing writer cannot alter Hard",Difference(hardMissing,hardComplete),Difference(hardMissing,hardComplete)==0);
                            mat.SetFloat("_HairFrontMode",2);helper.sources=new[]{irisSource,linerSource};
                            var missingWhite=capture(label+"-missing-white");
                            fieldCapture(helper.MaskTexture,label+"-missing-white-mask");fieldCapture(helper.DistanceTexture,label+"-missing-white-distance");
                            check(label+" missing white causes Feather artifact",Difference(joined,missingWhite),Difference(joined,missingWhite)>.001);
                            mat.SetFloat("_HairFrontMode",1);var hardMissingWhite=capture(label+"-hard-missing-white");
                            check(label+" missing white cannot alter Hard",Difference(hardComplete,hardMissingWhite),Difference(hardComplete,hardMissingWhite)==0);
                        }
                    }
                }
                iris.SetActive(false);liner.SetActive(false);helper.downsample=2;
                camera.orthographic=oldOrtho;camera.rect=oldRect;
                eye.transform.position=oldPosition;eye.transform.localScale=oldScale;eye.GetComponent<Renderer>().sharedMaterial=eyeMat;
                helper.sources=new[]{new TomHairFeatherCamera.Source{renderer=eye.GetComponent<Renderer>()}};
                mat.SetFloat("_HairFrontMode",2);
            }
            var skinObject=new GameObject("Moving skinned eye writer");skinObject.layer=layer;
            skinObject.transform.position=eye.transform.position;skinObject.transform.localScale=eye.transform.localScale;
            var skin=skinObject.AddComponent<SkinnedMeshRenderer>();
            var bone=new GameObject("Eye bone").transform;bone.SetParent(skinObject.transform,false);
            Mesh skinMesh=Object.Instantiate(eye.GetComponent<MeshFilter>().sharedMesh);garbage.Add(skinMesh);
            var weights=new BoneWeight[skinMesh.vertexCount];for(int n=0;n<weights.Length;n++)weights[n]=new BoneWeight{boneIndex0=0,weight0=1};
            skinMesh.boneWeights=weights;skinMesh.bindposes=new[]{Matrix4x4.identity};skin.sharedMesh=skinMesh;
            skin.bones=new[]{bone};skin.rootBone=bone;skin.sharedMaterial=eyeMat;skin.updateWhenOffscreen=true;
            eye.SetActive(false);helper.sources=new[]{new TomHairFeatherCamera.Source{renderer=skin}};animatedBone=bone;
            foreach(float shift in new[]{-.2f,.2f}){desiredBonePosition=new Vector3(shift,.1f,0);rectangle("world-skinned-moving-"+shift);}
            animatedBone=null;skinObject.SetActive(false);eye.SetActive(true);
            helper.sources=new[]{new TomHairFeatherCamera.Source{renderer=eye.GetComponent<Renderer>()}};
            camera.rect=new Rect(0,0,1,1);
            foreach(float z in new[]{1.5f,3f,6f}){camera.transform.position=new Vector3(0,0,-z);rectangle("world-depth-"+z);}
            camera.transform.position=new Vector3(0,0,-3);
            foreach(float fov in new[]{20f,40f,60f}){camera.fieldOfView=fov;rectangle("world-fov-"+fov);}
            camera.fieldOfView=28;foreach(int n in new[]{256,768}){resize(n);rectangle("world-resolution-"+n);}
            resize(512);camera.orthographic=true;
            foreach(float scale in new[]{.4f,.75f,1.5f}){camera.orthographicSize=scale;rectangle("world-ortho-"+scale);}
            camera.orthographicSize=.75f;mat.SetFloat("_HairFeatherWorldWidth",2);rectangle("world-budget-cap");
            check("Search budget capped",helper.SearchRadiusPixels,helper.SearchRadiusPixels==256&&helper.WidthBudgetClamped);
            mat.SetFloat("_HairFeatherWorldWidth",.05f);mat.SetFloat("_HairFeatherWidth",0);rectangle("world-independent-of-pixel-width");
            mat.SetFloat("_HairFeatherWorldWidth",0);mat.SetFloat("_HairFrontMode",1);Color[] zeroHard=capture("zero-world-hard");
            mat.SetFloat("_HairFrontMode",2);Color[] zeroField=capture("zero-world-feather");check("Zero world width Hard",Difference(zeroHard,zeroField),Difference(zeroHard,zeroField)<.00001&&!helper.Active);
            mat.SetFloat("_HairFeatherWorldWidth",.05f);
            float equivalent=.05f*.5f*camera.pixelHeight*Mathf.Abs(camera.projectionMatrix.m11);mat.SetFloat("_HairFeatherWidth",equivalent);
            mat.SetFloat("_DebugView",0);mat.SetFloat("_EmissionIntensity",0);mat.SetFloat("_UseRamp",0);
            mat.SetFloat("_HairFeatherThreshold",.25f);mat.SetFloat("_HairFeatherPower",2);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.cullingMask=1<<layer;sun.renderMode=LightRenderMode.ForcePixel;sun.transform.rotation=Quaternion.Euler(15,-20,0);
            var point=new GameObject("Additional point").AddComponent<Light>();point.type=LightType.Point;point.range=5;point.intensity=2;point.cullingMask=1<<layer;point.renderMode=LightRenderMode.ForcePixel;point.transform.position=new Vector3(.5f,.2f,-1);
            foreach(Rect rect in new[]{new Rect(0,0,1,1),new Rect(.33f,0,1,1)}){
                camera.rect=rect;mat.SetFloat("_HairFeatherWidthMode",0);Color[] pixel=capture("lit-pixels-"+rect.x);
                mat.SetFloat("_HairFeatherWidthMode",1);Color[] world=capture("lit-world-"+rect.x);
                check("Base/Add equivalent units viewport "+rect.x,Difference(pixel,world),Difference(pixel,world)<.00001);
                point.enabled=false;Color[] baseOnly=capture("lit-base-"+rect.x);point.enabled=true;
                check("Additional light fixture contributes "+rect.x,Difference(world,baseOnly),Difference(world,baseOnly)>.0001);
            }
            hair.SetActive(false);
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.layer=layer;sphere.GetComponent<Renderer>().sharedMaterial=mat;
            helper.receivers=new[]{sphere.GetComponent<Renderer>()};eye.transform.position=new Vector3(.4f,0,1);eye.transform.localScale=new Vector3(.5f,1.2f,1);
            mat.SetFloat("_OutlineOn",1);mat.SetFloat("_OutlineWidth",5);mat.SetColor("_OutlineColor",new Color(.1f,.4f,.9f,1));mat.SetFloat("_CullOption",0);
            foreach(Rect rect in new[]{new Rect(0,0,1,1),new Rect(.33f,0,1,1)}){
                camera.rect=rect;mat.SetFloat("_HairFeatherWidthMode",0);Color[] pixel=capture("outline-pixels-"+rect.x);
                mat.SetFloat("_HairFeatherWidthMode",1);Color[] world=capture("outline-world-"+rect.x);
                check("Outline equivalent units viewport "+rect.x,Difference(pixel,world),Difference(pixel,world)<.00001);
                mat.SetFloat("_OutlineOn",0);Color[] noOutline=capture("outline-disabled-"+rect.x);mat.SetFloat("_OutlineOn",1);
                check("Outline fixture contributes "+rect.x,Difference(world,noOutline),Difference(world,noOutline)>.0001);
            }
            compiled+="\n"+TomXStageTwoValidation.Validate("tom/HairX");
        }
        finally
        {
            RenderTexture.active=previousRT;EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
            foreach(Object o in garbage)if(o)Object.DestroyImmediate(o);
            foreach(var pair in masks)if(pair.Key)pair.Key.cullingMask=pair.Value;
            QualitySettings.pixelLightCount=oldLights;
            File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(folder,"compilation.txt"),compiled);
        }
        int failed=report.checks.FindAll(c=>!c.passed).Count;
        if(failed>0)throw new Exception(failed+" width checks failed; "+folder);
        return report.checks.Count+" viewport/width/overlap checks passed; "+report.captures.Count+" captures (including field diagnostics). "+folder;
    }
}

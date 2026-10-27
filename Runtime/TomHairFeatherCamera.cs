using System;
using System.Collections.Generic;
using TomRenderInfrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomShadersX
{
    // A binary eye-mask proxy, not a copy of camera colour or a soft stencil buffer.
    // Replays registered writer passes before opaque hair, then computes inward distance.
    [ExecuteInEditMode, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class TomHairFeatherCamera : MonoBehaviour
    {
        [Serializable] public sealed class Source
        {
            public Renderer renderer;
            public int materialIndex;
            public string passName = "StencilMask";
        }
        public Source[] sources = new Source[0];
        public Renderer[] receivers = new Renderer[0];
        public Shader fieldShader;
        public int downsample = 2;
        public int SourceDraws { get; private set; }
        public int FilterPasses { get; private set; }
        public string Status { get; private set; }
        public int VisibleSources { get; private set; }
        public int LateSources { get; private set; }
        public int MissingPassSources { get; private set; }
        public int EarliestHairQueue { get; private set; }
        public int SearchRadiusPixels { get; private set; }
        public bool WidthBudgetClamped { get; private set; }
        public bool Active { get { return commands != null; } }
        public RenderTexture DistanceTexture { get { return distance; } }
        public RenderTexture MaskTexture { get { return mask; } }
        Camera cameraComponent;
        CommandBuffer commands, publish, cleanup;
        bool commandsAttached;
        Material fieldMaterial;
        Mesh fullscreen;
        RenderTexture mask, ping, pong, distance;
        readonly List<RenderResourceLease> resources = new List<RenderResourceLease>();
        IDisposable cameraRegistration;
        long preparedInvocation;
        readonly List<Material> materials = new List<Material>();
        static readonly int Params = Shader.PropertyToID("_TomHairFeatherParams");
        static readonly int Distance = Shader.PropertyToID("_TomHairFeatherDistance");

        void OnEnable()
        {
            cameraComponent=GetComponent<Camera>();
            FeatherCameraScopes.Acquire();
            cameraRegistration=UnityCameraHub.Register(this,"Tom HairFront feather",400,
                context=>{if(UnityCameraHub.Camera(context)==cameraComponent)Prepare();},null,
                (context,aborted)=>{if(aborted && UnityCameraHub.Camera(context)==cameraComponent)Release();});
        }

        public void Prepare()
        {
            cameraComponent = GetComponent<Camera>();
            var context=UnityCameraHub.Coordinator.Find(cameraComponent);
            if(CameraRoles.IsAuxiliaryCapture(cameraComponent)){Status="Hard: auxiliary capture";Release();return;}
            if(context!=null && preparedInvocation==context.Id)return;
            if(context!=null)preparedInvocation=context.Id;
            foreach(var resource in resources)resource.Bind(context);
            SourceDraws = FilterPasses = 0;
            VisibleSources = LateSources = MissingPassSources = 0;
            EarliestHairQueue = int.MaxValue;
            SearchRadiusPixels = 32;
            WidthBudgetClamped = false;
            float requestedRadius = 32f;
            var planes = GeometryUtility.CalculateFrustumPlanes(cameraComponent);
            bool needed = false;
            int earliestHairQueue = int.MaxValue;
            foreach (Renderer renderer in receivers)
            {
                if (!Visible(renderer, planes)) continue;
                materials.Clear(); renderer.GetSharedMaterials(materials);
                foreach (Material material in materials)
                {
                    if (!material || !material.shader || material.shader.name != "tom/HairX"
                        || material.GetFloat("_HairFrontMode") <= 1.5f) continue;
                    bool world = material.HasProperty("_HairFeatherWidthMode") && material.GetFloat("_HairFeatherWidthMode") > .5f;
                    float requestedWidth = material.GetFloat(world ? "_HairFeatherWorldWidth" : "_HairFeatherWidth");
                    if (requestedWidth <= 0f) continue;
                    needed = true; earliestHairQueue = Mathf.Min(earliestHairQueue, material.renderQueue);
                    if (world)
                    {
                        // Conservative nearest depth of this renderer, so per-fragment widths fit the field.
                        Bounds bounds = renderer.bounds;
                        Matrix4x4 view = cameraComponent.worldToCameraMatrix;
                        Vector3 e = bounds.extents;
                        float nearest = -view.MultiplyPoint(bounds.center).z
                            - Mathf.Abs(view.m20)*e.x - Mathf.Abs(view.m21)*e.y - Mathf.Abs(view.m22)*e.z;
                        float depth = cameraComponent.orthographic ? 1f : Mathf.Max(cameraComponent.nearClipPlane, nearest);
                        requestedWidth *= .5f * cameraComponent.pixelHeight * Mathf.Abs(cameraComponent.projectionMatrix.m11) / Mathf.Max(depth,.00001f);
                        requestedRadius = Mathf.Max(requestedRadius,requestedWidth);
                    }
                }
            }
            EarliestHairQueue = earliestHairQueue;
            if (!needed) { Status = "Hard: no visible HairX with Mode=2 and Width>0"; Release(); return; }
            if (sources.Length == 0) { Status = "Hard: no registered eye writers"; Release(); return; }
            if (cameraComponent.stereoEnabled) { Status = "Hard: stereo camera unsupported"; Release(); return; }
            if (cameraComponent.actualRenderingPath != RenderingPath.Forward)
            { Status = "Hard: camera is not Forward"; Release(); return; }
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
            { Status = "Hard: graphics API is not Direct3D11"; Release(); return; }
            if (!fieldShader) fieldShader = Shader.Find("Hidden/TomX/HairFeather");
            if (!fieldShader) { Status = "Hard: hidden shader unavailable"; Release(); return; }
            if (!fieldShader.isSupported) { Status = "Hard: hidden shader unsupported"; Release(); return; }
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat)
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
            { Status = "Hard: required render texture format unsupported"; Release(); return; }

            int screenWidth = cameraComponent.pixelWidth, screenHeight = cameraComponent.pixelHeight;
            if (screenWidth <= 0 || screenHeight <= 0) { Status = "Hard: empty camera viewport"; Release(); return; }
            WidthBudgetClamped = requestedRadius > 256f;
            SearchRadiusPixels = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Clamp(requestedRadius,32f,256f)));
            int scale = Mathf.Clamp(downsample, 1, 4);
            int width = Mathf.Max(1, (screenWidth + scale - 1) / scale);
            int height = Mathf.Max(1, (screenHeight + scale - 1) / scale);
            if (!mask || !mask.IsCreated() || !ping || !ping.IsCreated() || !pong || !pong.IsCreated() || !distance || !distance.IsCreated()
                || mask.width != width || mask.height != height)
            {
                Release();
                try { Allocate(width,height); }
                catch(Exception error){Status="Hard: "+error.Message;Release();return;}
            }
            if (!fieldMaterial) fieldMaterial = new Material(fieldShader) { hideFlags = HideFlags.HideAndDontSave };
            if (!fullscreen)
            {
                fullscreen = new Mesh { name = "Tom hair mask fullscreen", hideFlags = HideFlags.HideAndDontSave };
                fullscreen.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
                fullscreen.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                fullscreen.triangles = new[] {0,1,2,0,2,3};
            }
            if (commands == null)
            {
                commands = new CommandBuffer { name = "Tom HairFront feather (binary proxy)" };
                publish = new CommandBuffer { name = "Tom HairFront publish camera field" };
                cleanup = new CommandBuffer { name = "Tom HairFront reset camera globals" };
                cleanup.SetGlobalVector(Params, Vector4.zero);
                cameraComponent.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, publish);
                cameraComponent.AddCommandBuffer(CameraEvent.AfterEverything, cleanup);
            }
            bool partial = cameraComponent.rect != new Rect(0,0,1,1);
            if (partial && commandsAttached)
            { cameraComponent.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commands); commandsAttached = false; }
            else if (!partial && !commandsAttached)
            { cameraComponent.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commands); commandsAttached = true; }
            commands.Clear();
            commands.SetGlobalVector(Params, Vector4.zero);
            commands.SetRenderTarget(mask);
            commands.SetViewport(new Rect(0,0,width,height));
            commands.ClearRenderTarget(true, true, Color.clear);
            commands.SetViewProjectionMatrices(cameraComponent.worldToCameraMatrix, cameraComponent.projectionMatrix);
            if (partial) commands.SetGlobalVector("_WorldSpaceCameraPos", cameraComponent.transform.position);
            foreach (Source source in sources)
            {
                if (source == null || !Visible(source.renderer, planes)) continue;
                VisibleSources++;
                materials.Clear(); source.renderer.GetSharedMaterials(materials);
                if (source.materialIndex < 0 || source.materialIndex >= materials.Count) continue;
                Material material = materials[source.materialIndex];
                // A later writer cannot affect hair's actual stencil. Do not invent that coverage.
                if (!material) continue;
                if (material.renderQueue >= earliestHairQueue) { LateSources++; continue; }
                int pass = material.FindPass(source.passName);
                if (pass < 0) { MissingPassSources++; continue; }
                commands.DrawRenderer(source.renderer, material, source.materialIndex, pass);
                SourceDraws++;
            }
            if (SourceDraws == 0) { Status = "Hard: no eligible eye writer draws"; Release(); return; }
            // A known missing pass makes the union incomplete even if other writers can draw.
            if (MissingPassSources > 0) { Status = "Hard: registered eye writer pass missing"; Release(); return; }
            commands.ClearRenderTarget(false, true, Color.black);
            commands.DrawMesh(fullscreen, Matrix4x4.identity, fieldMaterial, 0, 0);
            commands.SetGlobalVector("_TomHairFieldSize", new Vector4(width, height, screenWidth, screenHeight));
            commands.SetGlobalFloat("_TomHairMaxDistance", SearchRadiusPixels);
            Filter(mask, ping, 1);
            int start = Mathf.NextPowerOfTwo(Mathf.CeilToInt((float)SearchRadiusPixels / scale));
            RenderTexture current = ping, next = pong;
            for (int jump = start; jump >= 1; jump /= 2)
            {
                commands.SetGlobalFloat("_TomHairJump", jump);
                Filter(current, next, 2);
                var swap = current; current = next; next = swap;
            }
            commands.SetGlobalTexture("_TomHairFieldMask", mask);
            Filter(current, distance, 3);
            Publish(commands, screenWidth, screenHeight, partial);
            publish.Clear();
            Publish(publish, screenWidth, screenHeight, partial);
            if (partial)
            {
                // Switching RTs inside BeforeForwardOpaque resets Unity's partial viewport.
                // Build the private field before native camera setup; the later buffer only publishes globals.
                Graphics.ExecuteCommandBuffer(commands);
            }
            else
            {
                commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                commands.SetViewProjectionMatrices(cameraComponent.worldToCameraMatrix, cameraComponent.projectionMatrix);
            }
            Status = "Scheduled: proxy and distance commands (not GPU/pixel confirmation)";
        }

        void Publish(CommandBuffer buffer, int width, int height, bool partial)
        {
            buffer.SetGlobalTexture(Distance, distance);
            buffer.SetGlobalMatrix("_TomHairFeatherView", cameraComponent.worldToCameraMatrix);
            buffer.SetGlobalVector(Params, new Vector4(1,width,height,SearchRadiusPixels));
            buffer.SetGlobalVector("_TomHairFeatherLayout", new Vector4(partial ? 1 : 0,2,0,0));
        }

        void Filter(RenderTexture input, RenderTexture output, int pass)
        {
            // Explicit clip-space quad: no inherited camera matrix or Blit UV convention.
            commands.SetGlobalTexture("_TomHairFieldInput", input);
            commands.SetRenderTarget(output);
            commands.SetViewport(new Rect(0,0,output.width,output.height));
            commands.DrawMesh(fullscreen, Matrix4x4.identity, fieldMaterial, 0, pass);
            FilterPasses++;
        }

        bool Visible(Renderer renderer, Plane[] planes)
        {
            return renderer && renderer.enabled && renderer.gameObject.activeInHierarchy
                && (cameraComponent.cullingMask & (1 << renderer.gameObject.layer)) != 0
                && GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
        }

        void Allocate(int width, int height)
        {
            mask = Make(width, height, 24, RenderTextureFormat.ARGB32, "Tom hair binary mask", FilterMode.Point);
            ping = Make(width, height, 0, RenderTextureFormat.RGFloat, "Tom hair seed A", FilterMode.Point);
            pong = Make(width, height, 0, RenderTextureFormat.RGFloat, "Tom hair seed B", FilterMode.Point);
            distance = Make(width, height, 0, RenderTextureFormat.RHalf, "Tom hair distance", FilterMode.Bilinear);
        }

        RenderTexture Make(int width, int height, int depth, RenderTextureFormat format, string name, FilterMode filter)
        {
            var lease=RenderResourceLease.Create(this,new RenderResourceDescriptor{width=width,height=height,depth=depth,format=format,
                linear=true,filter=filter,purpose=name,stage="BeforeForwardOpaque/partial-immediate"});
            resources.Add(lease);return lease.Texture;
        }

        void OnDisable()
        {
            if(cameraRegistration!=null){cameraRegistration.Dispose();cameraRegistration=null;FeatherCameraScopes.Release();}
            Release();DestroyResource(fieldMaterial);DestroyResource(fullscreen);fieldMaterial=null;fullscreen=null;
        }
        void OnDestroy() { OnDisable(); }
        void Release()
        {
            if (cameraComponent && commands != null) cameraComponent.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commands);
            if (cameraComponent && publish != null) cameraComponent.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, publish);
            if (cameraComponent && cleanup != null) cameraComponent.RemoveCommandBuffer(CameraEvent.AfterEverything, cleanup);
            if (commands != null) commands.Release(); if (publish != null) publish.Release(); if (cleanup != null) cleanup.Release();
            commands = publish = cleanup = null; commandsAttached = false;
            foreach(var resource in resources)resource.Dispose();resources.Clear();
            mask = ping = pong = distance = null;
            if(UnityCameraHub.Coordinator.ActiveDepth==0)Shader.SetGlobalVector(Params, Vector4.zero);
        }
        static void DestroyResource(UnityEngine.Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }
    }

    // One global guard, including cameras without a helper. Child renders must
    // not sample a parent's field, and child cleanup must restore its parent.
    static class FeatherCameraScopes
    {
        sealed class State { internal long invocation;internal Vector4 parameters,layout;internal Matrix4x4 view;internal Texture distance; }
        static readonly List<State> states=new List<State>();
        static IDisposable registration;
        static int owners;
        static readonly int Parameters=Shader.PropertyToID("_TomHairFeatherParams");
        static readonly int Layout=Shader.PropertyToID("_TomHairFeatherLayout");
        static readonly int View=Shader.PropertyToID("_TomHairFeatherView");
        static readonly int Distance=Shader.PropertyToID("_TomHairFeatherDistance");
        internal static void Acquire()
        {
            if(owners++!=0)return;
            registration=UnityCameraHub.Register(typeof(FeatherCameraScopes),"Tom Feather global scope",450,null,
                context=>{states.Add(new State{invocation=context.Id,parameters=Shader.GetGlobalVector(Parameters),layout=Shader.GetGlobalVector(Layout),view=Shader.GetGlobalMatrix(View),distance=Shader.GetGlobalTexture(Distance)});Shader.SetGlobalVector(Parameters,Vector4.zero);},
                (context,aborted)=>{
                    if(states.Count==0 || states[states.Count-1].invocation!=context.Id)return;
                    var state=states[states.Count-1];states.RemoveAt(states.Count-1);
                    Shader.SetGlobalVector(Parameters,state.parameters);Shader.SetGlobalVector(Layout,state.layout);
                    Shader.SetGlobalMatrix(View,state.view);Shader.SetGlobalTexture(Distance,state.distance);
                });
        }
        internal static void Release()
        {
            if(--owners!=0)return;registration.Dispose();registration=null;states.Clear();Shader.SetGlobalVector(Parameters,Vector4.zero);
        }
    }
}

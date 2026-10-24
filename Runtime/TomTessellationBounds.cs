using System.Collections.Generic;
using UnityEngine;

namespace TomShadersX
{
    [ExecuteInEditMode, DisallowMultipleComponent, DefaultExecutionOrder(10010)]
    public sealed class TomTessellationBounds : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        Renderer target;
        SkinnedMeshRenderer skin;
        MeshFilter filter;
        Mesh original, owned;
        Bounds baseline, applied;
        bool hasBounds;

        public static bool IsTessellationMaterial(Material m)
        {
            return m && m.shader && (m.shader.name == "tom/MainOpaqueXTess" || m.shader.name == "tom/SkinXTess"
                || m.shader.name == "tom/MainAlphaXTess");
        }

        static float Finite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;
        }

        static float Strength(Material m, string key)
        {
            return m.HasProperty(key) ? Finite(m.GetFloat(key)) : 0;
        }

        public static bool NeedsBounds(Material m)
        {
            return IsTessellationMaterial(m) && (Strength(m, "_TessellationSmooth") > 0
                || Strength(m, "_DisplacementStrength") != 0 || Strength(m, "_TessellationBoundsPadding") > 0);
        }

        void OnEnable() { ApplyBounds(); }
        void LateUpdate() { ApplyBounds(); }

        public void ApplyBounds()
        {
            if (!target)
            {
                target = GetComponent<Renderer>(); skin = target as SkinnedMeshRenderer;
                filter = GetComponent<MeshFilter>();
            }
            if (!target || (!skin && !filter)) return;
            materials.Clear(); target.GetSharedMaterials(materials);
            float smoothing = 0, worldPadding = 0;
            foreach (var m in materials)
            {
                if (!IsTessellationMaterial(m)) continue;
                smoothing = Mathf.Max(smoothing, Mathf.Clamp01(Strength(m, "_TessellationSmooth")));
                float mid = Mathf.Clamp01(Strength(m, "_DisplacementMidlevel"));
                float displacement = Mathf.Min(Mathf.Abs(Strength(m, "_DisplacementStrength")), .05f) * Mathf.Max(mid, 1 - mid);
                worldPadding = Mathf.Max(worldPadding, displacement + Mathf.Clamp(Strength(m, "_TessellationBoundsPadding"), 0, .1f));
            }
            if (smoothing <= 0 && worldPadding <= 0) { RestoreBounds(); return; }
            if (skin)
            {
                if (!hasBounds || skin.localBounds != applied) baseline = skin.localBounds;
            }
            else
            {
                if (owned && filter.sharedMesh != owned) ReleaseMesh(false);
                if (!owned)
                {
                    original = filter.sharedMesh;
                    if (!original) return;
                    baseline = original.bounds;
                    owned = Instantiate(original); owned.name = original.name + " [Tom Tess Bounds]";
                    owned.hideFlags = HideFlags.DontSave;
                    filter.sharedMesh = owned;
                }
                else baseline = original.bounds;
            }
            // Any Phong projection moves at most the original AABB diagonal in local space.
            float smoothPadding = baseline.size.magnitude * smoothing;
            Matrix4x4 inverse = transform.worldToLocalMatrix;
            var localPadding = new Vector3(
                new Vector3(inverse.m00, inverse.m01, inverse.m02).magnitude,
                new Vector3(inverse.m10, inverse.m11, inverse.m12).magnitude,
                new Vector3(inverse.m20, inverse.m21, inverse.m22).magnitude) * worldPadding;
            localPadding += Vector3.one * smoothPadding;
            applied = baseline; applied.Expand(localPadding * 2);
            if (skin) skin.localBounds = applied;
            else owned.bounds = applied;
            hasBounds = true;
        }

        void ReleaseMesh(bool restore)
        {
            if (restore && filter && owned && filter.sharedMesh == owned) filter.sharedMesh = original;
            if (owned)
            {
                if (Application.isPlaying) Destroy(owned);
                else DestroyImmediate(owned);
            }
            owned = null; original = null; hasBounds = false;
        }

        public void RestoreBounds()
        {
            if (skin && hasBounds && skin.localBounds == applied) skin.localBounds = baseline;
            ReleaseMesh(true); hasBounds = false;
        }
        void OnDisable() { RestoreBounds(); }
        void OnDestroy() { RestoreBounds(); }
    }
}

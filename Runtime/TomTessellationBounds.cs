using System.Collections.Generic;
using TomRenderInfrastructure;
using UnityEngine;

namespace TomShadersX
{
    [ExecuteInEditMode, DisallowMultipleComponent, DefaultExecutionOrder(10010)]
    public sealed class TomTessellationBounds : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        Renderer target;
        RendererBoundsBroker.Lease boundsLease;

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
                target = GetComponent<Renderer>();
            }
            if (!target) return;
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
            if (boundsLease == null) boundsLease = RendererBoundsBroker.Acquire(target);
            if (boundsLease != null) boundsLease.Set(worldPadding, smoothing);
        }

        public void RestoreBounds()
        {
            if (boundsLease != null) { boundsLease.Dispose(); boundsLease = null; }
        }
        void OnDisable() { RestoreBounds(); }
        void OnDestroy() { RestoreBounds(); }
    }
}

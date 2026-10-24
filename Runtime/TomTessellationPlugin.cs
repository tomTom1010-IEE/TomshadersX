#if TOM_X_BEPINEX
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace TomShadersX
{
    [BepInPlugin("tom.shaders.x.tessbounds", "Tom X Tessellation Bounds", "0.1.1")]
    public sealed class TomTessellationPlugin : BaseUnityPlugin
    {
        readonly Dictionary<Renderer, TomTessellationBounds> tracked = new Dictionary<Renderer, TomTessellationBounds>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Renderer> stale = new List<Renderer>();
        float nextScan;

        bool Needed(Renderer renderer)
        {
            if (!renderer) return false;
            materials.Clear(); renderer.GetSharedMaterials(materials);
            foreach (var material in materials) if (TomTessellationBounds.NeedsBounds(material)) return true;
            return false;
        }

        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1;
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !Needed(renderer)
                    || tracked.ContainsKey(renderer) || renderer.GetComponent<TomTessellationBounds>()) continue;
                if (!(renderer is SkinnedMeshRenderer) && !renderer.GetComponent<MeshFilter>()) continue;
                tracked.Add(renderer, renderer.gameObject.AddComponent<TomTessellationBounds>());
            }
            stale.Clear();
            foreach (var entry in tracked) if (!entry.Value || !Needed(entry.Key)) stale.Add(entry.Key);
            foreach (var renderer in stale)
            {
                var helper = tracked[renderer];
                if (helper) { helper.enabled = false; Destroy(helper); }
                tracked.Remove(renderer);
            }
        }

        void OnDisable()
        {
            foreach (var helper in tracked.Values)
                if (helper) { helper.enabled = false; Destroy(helper); }
            tracked.Clear();
        }
        void OnDestroy() { OnDisable(); }
    }
}
#endif

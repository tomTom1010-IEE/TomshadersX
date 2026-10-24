#if TOM_X_BEPINEX
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace TomShadersX
{
    [BepInPlugin("tom.shaders.x.facesdf", "Tom SkinX Face SDF", "0.1.1")]
    public sealed class TomSkinSDFPlugin : BaseUnityPlugin
    {
        readonly Dictionary<Renderer, TomSkinSDFHead> tracked = new Dictionary<Renderer, TomSkinSDFHead>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Renderer> stale = new List<Renderer>();
        float nextScan;

        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                materials.Clear(); renderer.GetSharedMaterials(materials);
                bool needed = false;
                foreach (var material in materials) needed |= TomSkinSDFHead.UsesTrackedSDF(material);
                if (!needed || tracked.ContainsKey(renderer) || renderer.GetComponent<TomSkinSDFHead>()) continue;
                var head = TomSkinSDFHead.FindHead(renderer);
                if (!head) continue;
                var helper = renderer.gameObject.AddComponent<TomSkinSDFHead>();
                helper.head = head; helper.ApplyFrame(); tracked.Add(renderer, helper);
            }
            stale.Clear();
            foreach (var entry in tracked)
            {
                bool needed = false;
                if (entry.Key && entry.Value)
                {
                    materials.Clear(); entry.Key.GetSharedMaterials(materials);
                    foreach (var material in materials) needed |= TomSkinSDFHead.UsesTrackedSDF(material);
                }
                if (!needed) stale.Add(entry.Key);
            }
            foreach (var renderer in stale)
            {
                var helper = tracked[renderer];
                if (helper) { helper.enabled = false; Destroy(helper); }
                tracked.Remove(renderer);
            }
        }

        void OnDestroy()
        {
            foreach (var helper in tracked.Values)
                if (helper) { helper.enabled = false; Destroy(helper); }
            tracked.Clear();
        }
    }
}
#endif

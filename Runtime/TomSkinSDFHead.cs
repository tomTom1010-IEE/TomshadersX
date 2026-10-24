using System.Collections.Generic;
using UnityEngine;

namespace TomShadersX
{
    [ExecuteInEditMode, DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class TomSkinSDFHead : MonoBehaviour
    {
        public Transform head;
        static readonly int HeadMatrix = Shader.PropertyToID("_TomFaceSDFHeadToWorld");
        static readonly int HeadValid = Shader.PropertyToID("_TomFaceSDFHeadValid");
        readonly List<Material> materials = new List<Material>();
        MaterialPropertyBlock block;
        Renderer target;

        public static bool UsesTrackedSDF(Material material)
        {
            return material && material.shader && (material.shader.name == "tom/SkinX" || material.shader.name == "tom/SkinXTess")
                && material.HasProperty("_UseFaceSDF") && material.GetFloat("_UseFaceSDF") > .5f
                && material.GetFloat("_FaceSDFDirectionSource") > .5f;
        }

        static Transform NamedHead(Transform bone)
        {
            for (Transform t = bone; t; t = t.parent)
                if (t.name == "cf_J_Head" || t.name == "cm_J_Head"
                    || t.name == "Head" || t.name == "mixamorig:Head") return t;
            return null;
        }

        public static Transform FindHead(Renderer renderer)
        {
            var animator = renderer.GetComponentInParent<Animator>();
            if (animator && animator.isHuman)
            {
                var bone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone) return bone;
            }
            var skin = renderer as SkinnedMeshRenderer;
            if (skin)
            {
                var bone = NamedHead(skin.rootBone);
                if (bone) return bone;
                foreach (var candidate in skin.bones)
                {
                    bone = NamedHead(candidate);
                    if (bone) return bone;
                }
            }
            return NamedHead(renderer.transform);
        }

        void Awake() { target = GetComponent<Renderer>(); }
        void LateUpdate() { ApplyFrame(); }
        void OnWillRenderObject() { ApplyFrame(); }

        public void ApplyFrame()
        {
            if (!target) target = GetComponent<Renderer>();
            if (!target) return;
            if (block == null) block = new MaterialPropertyBlock();
            if (!head) head = FindHead(target);
            materials.Clear(); target.GetSharedMaterials(materials);
            bool active = false;
            foreach (var m in materials) active |= UsesTrackedSDF(m);
            float valid = active && head ? 1f : 0f;
            Matrix4x4 frame = head ? Matrix4x4.Rotate(head.rotation) : Matrix4x4.identity;
            target.GetPropertyBlock(block);
            block.SetFloat(HeadValid, valid); block.SetMatrix(HeadMatrix, frame);
            target.SetPropertyBlock(block);
            // Slot blocks override the renderer block. Preserve other providers' values.
            for (int i = 0; i < materials.Count; i++)
            {
                target.GetPropertyBlock(block, i);
                if (block.isEmpty) continue;
                block.SetFloat(HeadValid, valid); block.SetMatrix(HeadMatrix, frame);
                target.SetPropertyBlock(block, i);
            }
        }

        void OnDisable()
        {
            if (!target || block == null) return;
            target.GetPropertyBlock(block); block.SetFloat(HeadValid, 0f); target.SetPropertyBlock(block);
            materials.Clear(); target.GetSharedMaterials(materials);
            for (int i = 0; i < materials.Count; i++)
            {
                target.GetPropertyBlock(block, i);
                if (block.isEmpty) continue;
                block.SetFloat(HeadValid, 0f); target.SetPropertyBlock(block, i);
            }
        }
    }
}

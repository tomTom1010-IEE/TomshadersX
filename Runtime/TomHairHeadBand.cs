using UnityEngine;

namespace TomShadersX
{
    public enum TomHairHighlightDebug
    {
        Off = 0, Primary = 1, Secondary = 2, Direction = 3, Shift = 4,
        Coverage = 5, Normal = 6
    }

    // Inert compatibility component for existing scenes; the provider is archived.
    [ExecuteInEditMode, DisallowMultipleComponent]
    public sealed class TomHairHeadBand : MonoBehaviour
    {
        public Transform head;
        public static bool UsesTrackedBand(Material material) { return false; }
        public static Transform FindHead(Renderer renderer) { return null; }
        public void ApplyFrame() { }
        void OnEnable() { enabled = false; }
    }
}

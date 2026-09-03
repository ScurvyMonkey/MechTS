using MechTS.Units;
using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// A dimmed, static filled-disc marker showing where an enemy building was last seen
    /// while its <see cref="Vision.FogState"/> is <see cref="Vision.FogState.Explored"/> — a
    /// simplified stand-in for a full remembered sprite snapshot (this project's fog spec
    /// only requires "a dimmed static marker," not a live-updating copy of the building's
    /// actual appearance). Reuses <see cref="RingMesh"/> with a zero inner radius, which
    /// collapses cleanly into a filled disc. Uses a solid dimmed color rather than alpha
    /// transparency, matching <see cref="Units.UnitBase"/>'s existing ring-dimming approach
    /// (<c>baseColor * dimBrightness</c>) rather than introducing a transparent-material
    /// setup that hasn't been proven in this project's URP/unity-mcp environment.
    /// </summary>
    public static class FogGhostMarker
    {
        private static readonly Vector3 LocalOffset = new Vector3(0f, 0.015f, 0f);
        private const float DimBrightness = 0.35f;
        private const float Radius = 1.5f;

        private static Material _sharedMaterial;

        /// <summary>
        /// Creates a new ghost marker at the given world position, tinted with a dimmed
        /// version of the owning faction's color.
        /// </summary>
        /// <param name="worldPosition">The building's last-known world position.</param>
        /// <param name="faction">The building's owning faction.</param>
        public static GameObject Create(Vector3 worldPosition, Faction faction)
        {
            var marker = new GameObject("FogGhostMarker");
            marker.transform.position = worldPosition + LocalOffset;

            marker.AddComponent<MeshFilter>().mesh = RingMesh.Create(0f, Radius);

            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.material = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor("_BaseColor", FactionColor.GetColor(faction) * DimBrightness);
            renderer.SetPropertyBlock(propertyBlock);

            return marker;
        }

        /// <summary>
        /// Repositions an existing ghost marker to a new last-known position.
        /// </summary>
        /// <param name="marker">The marker GameObject to reposition.</param>
        /// <param name="worldPosition">The new last-known world position.</param>
        public static void SetPosition(GameObject marker, Vector3 worldPosition)
        {
            marker.transform.position = worldPosition + LocalOffset;
        }

        /// <summary>
        /// Returns the shared unlit material used by every ghost marker, creating it once.
        /// </summary>
        private static Material GetMaterial()
        {
            if (_sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _sharedMaterial = new Material(shader) { name = "FogGhostMarkerMaterial" };
                _sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            return _sharedMaterial;
        }
    }
}

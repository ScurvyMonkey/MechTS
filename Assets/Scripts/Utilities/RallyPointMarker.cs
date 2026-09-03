using MechTS.Units;
using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// A small always-visible ring marking a production building's rally point, in the
    /// building's faction color (see <see cref="MechTS.Economy.BuildingInstance.RallyPoint"/>).
    /// Unlike <see cref="SelectionIndicator"/> (one shared instance, shown only while
    /// selected), each building with an active rally point gets its own marker instance,
    /// since several can be set simultaneously across different buildings/factions.
    /// </summary>
    public static class RallyPointMarker
    {
        private static readonly Vector3 LocalOffset = new Vector3(0f, 0.02f, 0f);
        private const float InnerRadius = 0.4f;
        private const float OuterRadius = 0.6f;

        private static Material _sharedMaterial;

        /// <summary>
        /// Creates a new rally point marker at the given world position, tinted for the
        /// given faction via a per-instance <see cref="MaterialPropertyBlock"/> (the
        /// underlying material is shared across every marker, so per-instance color can't
        /// be set directly on it — mirrors <see cref="Units.UnitBase"/>'s ring-coloring pattern).
        /// </summary>
        /// <param name="worldPosition">The rally point's world position.</param>
        /// <param name="faction">The owning building's faction, for marker color.</param>
        public static GameObject Create(Vector3 worldPosition, Faction faction)
        {
            var marker = new GameObject("RallyPointMarker");
            marker.AddComponent<MeshFilter>().mesh = RingMesh.Create(InnerRadius, OuterRadius);

            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.material = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", FactionColor.GetColor(faction));
            renderer.SetPropertyBlock(block);

            SetPosition(marker, worldPosition);
            return marker;
        }

        /// <summary>
        /// Moves an existing marker to a new world position, reapplying the small
        /// above-ground Y offset every flat marker in this project needs.
        /// </summary>
        /// <param name="marker">The marker GameObject to move.</param>
        /// <param name="worldPosition">The new world position.</param>
        public static void SetPosition(GameObject marker, Vector3 worldPosition)
        {
            marker.transform.position = worldPosition + LocalOffset;
        }

        /// <summary>
        /// Returns the shared unlit material used by every rally point marker, creating it once.
        /// </summary>
        private static Material GetMaterial()
        {
            if (_sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _sharedMaterial = new Material(shader) { name = "RallyPointMarkerMaterial" };
                _sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            return _sharedMaterial;
        }
    }
}

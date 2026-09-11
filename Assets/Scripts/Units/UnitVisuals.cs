using System.Collections.Generic;
using MechTS.Utilities;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Owns a unit's faction/selection ring and art-renderer visibility, extracted from
    /// <see cref="UnitBase"/> (issue #96) — a plain composed class, not a MonoBehaviour, held
    /// as a private field on <see cref="UnitBase"/>. Mirrors the
    /// <see cref="Economy.EconomyRegistry"/>/<see cref="Economy.UnitUpkeepTracker"/> (issue #61)
    /// and <c>MapEditorPainter</c>/<c>MapEditorPreview</c> (issue #87) composition precedent —
    /// <see cref="UnitBase"/> still owns <see cref="Faction"/>/<see cref="UnitBase.IsSelected"/>,
    /// so every method here takes them as explicit parameters rather than holding a
    /// back-reference to the unit that owns it.
    /// </summary>
    public class UnitVisuals
    {
        private const float RingSelectedScale = 1.15f;
        private const float RingDimBrightness = 0.5f;

        /// <summary>
        /// How far above whatever surface a unit is standing on its art is nudged, so it
        /// doesn't sit flush with that surface's own mesh. See <see cref="NudgeArtAboveSurface"/>.
        /// </summary>
        private const float ArtYOffset = 0.03f;

        private static Material _sharedRingMaterial;

        private MeshRenderer _ringRenderer;
        private MaterialPropertyBlock _ringPropertyBlock;
        private List<Renderer> _artRenderers;

        /// <summary>
        /// Builds this unit's merged faction/selection ring — a hollow ring, sized from the
        /// unit's own <see cref="CapsuleCollider"/> radius where present, dim in the unit's
        /// faction color at rest and brighter/larger when selected (see
        /// <see cref="RefreshRingAppearance"/>) — collects its art renderers, and nudges them
        /// above the surface it's standing on (see <see cref="NudgeArtAboveSurface"/>). Called
        /// once from <see cref="UnitBase.Awake"/>, in this exact order, before
        /// <c>ComputeCapabilities</c> runs.
        /// </summary>
        /// <param name="root">The unit's own root transform.</param>
        /// <param name="faction">The unit's owning faction, for the ring's initial color.</param>
        public void Build(Transform root, Faction faction)
        {
            BuildRing(root);
            RefreshRingAppearance(faction, false);

            _artRenderers = new List<Renderer>(root.GetComponentsInChildren<Renderer>(true));
            _artRenderers.Remove(_ringRenderer);

            NudgeArtAboveSurface(root);
        }

        /// <summary>
        /// Creates the ring GameObject (mesh + renderer), sized from the unit's collider footprint.
        /// </summary>
        /// <param name="root">The unit's own root transform, to parent the ring under.</param>
        private void BuildRing(Transform root)
        {
            float radius = 0.5f;
            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule != null) radius = capsule.radius;

            var ringGo = new GameObject("FactionRing");
            ringGo.transform.SetParent(root, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            var meshFilter = ringGo.AddComponent<MeshFilter>();
            meshFilter.mesh = RingMesh.Create(radius * 1.4f, radius * 1.7f);

            _ringRenderer = ringGo.AddComponent<MeshRenderer>();
            _ringRenderer.material = GetRingMaterial();
            _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringPropertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Returns the shared unlit material used by every unit's ring, creating it once.
        /// </summary>
        private static Material GetRingMaterial()
        {
            if (_sharedRingMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _sharedRingMaterial = new Material(shader) { name = "UnitRingMaterial" };
                _sharedRingMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            return _sharedRingMaterial;
        }

        /// <summary>
        /// Updates the ring's color (dim faction color at rest, full brightness when selected)
        /// and scale (a slight pop when selected) to reflect current state. Called by
        /// <see cref="UnitBase"/> whenever its faction or selection state changes.
        /// </summary>
        /// <param name="faction">The unit's current owning faction.</param>
        /// <param name="isSelected">Whether the unit is currently selected.</param>
        public void RefreshRingAppearance(Faction faction, bool isSelected)
        {
            if (_ringRenderer == null) return;

            Color baseColor = FactionColor.GetColor(faction);
            Color color = isSelected ? baseColor : baseColor * RingDimBrightness;

            _ringPropertyBlock.SetColor("_BaseColor", color);
            _ringRenderer.SetPropertyBlock(_ringPropertyBlock);

            float scale = isSelected ? RingSelectedScale : 1f;
            _ringRenderer.transform.localScale = new Vector3(scale, 1f, scale);
        }

        /// <summary>
        /// Nudges each top-level art child (the direct children of the unit's own root that
        /// carry a renderer — either a single "Art" wrapper or several matched-parts siblings
        /// like Torso/Head/HandL/HandR, depending on the prefab's art convention) up by
        /// <see cref="ArtYOffset"/>, so the art never sits flush with the surface the unit is
        /// standing on (issue #35's elevated-platform occlusion). Walking each renderer up to
        /// its nearest ancestor that is a direct child of the unit's own transform (rather
        /// than nudging every renderer individually) avoids double-applying the offset to a
        /// nested child of an already-nudged parent (e.g. a hand parented under a torso).
        /// </summary>
        /// <param name="root">The unit's own root transform.</param>
        private void NudgeArtAboveSurface(Transform root)
        {
            var nudged = new HashSet<Transform>();
            foreach (var renderer in _artRenderers)
            {
                if (renderer == null) continue;

                Transform topLevel = renderer.transform;
                while (topLevel.parent != null && topLevel.parent != root)
                {
                    topLevel = topLevel.parent;
                }

                if (topLevel.parent == root && nudged.Add(topLevel))
                {
                    topLevel.localPosition += Vector3.up * ArtYOffset;
                }
            }
        }

        /// <summary>
        /// Enables or disables every art renderer and the faction/selection ring. Called by
        /// <see cref="UnitBase.Update"/> to hide an Enemy-owned unit's art while it's outside
        /// the Player faction's current vision (issue #36).
        /// </summary>
        /// <param name="visible">True to show, false to hide.</param>
        public void SetArtVisible(bool visible)
        {
            foreach (var renderer in _artRenderers)
            {
                if (renderer != null) renderer.enabled = visible;
            }

            if (_ringRenderer != null) _ringRenderer.enabled = visible;
        }
    }
}

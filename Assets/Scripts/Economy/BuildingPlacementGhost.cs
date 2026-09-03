using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechTS.Economy
{
    /// <summary>
    /// A translucent, non-functional preview instance of a pending building, following the
    /// cursor while placement is pending. Tinted green when the hovered position is clear,
    /// red when it would overlap an existing building (see
    /// <see cref="BuildingPlacement.WouldOverlapExistingBuilding"/> — the same check also
    /// gates the real placement, so this is a live preview of that check, not just cosmetic).
    /// Every runtime component (<see cref="BuildingInstance"/>/<see cref="Units.Health"/>/
    /// <see cref="ProductionQueue"/>/<see cref="Units.Weapon"/>) is destroyed immediately after
    /// <see cref="Instantiate"/> so the ghost never registers with <see cref="EconomyManager"/>,
    /// deals damage, etc., and every <see cref="Collider"/> is disabled so it never blocks a
    /// raycast or an overlap check itself. Uses the same URP Unlit + alpha-blended Transparent
    /// material recipe already proven working in this project by
    /// <see cref="Vision.FogOfWarRenderer"/>.
    /// </summary>
    public class BuildingPlacementGhost : MonoBehaviour
    {
        private static readonly Color ValidColor = new Color(0.3f, 1f, 0.3f, 0.45f);
        private static readonly Color InvalidColor = new Color(1f, 0.25f, 0.25f, 0.45f);

        private readonly List<Material> _instancedMaterials = new List<Material>();

        /// <summary>
        /// Instantiates a stripped-down, translucent copy of the given building prefab for
        /// use as a placement preview.
        /// </summary>
        /// <param name="sourcePrefab">The building prefab to preview.</param>
        public static BuildingPlacementGhost Create(GameObject sourcePrefab)
        {
            var instance = Instantiate(sourcePrefab);
            instance.name = "PlacementGhost";

            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Destroy(behaviour);
            }
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            var ghost = instance.AddComponent<BuildingPlacementGhost>();
            ghost.ApplyGhostMaterials();
            return ghost;
        }

        /// <summary>
        /// Replaces every renderer's materials with a fresh, instanced, alpha-blended copy —
        /// never touches the real building's shared material asset — and disables shadow
        /// casting, matching every other UI/indicator marker in this project.
        /// </summary>
        private void ApplyGhostMaterials()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = BuildGhostMaterial();
                    _instancedMaterials.Add(materials[i]);
                }
                renderer.materials = materials;
            }

            SetValid(true);
        }

        /// <summary>
        /// Builds a fresh alpha-blended Transparent URP Unlit material, matching the exact
        /// recipe already proven working in this project by <see cref="Vision.FogOfWarRenderer"/>.
        /// </summary>
        private static Material BuildGhostMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "PlacementGhostMaterial" };
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetFloat("_Surface", 1f); // URP Unlit: 0 = Opaque, 1 = Transparent
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetInt("_ZWrite", 0);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            return material;
        }

        /// <summary>Moves the ghost to the given world position.</summary>
        /// <param name="position">The world position to preview placement at.</param>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>Tints the ghost green (valid) or red (would overlap an existing building).</summary>
        /// <param name="valid">Whether the currently-previewed position is a legal placement.</param>
        public void SetValid(bool valid)
        {
            var color = valid ? ValidColor : InvalidColor;
            foreach (var material in _instancedMaterials)
            {
                material.color = color;
            }
        }
    }
}

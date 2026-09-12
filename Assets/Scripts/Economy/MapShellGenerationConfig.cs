using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Tunable parameters for <c>MapShellGenerator</c> (see <c>Assets/Editor/</c>) — a
    /// procedural environment/terrain-shell pass (elevation, ground texture, foliage) run once
    /// per generation, not a runtime system. Scope narrowed to terrain/foliage only in issue
    /// #95 — the first version also placed <c>Platform_Tier1</c>/<c>Ramp</c> clusters, but
    /// naive offset placement never connected them properly (those pieces need precise
    /// hand-placement, issue #35), so cliff/platform generation was dropped entirely rather
    /// than half-solved. ScriptableObject-first per convention so a future named preset (e.g.
    /// a "Canyon" vs. "Plains" style) is authoring a second asset, not a code change — v1 ships
    /// with one default config, presets are out of scope for now.
    /// <para>
    /// Flat Zones (issue #98): the generator also carves a small number of genuinely flat
    /// regions into the same heightmap — one anchored at each faction's <c>PlayerStartPoint</c>
    /// (a guaranteed buildable pad) plus a few smaller scattered ones for resource placement —
    /// so a designer doesn't need a manual post-generation flatten pass (which risked the
    /// terrain-elevation-vs-Fog-of-War rendering bug fixed in issue #97).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Map Editor/Map Shell Generation Config")]
    public class MapShellGenerationConfig : ScriptableObject
    {
        [Header("Elevation")]
        public int seed = 12345;

        /// <summary>Base frequency of the layered Perlin noise driving the heightmap — smaller values produce broader, gentler hills.</summary>
        public float noiseScale = 0.05f;

        /// <summary>World-unit height range the generated terrain is remapped into.</summary>
        public float minHeight;
        public float maxHeight = 6f;

        [Header("Foliage")]
        public MapBrushDefinition[] foliageBrushes;
        public int foliageClusterCount = 20;

        /// <summary>Foliage is never placed where the terrain slope exceeds this angle, in degrees.</summary>
        [Range(0f, 90f)] public float maxFoliageSlope = 25f;

        [Header("Flat Zones (issue #98)")]
        /// <summary>Radius, in world units, of the guaranteed-flat zone reserved around each faction's <c>PlayerStartPoint</c>.</summary>
        public float startZoneRadius = 15f;

        /// <summary>Distance, in world units, over which a start zone blends back into the surrounding noise — avoids a hard cliff edge.</summary>
        public float startZoneFalloff = 10f;

        /// <summary>How many additional smaller flat zones (for resource-node placement) to scatter across the map.</summary>
        public int resourceZoneCount = 4;

        /// <summary>Radius, in world units, of each scattered resource flat zone.</summary>
        public float resourceZoneRadius = 6f;

        /// <summary>Distance, in world units, over which a resource zone blends back into the surrounding noise.</summary>
        public float resourceZoneFalloff = 5f;

        /// <summary>Minimum distance, in world units, enforced between resource flat zones (and from start zones) via rejection sampling.</summary>
        public float resourceZoneMinSpacing = 15f;
    }
}

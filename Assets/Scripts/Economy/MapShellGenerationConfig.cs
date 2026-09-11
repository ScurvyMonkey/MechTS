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
    }
}

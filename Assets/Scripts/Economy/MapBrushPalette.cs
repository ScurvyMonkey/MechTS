using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// The full set of brushes shown in the Map Editor window's palette.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Map Editor/Brush Palette")]
    public class MapBrushPalette : ScriptableObject
    {
        public MapBrushDefinition[] brushes;

        /// <summary>
        /// World-unit size of the placement grid used by any brush with
        /// <see cref="MapBrushDefinition.useGridSnap"/> set (issue #71) — shared across every
        /// grid-snapping brush so pieces of different types still align to the same coordinate
        /// lattice (e.g. a wall's edge lines up with a platform's edge), rather than each brush
        /// defining its own independent pitch. Defaults to 8, matching SpacePlatformKit's own
        /// module width (confirmed via direct bounds inspection of its platform prefabs).
        /// </summary>
        public float gridSize = 8f;
    }
}

using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// A paintable entry for the Map Editor tool (see <c>MapEditorWindow</c> in
    /// <c>Assets/Editor/</c>): a pre-configured prefab plus display metadata for the brush
    /// palette. Adding a new paintable type is authoring one of these, not touching the
    /// tool's code.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Map Editor/Brush Definition")]
    public class MapBrushDefinition : ScriptableObject
    {
        public string displayName;

        /// <summary>Palette grouping label (e.g. "Resources", "Buildings", "Props").</summary>
        public string category;

        public GameObject prefab;

        /// <summary>
        /// If non-empty, every placement (single or splash) picks one random entry from this
        /// array to instantiate instead of <see cref="prefab"/> — lets one brush (e.g. "Rock")
        /// scatter naturally varied-looking results from a themed set of prefabs rather than
        /// stamping the same model repeatedly (issue #86). <see cref="prefab"/> can be left
        /// unassigned for a variants-only brush — the Map Editor tool's paint/erase logic
        /// treats a non-empty <see cref="prefabVariants"/> as equally valid. Default empty —
        /// every brush that doesn't opt in behaves exactly as before.
        /// </summary>
        public GameObject[] prefabVariants;

        /// <summary>
        /// If true, painting this brush scatters <see cref="splashCount"/> randomized
        /// instances within <see cref="splashRadius"/> instead of placing exactly one at the
        /// resolved point. Default false — every brush that doesn't opt in behaves exactly as
        /// before.
        /// </summary>
        public bool isSplashBrush;

        /// <summary>Number of instances scattered per paint tick when <see cref="isSplashBrush"/> is set.</summary>
        public int splashCount = 5;

        /// <summary>Radius (world units) within which scattered instances are randomly placed.</summary>
        public float splashRadius = 3f;

        /// <summary>If set, each scattered instance gets a random Y-axis rotation (0-360 degrees).</summary>
        public bool randomizeRotation;

        /// <summary>
        /// Min (x) / max (y) uniform scale applied to each scattered instance, picked
        /// independently per instance. Explicitly initialized to (1,1) — Unity's own default
        /// for an unset Vector2 field is (0,0), which would silently spawn every splash
        /// instance at zero scale (invisible) unless this is set here rather than left to the
        /// serializer's own default.
        /// </summary>
        public Vector2 scaleRange = new Vector2(1f, 1f);

        /// <summary>
        /// If true, this brush's placement X/Z position snaps to the nearest multiple of the
        /// owning <see cref="MapBrushPalette.gridSize"/> before painting (issue #71) — for
        /// modular kit pieces (SpacePlatformKit's platforms/walls) that need to tile edge-to-edge
        /// precisely rather than landing at whatever sub-unit position the cursor's raycast
        /// happened to hit. Default false — every existing freehand/organic brush (Props,
        /// Resources, Buildings, Spawn Points) is completely unaffected.
        /// </summary>
        public bool useGridSnap;
    }
}

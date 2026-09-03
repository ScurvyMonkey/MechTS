using System.Collections.Generic;
using MechTS.Economy;
using UnityEditor;
using UnityEngine;

namespace MechTS.EditorTools
{
    /// <summary>
    /// Paint/erase mechanics and <c>MapContent</c> category-folder management for
    /// <see cref="MapEditorWindow"/> (issue #87 — extracted once the window's own
    /// responsibilities grew past a single class across 13+ prior issues, mirroring how
    /// <c>EconomyManager</c> composes <c>EconomyRegistry</c>/<c>UnitUpkeepTracker</c> rather
    /// than growing one class indefinitely, issue #61). A plain class the window constructs
    /// once and holds as a private field — not a second <see cref="EditorWindow"/>, not a
    /// <c>Bootstrapper</c>-created singleton (this is Editor-only tooling, not runtime).
    /// </summary>
    public class MapEditorPainter
    {
        public const string GroundLayerName = "Ground";
        public const string TerrainCategory = "Terrain";
        public const float MaxRaycastDistance = 1000f;

        private const float MinPaintSpacing = 1.5f;
        private const float EraseRadius = 1.5f;
        private const float SplashSpacingFraction = 0.5f;
        private const int MaxSpacingAttempts = 20;

        /// <summary>
        /// How far above a splash instance's candidate XZ position its own independent
        /// downward raycast starts from — must clear the tallest terrain this project's
        /// splash-eligible content could realistically be painted near.
        /// </summary>
        private const float SplashRaycastHeight = 50f;

        private Vector3? _lastPaintPosition;

        /// <summary>
        /// Clears the drag-spacing tracker — called when the mouse button is released, so the
        /// next paint tick of a fresh drag isn't spaced against a stale position left over from
        /// a previous one.
        /// </summary>
        public void EndDrag()
        {
            _lastPaintPosition = null;
        }

        /// <summary>
        /// Places the given brush at the given raw Ground-layer raycast point — a single
        /// instance for an ordinary brush, or a randomized scatter batch for a splash brush —
        /// if far enough from the last paint position in this drag to avoid stacking duplicates
        /// every frame. Resolves the brush's own category-based Y-force and grid-snap before
        /// the spacing check (moved here from <see cref="MapEditorWindow.OnSceneGUI"/> as part
        /// of issue #87's extraction, same order/behavior as before): a "Terrain" category
        /// brush (Platform_Tier1, Ramp) always starts flush on bare Ground (Y=0) — both prefabs
        /// are built base-anchored specifically so their root sits at Y=0, and multi-tier
        /// stacking is explicitly out of scope (issue #35) — without this override, painting a
        /// new Terrain brush while hovering over already-placed elevated terrain would resolve
        /// against that surface's height instead of the ground beneath it. Grid snapping
        /// (issue #71) is independent of the Terrain Y-force — a Structures piece placed atop
        /// an elevated surface still wants its X/Z snapped, just not its Y forced to 0.
        /// A splash brush's own <see cref="MapBrushDefinition.splashCount"/> instances all
        /// count as one paint tick for the spacing check, matching the origin click/drag point
        /// exactly like an ordinary brush does. For a splash brush specifically, the required
        /// gap is at least its own <see cref="MapBrushDefinition.splashRadius"/> (not the flat
        /// <see cref="MinPaintSpacing"/> ordinary brushes use) — a splash brush already
        /// scatters instances across its full radius in one click, so dragging at the same
        /// tight spacing would drop another full batch almost on top of the last one on every
        /// drag step (issue #62 follow-up).
        /// </summary>
        /// <param name="brush">The brush to paint.</param>
        /// <param name="rawWorldPoint">The raw Ground-layer raycast hit point, before any
        /// brush-specific Y-force or grid-snap resolution (issue #35: this may be bare Ground,
        /// a Platform's top, or a Ramp's sloped surface).</param>
        /// <param name="gridSize">The palette's shared grid size, used only if the brush opts
        /// into grid snapping.</param>
        public void TryPaint(MapBrushDefinition brush, Vector3 rawWorldPoint, float gridSize)
        {
            if (!HasPaintablePrefab(brush)) return;

            Vector3 worldPoint = rawWorldPoint;
            if (brush.category == TerrainCategory) worldPoint.y = 0f;
            if (brush.useGridSnap) worldPoint = SnapToGrid(worldPoint, gridSize);

            float requiredSpacing = brush.isSplashBrush
                ? Mathf.Max(MinPaintSpacing, brush.splashRadius)
                : MinPaintSpacing;
            if (_lastPaintPosition.HasValue && Vector3.Distance(_lastPaintPosition.Value, worldPoint) < requiredSpacing) return;

            if (brush.isSplashBrush)
            {
                PaintSplash(brush, worldPoint);
            }
            else
            {
                PaintSingle(brush, worldPoint);
            }

            _lastPaintPosition = worldPoint;
        }

        /// <summary>
        /// Whether the given brush has something to actually instantiate — either a single
        /// <see cref="MapBrushDefinition.prefab"/> or a non-empty
        /// <see cref="MapBrushDefinition.prefabVariants"/> (issue #86). Every paint/erase entry
        /// point gates on this instead of checking <c>prefab == null</c> directly, so a
        /// variants-only brush (no single <c>prefab</c> assigned) is still paintable/erasable.
        /// </summary>
        /// <param name="brush">The brush to check.</param>
        public static bool HasPaintablePrefab(MapBrushDefinition brush)
        {
            return brush.prefab != null || (brush.prefabVariants != null && brush.prefabVariants.Length > 0);
        }

        /// <summary>
        /// Resolves which prefab a single placement of the given brush should instantiate —
        /// a random entry from <see cref="MapBrushDefinition.prefabVariants"/> when non-empty
        /// (issue #86), otherwise the brush's single <see cref="MapBrushDefinition.prefab"/>.
        /// Called independently per instance by both <see cref="PaintSingle"/> and each
        /// iteration of <see cref="PaintSplash"/>'s loop, so a splash-cluster batch scatters
        /// naturally varied results rather than one model repeated.
        /// </summary>
        /// <param name="brush">The brush being painted.</param>
        private static GameObject ResolvePrefab(MapBrushDefinition brush)
        {
            if (brush.prefabVariants != null && brush.prefabVariants.Length > 0)
            {
                return brush.prefabVariants[Random.Range(0, brush.prefabVariants.Length)];
            }
            return brush.prefab;
        }

        /// <summary>
        /// Whether <paramref name="source"/> — a scene instance's traced-back original prefab
        /// asset (<see cref="PrefabUtility.GetCorrespondingObjectFromOriginalSource"/>) —
        /// belongs to the given brush: either its single <see cref="MapBrushDefinition.prefab"/>
        /// or any entry in its <see cref="MapBrushDefinition.prefabVariants"/> (issue #86).
        /// Without the variants check, <see cref="TryErase"/> would only ever recognize
        /// instances matching whichever single variant happened to be placed most recently as
        /// <c>prefab</c>, silently leaving every other variant permanently un-erasable.
        /// </summary>
        /// <param name="brush">The currently selected brush.</param>
        /// <param name="source">The traced-back prefab asset of a candidate scene instance.</param>
        private static bool IsBrushSource(MapBrushDefinition brush, Object source)
        {
            if (source == brush.prefab) return true;
            if (brush.prefabVariants == null) return false;
            foreach (var variant in brush.prefabVariants)
            {
                if (source == variant) return true;
            }
            return false;
        }

        /// <summary>
        /// Instantiates the given brush's prefab exactly at the given world position, parented
        /// under its category's organizational GameObject.
        /// </summary>
        /// <param name="brush">The brush to paint.</param>
        /// <param name="worldPoint">The already-resolved world position to paint at.</param>
        private void PaintSingle(MapBrushDefinition brush, Vector3 worldPoint)
        {
            var parent = GetOrCreateCategoryParent(brush.category);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(ResolvePrefab(brush), parent.transform);
            instance.transform.position = worldPoint;
            Undo.RegisterCreatedObjectUndo(instance, $"Paint {brush.displayName}");

            // Edit Mode never runs a freshly-instantiated component's Awake() (confirmed
            // directly), so a ResourceNode's scatter cluster must be generated explicitly
            // here — otherwise a painted area shows nothing until Play Mode starts.
            if (instance.TryGetComponent<ResourceNode>(out var resourceNode))
            {
                resourceNode.GenerateScatter();
            }
        }

        /// <summary>
        /// Scatters <see cref="MapBrushDefinition.splashCount"/> randomized instances of the
        /// given brush's prefab within <see cref="MapBrushDefinition.splashRadius"/> of the
        /// given origin point, each with optional random Y rotation and uniform scale. Each
        /// instance resolves its own surface height via an independent vertical raycast from
        /// above its own randomized XZ position — not a reuse of the cursor's own screen-space
        /// ray, which only has meaning for the actual mouse position — so a splash painted near
        /// a Platform_Tier1/Ramp edge (issue #35) correctly places each instance on whatever
        /// surface is actually beneath it. An instance whose raycast misses the Ground layer
        /// entirely (e.g. an offset landing past the map's edge) is silently skipped.
        /// Candidate XZ offsets are rejection-sampled against every already-placed instance in
        /// this same batch, requiring at least <see cref="SplashSpacingFraction"/> of the
        /// brush's own <see cref="MapBrushDefinition.splashRadius"/> between them (issue #63
        /// follow-up) — <c>Random.insideUnitCircle</c> alone has no minimum-
        /// spacing guarantee, so with a small <see cref="MapBrushDefinition.splashCount"/> it's
        /// entirely possible for 2-3 candidates to land close enough to touch/overlap by pure
        /// chance even at a generously-tuned radius, which reads as "something's chained/
        /// rotated wrong" the same way the earlier drag-stacking bug did, from a third,
        /// independent cause. Falls back to placing at the last-tried candidate if
        /// <see cref="MaxSpacingAttempts"/> is exhausted, rather than silently dropping the
        /// instance — a worse-than-ideal placement is better than paint quietly doing less than
        /// the configured <see cref="MapBrushDefinition.splashCount"/>.
        /// </summary>
        /// <param name="brush">The brush to paint.</param>
        /// <param name="originPoint">The Ground-layer surface world position the scatter is centered on.</param>
        private void PaintSplash(MapBrushDefinition brush, Vector3 originPoint)
        {
            var parent = GetOrCreateCategoryParent(brush.category);
            int groundLayerMask = LayerMask.GetMask(GroundLayerName);
            var placedXZ = new List<Vector2>();
            // A fraction of the brush's own splashRadius, not a flat constant — this keeps the
            // spacing proportional to whatever a given brush is already tuned to (a small-object
            // brush like Plant1Cluster, radius 3, still allows fairly close/dense placement; a
            // full-tree brush at radius 8 gets a proportionally larger gap) rather than forcing
            // one absolute distance onto every brush regardless of its own scale.
            float minSpacing = brush.splashRadius * SplashSpacingFraction;

            for (int i = 0; i < brush.splashCount; i++)
            {
                Vector2 offset = Vector2.zero;
                for (int attempt = 0; attempt < MaxSpacingAttempts; attempt++)
                {
                    Vector2 candidate = Random.insideUnitCircle * brush.splashRadius;
                    offset = candidate;
                    bool farEnough = true;
                    foreach (var placed in placedXZ)
                    {
                        if (Vector2.Distance(candidate, placed) < minSpacing) { farEnough = false; break; }
                    }
                    if (farEnough) break;
                }
                placedXZ.Add(offset);

                Vector3 rayOrigin = new Vector3(originPoint.x + offset.x, originPoint.y + SplashRaycastHeight, originPoint.z + offset.y);
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, MaxRaycastDistance, groundLayerMask)) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ResolvePrefab(brush), parent.transform);
                instance.transform.position = hit.point;

                if (brush.randomizeRotation)
                {
                    instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                // A no-op when scaleRange is the (1,1) default — Random.Range(1, 1) is 1.
                float scale = Random.Range(brush.scaleRange.x, brush.scaleRange.y);
                instance.transform.localScale *= scale;

                Undo.RegisterCreatedObjectUndo(instance, $"Paint {brush.displayName} (Splash)");

                if (instance.TryGetComponent<ResourceNode>(out var resourceNode))
                {
                    resourceNode.GenerateScatter();
                }
            }
        }

        /// <summary>
        /// Destroys the nearest instance of the <b>given brush's own prefab</b> within
        /// <see cref="EraseRadius"/> of the given world position, searching only under the
        /// <c>MapContent</c> root so erase can never remove scene content this tool didn't
        /// paint. A no-op if the brush has nothing paintable to match against (issue #74,
        /// designer-reported: right-click was blindly erasing the nearest painted object
        /// regardless of which brush created it, making it unsafe to clean up one piece type
        /// without risking an unrelated neighbor). Distance is compared on the XZ plane only,
        /// ignoring height — a right-click naturally lands on whatever surface is visible under
        /// the cursor (e.g. a <c>Platform_Tier1</c>'s top at Y=2.5), which can be several units
        /// above that object's own base-anchored pivot; a full 3D distance check would make any
        /// tall terrain object un-erasable regardless of how precisely the cursor is placed
        /// over it.
        /// </summary>
        /// <param name="brush">The currently selected brush to match against.</param>
        /// <param name="worldPoint">The Ground-layer surface world position to erase at.</param>
        public void TryErase(MapBrushDefinition brush, Vector3 worldPoint)
        {
            if (brush == null || !HasPaintablePrefab(brush)) return;

            var mapContent = GameObject.Find("MapContent");
            if (mapContent == null) return;

            Vector2 clickXZ = new Vector2(worldPoint.x, worldPoint.z);
            Transform nearest = null;
            float nearestDistance = EraseRadius;

            foreach (Transform categoryParent in mapContent.transform)
            {
                foreach (Transform child in categoryParent)
                {
                    // Only ever consider instances of the selected brush's own prefab (or, for
                    // a variant brush, any one of its prefabVariants) — never the nearest
                    // object regardless of type, even one painted by a different brush sitting
                    // right next to it.
                    var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                    if (!IsBrushSource(brush, source)) continue;

                    Vector2 childXZ = new Vector2(child.position.x, child.position.z);
                    float dist = Vector2.Distance(childXZ, clickXZ);
                    if (dist < nearestDistance)
                    {
                        nearestDistance = dist;
                        nearest = child;
                    }
                }
            }

            if (nearest != null)
            {
                Undo.DestroyObjectImmediate(nearest.gameObject);
            }
        }

        /// <summary>
        /// Finds or creates the <c>MapContent/&lt;category&gt;</c> organizational
        /// GameObject that painted instances of the given category are parented under. Its own
        /// local transform is force-reset to identity on every call (issue #73) — a category
        /// folder is purely organizational and must never carry position/rotation/scale of its
        /// own, since every child instantiated under it inherits that in world space. A real
        /// incident found this the hard way: an accidental Hierarchy-panel rotate on the
        /// <c>Terrain</c> folder itself (not any individual piece) silently rotated every
        /// existing AND every subsequently-painted Terrain-category piece in world space, while
        /// each piece's own local transform stayed perfectly correct the whole time — extremely
        /// non-obvious to diagnose from the Scene view alone. This reset makes that entire class
        /// of mistake self-healing the next time anything is painted into the affected category.
        /// </summary>
        /// <param name="category">The brush category to find or create a parent for.</param>
        private GameObject GetOrCreateCategoryParent(string category)
        {
            var mapContent = GameObject.Find("MapContent");
            if (mapContent == null)
            {
                mapContent = new GameObject("MapContent");
                Undo.RegisterCreatedObjectUndo(mapContent, "Create MapContent");
            }

            string categoryName = string.IsNullOrEmpty(category) ? "Uncategorized" : category;
            var existing = mapContent.transform.Find(categoryName);
            if (existing != null)
            {
                ResetCategoryTransform(existing);
                return existing.gameObject;
            }

            var categoryGo = new GameObject(categoryName);
            Undo.RegisterCreatedObjectUndo(categoryGo, "Create MapContent Category");
            categoryGo.transform.SetParent(mapContent.transform);
            return categoryGo;
        }

        /// <summary>
        /// Forces a category folder's local position/rotation/scale back to identity if it has
        /// drifted (issue #73) — see <see cref="GetOrCreateCategoryParent"/> for why this matters.
        /// </summary>
        /// <param name="categoryTransform">The category folder to sanitize.</param>
        private void ResetCategoryTransform(Transform categoryTransform)
        {
            if (categoryTransform.localPosition == Vector3.zero
                && categoryTransform.localRotation == Quaternion.identity
                && categoryTransform.localScale == Vector3.one)
            {
                return;
            }

            Debug.LogWarning($"MechTS: Map Editor category folder '{categoryTransform.name}' had a non-identity transform (pos={categoryTransform.localPosition}, rot={categoryTransform.localEulerAngles}, scale={categoryTransform.localScale}) — reset to identity. Every child painted into this category was inheriting that offset in world space.");
            categoryTransform.localPosition = Vector3.zero;
            categoryTransform.localRotation = Quaternion.identity;
            categoryTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// Snaps a world position's X/Z to the nearest multiple of <paramref name="gridSize"/>
        /// (issue #71), leaving Y untouched. Falls back to the unsnapped point if a non-positive
        /// grid size is given, rather than dividing by zero. Static and public so
        /// <see cref="MapEditorPreview"/>'s grid-snap gizmo can share the exact same resolution
        /// logic without duplicating it.
        /// </summary>
        /// <param name="point">The world position to snap.</param>
        /// <param name="gridSize">The grid size to snap to (typically <c>MapBrushPalette.gridSize</c>).</param>
        public static Vector3 SnapToGrid(Vector3 point, float gridSize)
        {
            if (gridSize <= 0f) return point;

            float snappedX = Mathf.Round(point.x / gridSize) * gridSize;
            float snappedZ = Mathf.Round(point.z / gridSize) * gridSize;
            return new Vector3(snappedX, point.y, snappedZ);
        }
    }
}

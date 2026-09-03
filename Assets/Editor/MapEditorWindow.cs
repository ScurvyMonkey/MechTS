using System.Linq;
using MechTS.Economy;
using MechTS.Utilities;
using UnityEditor;
using UnityEngine;

namespace MechTS.EditorTools
{
    /// <summary>
    /// Custom Scene View tool for painting registered prefabs (resource nodes, buildings,
    /// props, flora) directly onto the map: select a brush here, then left-click/drag in
    /// the Scene view to place it, right-click to erase the nearest instance of that same
    /// selected brush (issue #74 — never a different brush's piece, even if it's closer to
    /// the cursor; erase is a no-op with nothing selected). Edit Mode only — see
    /// <see cref="OnSceneGUI"/>. Painted objects are ordinary scene GameObjects afterward,
    /// fully editable via the normal Inspector, and every placement/erase goes through
    /// Unity's own <see cref="Undo"/> system.
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private const float MinPaintSpacing = 1.5f;
        private const float EraseRadius = 1.5f;
        private const float SplashSpacingFraction = 0.5f;
        private const int MaxSpacingAttempts = 20;
        private const float MaxRaycastDistance = 1000f;
        private const string DefaultPalettePath = "Assets/Data/MapEditor/MapBrushPalette.asset";
        private const string GroundLayerName = "Ground";
        private const string TerrainCategory = "Terrain";

        /// <summary>
        /// How far above a splash instance's candidate XZ position its own independent
        /// downward raycast starts from — must clear the tallest terrain this project's
        /// splash-eligible content could realistically be painted near.
        /// </summary>
        private const float SplashRaycastHeight = 50f;

        /// <summary>Radius of the always-visible marker drawn for each painted <see cref="PlayerStartPoint"/>.</summary>
        private const float StartPointMarkerRadius = 1.5f;

        [SerializeField] private MapBrushPalette _palette;
        [SerializeField] private MapBrushDefinition _selectedBrush;

        private Vector3? _lastPaintPosition;
        private Vector2 _scrollPosition;

        /// <summary>
        /// Opens the Map Editor window, via the <c>MechTS/Map Editor</c> menu item.
        /// </summary>
        [MenuItem("MechTS/Map Editor")]
        private static void Open()
        {
            GetWindow<MapEditorWindow>("Map Editor");
        }

        /// <summary>
        /// Subscribes to Scene View input and loads the default brush palette if none is assigned yet.
        /// </summary>
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

            if (_palette == null)
            {
                _palette = AssetDatabase.LoadAssetAtPath<MapBrushPalette>(DefaultPalettePath);
            }
        }

        /// <summary>
        /// Unsubscribes from Scene View input.
        /// </summary>
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        /// <summary>
        /// Draws the brush palette, grouped by category, plus the palette-asset field and current mode hint.
        /// </summary>
        private void OnGUI()
        {
            _palette = (MapBrushPalette)EditorGUILayout.ObjectField("Brush Palette", _palette, typeof(MapBrushPalette), false);

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Painting is disabled during Play Mode.", MessageType.Info);
            }

            if (_palette == null || _palette.brushes == null || _palette.brushes.Length == 0)
            {
                EditorGUILayout.HelpBox("Assign a Brush Palette asset to begin painting.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Left-click/drag in the Scene view to paint. Right-click to erase the nearest piece of the selected brush only (issue #74) — select a brush first, or erase does nothing.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var groups = _palette.brushes
                .Where(b => b != null)
                .GroupBy(b => string.IsNullOrEmpty(b.category) ? "Uncategorized" : b.category);

            foreach (var group in groups)
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                foreach (var brush in group)
                {
                    DrawBrushButton(brush);
                }
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a single brush's selectable button, highlighted while selected.
        /// </summary>
        /// <param name="brush">The brush this button represents.</param>
        private void DrawBrushButton(MapBrushDefinition brush)
        {
            bool isSelected = _selectedBrush == brush;
            Color previousColor = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = Color.cyan;

            if (GUILayout.Button(brush.displayName))
            {
                _selectedBrush = isSelected ? null : brush;
            }

            GUI.backgroundColor = previousColor;
        }

        /// <summary>
        /// Handles Scene View mouse input: left-click/drag paints the selected brush,
        /// right-click/drag erases the nearest painted object under the cursor. No-ops
        /// entirely during Play Mode, since this is a level-authoring tool only.
        /// </summary>
        /// <param name="sceneView">The Scene View this GUI callback is for.</param>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (EditorApplication.isPlaying) return;

            // The Scene View only generates MouseMove events (hover, no button held) when
            // this flag is set — without it, a splash brush's radius-preview gizmo below
            // could never track the cursor before the first click, since OnSceneGUI simply
            // wouldn't be called for pure hover at all. Cheap to set on every call.
            sceneView.wantsMouseMove = true;

            Event e = Event.current;
            if (e == null) return;

            bool isPaintEvent = _selectedBrush != null && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
            bool isEraseEvent = e.button == 1 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);

            if (e.type == EventType.MouseUp)
            {
                _lastPaintPosition = null;
            }

            // Must run ahead of the paint/erase-only early-return below: the radius preview
            // needs to draw on hover alone (MouseMove/Repaint), before any button is pressed,
            // to actually serve as a "preview before you click" aid.
            if (_selectedBrush != null && _selectedBrush.isSplashBrush)
            {
                DrawSplashRadiusPreview(e);
            }

            if (_selectedBrush != null && _selectedBrush.useGridSnap)
            {
                DrawGridSnapPreview(e);
            }

            DrawStartPointMarkers();

            if (!isPaintEvent && !isEraseEvent) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // A real physics raycast against the Ground layer (issue #35) — rather than the
            // flat Y=0 math plane issue #18 originally used — so painting resolves the actual
            // surface height under the cursor, including an elevated Platform_Tier1's top or
            // a Ramp's sloped surface, not just bare Ground. Both new terrain prefabs are on
            // this same layer specifically so this one raycast covers all three.
            // SyncTransforms is required here, confirmed directly: a collider from a brush
            // painted moments earlier in the same session is NOT yet visible to
            // Physics.Raycast in Edit Mode without this — e.g. painting a building brush
            // immediately after painting the Platform underneath it would otherwise silently
            // resolve against bare Ground (Y=0) instead of the Platform's real top surface.
            Physics.SyncTransforms();
            int groundLayerMask = LayerMask.GetMask(GroundLayerName);
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxRaycastDistance, groundLayerMask)) return;

            Vector3 worldPoint = hit.point;

            // A "Terrain" category brush (Platform_Tier1, Ramp) is a base-tier feature that
            // always starts flush on bare Ground (Y=0) — both prefabs are built base-anchored
            // specifically so their root sits at Y=0. Multi-tier stacking (a platform painted
            // on top of another platform/ramp) is explicitly out of scope for this project
            // (issue #35). Without this override, painting a new Terrain brush while hovering
            // over already-placed elevated terrain resolves the raycast to THAT surface's
            // height instead of the ground beneath it, spawning the new piece floating in the
            // air, disconnected from every ramp built to reach the correct Y=2.5 top — a real
            // bug found post-ship (see memory/PATTERNS.md's 2026-08-23 entry). Forcing Y=0
            // here only affects Terrain-category paints; props/buildings/resource nodes still
            // correctly resolve to whatever elevated surface they're painted on.
            if (isPaintEvent && _selectedBrush.category == TerrainCategory)
            {
                worldPoint.y = 0f;
            }

            // Snap X/Z to the shared placement grid (issue #71) for a brush that opted in —
            // independent of the Terrain Y-force above, since grid alignment and surface height
            // are orthogonal concerns (a Structures piece placed atop an elevated surface still
            // wants its X/Z snapped, just not its Y forced to 0). Only applied on paint, never
            // erase — erase should always target whatever's actually nearest the cursor, not a
            // hypothetical grid cell.
            if (isPaintEvent && _selectedBrush.useGridSnap)
            {
                worldPoint = SnapToGrid(worldPoint);
            }

            // Claim the event so painting/erasing doesn't also orbit the Scene View camera
            // or try to select objects underneath the cursor.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            if (isPaintEvent)
            {
                TryPaint(worldPoint);
            }
            else
            {
                TryErase(worldPoint);
            }

            e.Use();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Places the selected brush at the given world position — a single instance for an
        /// ordinary brush, or a randomized scatter batch for a splash brush — if far enough
        /// from the last paint position in this drag to avoid stacking duplicates every frame.
        /// A splash brush's own <see cref="MapBrushDefinition.splashCount"/> instances all
        /// count as one paint tick for this spacing check, matching the origin click/drag
        /// point exactly like an ordinary brush does. For a splash brush specifically, the
        /// required gap is at least its own <see cref="MapBrushDefinition.splashRadius"/>
        /// (not the flat <see cref="MinPaintSpacing"/> ordinary brushes use) — a splash
        /// brush already scatters instances across its full radius in one click, so dragging
        /// at the same tight spacing ordinary brushes use would drop another full batch
        /// almost on top of the last one on every drag step, unioning consecutive batches
        /// into one dense, overlapping mass regardless of how loosely any single batch is
        /// tuned. Found via a real designer report (issue #62 follow-up) that read as "the
        /// splash brush places trees strangely" even after the per-batch density itself was
        /// already fixed — the actual cause was multiple batches from one drag, not one
        /// batch's own tuning.
        /// </summary>
        /// <param name="worldPoint">The Ground-layer surface world position to paint at (issue #35: this may be bare Ground, a Platform's top, or a Ramp's sloped surface).</param>
        private void TryPaint(Vector3 worldPoint)
        {
            if (!HasPaintablePrefab(_selectedBrush)) return;
            float requiredSpacing = _selectedBrush.isSplashBrush
                ? Mathf.Max(MinPaintSpacing, _selectedBrush.splashRadius)
                : MinPaintSpacing;
            if (_lastPaintPosition.HasValue && Vector3.Distance(_lastPaintPosition.Value, worldPoint) < requiredSpacing) return;

            if (_selectedBrush.isSplashBrush)
            {
                PaintSplash(worldPoint);
            }
            else
            {
                PaintSingle(worldPoint);
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
        private static bool HasPaintablePrefab(MapBrushDefinition brush)
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
        /// Instantiates the selected brush's prefab exactly at the given world position,
        /// parented under its category's organizational GameObject.
        /// </summary>
        /// <param name="worldPoint">The Ground-layer surface world position to paint at.</param>
        private void PaintSingle(Vector3 worldPoint)
        {
            var parent = GetOrCreateCategoryParent(_selectedBrush.category);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(ResolvePrefab(_selectedBrush), parent.transform);
            instance.transform.position = worldPoint;
            Undo.RegisterCreatedObjectUndo(instance, $"Paint {_selectedBrush.displayName}");

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
        /// selected brush's prefab within <see cref="MapBrushDefinition.splashRadius"/> of the
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
        /// <param name="originPoint">The Ground-layer surface world position the scatter is centered on.</param>
        private void PaintSplash(Vector3 originPoint)
        {
            var parent = GetOrCreateCategoryParent(_selectedBrush.category);
            int groundLayerMask = LayerMask.GetMask(GroundLayerName);
            var placedXZ = new System.Collections.Generic.List<Vector2>();
            // A fraction of the brush's own splashRadius, not a flat constant — this keeps the
            // spacing proportional to whatever a given brush is already tuned to (a small-object
            // brush like Plant1Cluster, radius 3, still allows fairly close/dense placement; a
            // full-tree brush at radius 8 gets a proportionally larger gap) rather than forcing
            // one absolute distance onto every brush regardless of its own scale.
            float minSpacing = _selectedBrush.splashRadius * SplashSpacingFraction;

            for (int i = 0; i < _selectedBrush.splashCount; i++)
            {
                Vector2 offset = Vector2.zero;
                for (int attempt = 0; attempt < MaxSpacingAttempts; attempt++)
                {
                    Vector2 candidate = Random.insideUnitCircle * _selectedBrush.splashRadius;
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

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ResolvePrefab(_selectedBrush), parent.transform);
                instance.transform.position = hit.point;

                if (_selectedBrush.randomizeRotation)
                {
                    instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                // A no-op when scaleRange is the (1,1) default — Random.Range(1, 1) is 1.
                float scale = Random.Range(_selectedBrush.scaleRange.x, _selectedBrush.scaleRange.y);
                instance.transform.localScale *= scale;

                Undo.RegisterCreatedObjectUndo(instance, $"Paint {_selectedBrush.displayName} (Splash)");

                if (instance.TryGetComponent<ResourceNode>(out var resourceNode))
                {
                    resourceNode.GenerateScatter();
                }
            }
        }

        /// <summary>
        /// Draws a wire-circle gizmo previewing a splash brush's scatter radius at the
        /// cursor's currently-resolved Ground-layer surface position, so the designer can see
        /// the scatter area before clicking. Called every Scene View event while a splash
        /// brush is selected (see the hover-tracking note in <see cref="OnSceneGUI"/>), not
        /// just during an actual paint/erase action.
        /// </summary>
        /// <param name="e">The current Scene View GUI event, used to resolve the cursor's screen position.</param>
        private void DrawSplashRadiusPreview(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            int groundLayerMask = LayerMask.GetMask(GroundLayerName);
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxRaycastDistance, groundLayerMask)) return;

            Handles.color = Color.cyan;
            Handles.DrawWireDisc(hit.point, Vector3.up, _selectedBrush.splashRadius);
        }

        /// <summary>
        /// Snaps a world position's X/Z to the nearest multiple of <see cref="MapBrushPalette.gridSize"/>
        /// (issue #71), leaving Y untouched. Falls back to the unsnapped point if no palette or a
        /// non-positive grid size is configured, rather than dividing by zero.
        /// </summary>
        /// <param name="point">The world position to snap.</param>
        private Vector3 SnapToGrid(Vector3 point)
        {
            float gridSize = _palette != null ? _palette.gridSize : 0f;
            if (gridSize <= 0f) return point;

            float snappedX = Mathf.Round(point.x / gridSize) * gridSize;
            float snappedZ = Mathf.Round(point.z / gridSize) * gridSize;
            return new Vector3(snappedX, point.y, snappedZ);
        }

        /// <summary>
        /// Draws a wire-square gizmo at the cursor's grid-snapped position (issue #71), the size
        /// of one grid cell, so the designer can see exactly where a grid-snapping brush will
        /// land before clicking. Called every Scene View event while such a brush is selected
        /// (mirrors <see cref="DrawSplashRadiusPreview"/>'s hover-tracking shape), not just
        /// during an actual paint action.
        /// </summary>
        /// <param name="e">The current Scene View GUI event, used to resolve the cursor's screen position.</param>
        private void DrawGridSnapPreview(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            int groundLayerMask = LayerMask.GetMask(GroundLayerName);
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxRaycastDistance, groundLayerMask)) return;

            Vector3 worldPoint = hit.point;
            if (_selectedBrush.category == TerrainCategory) worldPoint.y = 0f;
            Vector3 snapped = SnapToGrid(worldPoint);

            float gridSize = _palette != null ? _palette.gridSize : 0f;
            Handles.color = Color.yellow;
            Handles.DrawWireCube(snapped + Vector3.up * 0.05f, new Vector3(gridSize, 0.1f, gridSize));
        }

        /// <summary>
        /// Draws a faction-colored wire disc at every <see cref="PlayerStartPoint"/> currently
        /// in the scene, every time this window's Scene View callback runs. Deliberately drawn
        /// via <see cref="Handles"/> rather than relying on <see cref="PlayerStartPoint"/>'s own
        /// <c>OnDrawGizmos</c> — Editor Gizmos are gated by the Scene view's own "Gizmos"
        /// toolbar toggle (confirmed off by default in at least one real session, making a
        /// freshly-painted start point look like nothing happened), while a `Handles` call made
        /// here, from this window's own `OnSceneGUI`, is not. This keeps `PlayerStartPoint`'s
        /// existing Gizmo as a harmless fallback for anyone Scene-browsing without this window
        /// open, while guaranteeing visibility for the actual painting workflow this bug was
        /// reported against.
        /// </summary>
        private void DrawStartPointMarkers()
        {
            foreach (var startPoint in Object.FindObjectsByType<PlayerStartPoint>(FindObjectsSortMode.None))
            {
                Handles.color = FactionColor.GetColor(startPoint.faction);
                Handles.DrawWireDisc(startPoint.transform.position, Vector3.up, StartPointMarkerRadius);
            }
        }

        /// <summary>
        /// Destroys the nearest instance of the <b>currently selected brush's own prefab</b>
        /// within <see cref="EraseRadius"/>, searching only under the <c>MapContent</c> root so
        /// erase can never remove scene content this tool didn't paint. Requires a brush to be
        /// selected — with none selected, there's no type to match against, so this is a no-op
        /// rather than falling back to "erase whatever's nearest" (issue #74, designer-reported:
        /// right-click was blindly erasing the nearest painted object regardless of which brush
        /// created it, making it unsafe to clean up one piece type without risking an unrelated
        /// neighbor). Distance is compared on the XZ plane only, ignoring height — a right-click
        /// naturally lands on whatever surface is visible under the cursor (e.g. a
        /// <c>Platform_Tier1</c>'s top at Y=2.5), which can be several units above that object's
        /// own base-anchored pivot; a full 3D distance check would make any tall terrain object
        /// un-erasable regardless of how precisely the cursor is placed over it.
        /// </summary>
        /// <param name="worldPoint">The Ground-layer surface world position to erase at.</param>
        private void TryErase(Vector3 worldPoint)
        {
            if (_selectedBrush == null || !HasPaintablePrefab(_selectedBrush)) return;

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
                    if (!IsBrushSource(_selectedBrush, source)) continue;

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
    }
}

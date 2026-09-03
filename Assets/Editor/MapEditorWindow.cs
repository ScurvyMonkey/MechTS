using System.Linq;
using MechTS.Economy;
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
    /// Unity's own <see cref="Undo"/> system. Owns window lifecycle, GUI, and Scene View
    /// input routing only — paint/erase mechanics live in <see cref="MapEditorPainter"/> and
    /// preview-gizmo drawing in <see cref="MapEditorPreview"/> (issue #87), both composed as
    /// plain fields/static calls rather than grown inline, once this class's responsibilities
    /// outgrew a single file across 13+ prior issues.
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private const string DefaultPalettePath = "Assets/Data/MapEditor/MapBrushPalette.asset";

        [SerializeField] private MapBrushPalette _palette;
        [SerializeField] private MapBrushDefinition _selectedBrush;

        private Vector2 _scrollPosition;
        private readonly MapEditorPainter _painter = new MapEditorPainter();

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
        /// entirely during Play Mode, since this is a level-authoring tool only. Resolves the
        /// raw Ground-layer raycast point and dispatches to <see cref="MapEditorPainter"/> —
        /// brush-specific position resolution (Terrain Y-force, grid-snap) now lives there
        /// (issue #87), not here.
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
                _painter.EndDrag();
            }

            float gridSize = _palette != null ? _palette.gridSize : 0f;

            // Must run ahead of the paint/erase-only early-return below: the radius preview
            // needs to draw on hover alone (MouseMove/Repaint), before any button is pressed,
            // to actually serve as a "preview before you click" aid.
            if (_selectedBrush != null && _selectedBrush.isSplashBrush)
            {
                MapEditorPreview.DrawSplashRadiusPreview(e, _selectedBrush);
            }

            if (_selectedBrush != null && _selectedBrush.useGridSnap)
            {
                MapEditorPreview.DrawGridSnapPreview(e, _selectedBrush, gridSize);
            }

            MapEditorPreview.DrawStartPointMarkers();

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
            int groundLayerMask = LayerMask.GetMask(MapEditorPainter.GroundLayerName);
            if (!Physics.Raycast(ray, out RaycastHit hit, MapEditorPainter.MaxRaycastDistance, groundLayerMask)) return;

            // Claim the event so painting/erasing doesn't also orbit the Scene View camera
            // or try to select objects underneath the cursor.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            if (isPaintEvent)
            {
                _painter.TryPaint(_selectedBrush, hit.point, gridSize);
            }
            else
            {
                _painter.TryErase(_selectedBrush, hit.point);
            }

            e.Use();
            SceneView.RepaintAll();
        }
    }
}

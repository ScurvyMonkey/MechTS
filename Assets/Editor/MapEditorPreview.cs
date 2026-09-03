using MechTS.Economy;
using MechTS.Utilities;
using UnityEditor;
using UnityEngine;

namespace MechTS.EditorTools
{
    /// <summary>
    /// Scene View preview-gizmo drawing for <see cref="MapEditorWindow"/> (issue #87 —
    /// extracted alongside <see cref="MapEditorPainter"/> once the window's responsibilities
    /// grew past a single class). Stateless — every method reads current brush/scene state and
    /// draws, nothing persists between calls, so this stays a static utility rather than an
    /// instance the window would otherwise have to construct and hold for no reason.
    /// </summary>
    public static class MapEditorPreview
    {
        /// <summary>Radius of the always-visible marker drawn for each painted <see cref="PlayerStartPoint"/>.</summary>
        private const float StartPointMarkerRadius = 1.5f;

        /// <summary>
        /// Draws a wire-circle gizmo previewing a splash brush's scatter radius at the
        /// cursor's currently-resolved Ground-layer surface position, so the designer can see
        /// the scatter area before clicking. Called every Scene View event while a splash
        /// brush is selected (see the hover-tracking note in <see cref="MapEditorWindow.OnSceneGUI"/>),
        /// not just during an actual paint/erase action.
        /// </summary>
        /// <param name="e">The current Scene View GUI event, used to resolve the cursor's screen position.</param>
        /// <param name="brush">The currently selected splash brush.</param>
        public static void DrawSplashRadiusPreview(Event e, MapBrushDefinition brush)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            int groundLayerMask = LayerMask.GetMask(MapEditorPainter.GroundLayerName);
            if (!Physics.Raycast(ray, out RaycastHit hit, MapEditorPainter.MaxRaycastDistance, groundLayerMask)) return;

            Handles.color = Color.cyan;
            Handles.DrawWireDisc(hit.point, Vector3.up, brush.splashRadius);
        }

        /// <summary>
        /// Draws a wire-square gizmo at the cursor's grid-snapped position (issue #71), the size
        /// of one grid cell, so the designer can see exactly where a grid-snapping brush will
        /// land before clicking. Called every Scene View event while such a brush is selected
        /// (mirrors <see cref="DrawSplashRadiusPreview"/>'s hover-tracking shape), not just
        /// during an actual paint action.
        /// </summary>
        /// <param name="e">The current Scene View GUI event, used to resolve the cursor's screen position.</param>
        /// <param name="brush">The currently selected grid-snapping brush.</param>
        /// <param name="gridSize">The palette's shared grid size.</param>
        public static void DrawGridSnapPreview(Event e, MapBrushDefinition brush, float gridSize)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Physics.SyncTransforms();
            int groundLayerMask = LayerMask.GetMask(MapEditorPainter.GroundLayerName);
            if (!Physics.Raycast(ray, out RaycastHit hit, MapEditorPainter.MaxRaycastDistance, groundLayerMask)) return;

            Vector3 worldPoint = hit.point;
            if (brush.category == MapEditorPainter.TerrainCategory) worldPoint.y = 0f;
            Vector3 snapped = MapEditorPainter.SnapToGrid(worldPoint, gridSize);

            Handles.color = Color.yellow;
            Handles.DrawWireCube(snapped + Vector3.up * 0.05f, new Vector3(gridSize, 0.1f, gridSize));
        }

        /// <summary>
        /// Draws a faction-colored wire disc at every <see cref="PlayerStartPoint"/> currently
        /// in the scene, every time the window's Scene View callback runs. Deliberately drawn
        /// via <see cref="Handles"/> rather than relying on <see cref="PlayerStartPoint"/>'s own
        /// <c>OnDrawGizmos</c> — Editor Gizmos are gated by the Scene view's own "Gizmos"
        /// toolbar toggle (confirmed off by default in at least one real session, making a
        /// freshly-painted start point look like nothing happened), while a `Handles` call made
        /// here, from the window's own `OnSceneGUI`, is not. This keeps `PlayerStartPoint`'s
        /// existing Gizmo as a harmless fallback for anyone Scene-browsing without this window
        /// open, while guaranteeing visibility for the actual painting workflow this bug was
        /// reported against.
        /// </summary>
        public static void DrawStartPointMarkers()
        {
            foreach (var startPoint in Object.FindObjectsByType<PlayerStartPoint>(FindObjectsSortMode.None))
            {
                Handles.color = FactionColor.GetColor(startPoint.faction);
                Handles.DrawWireDisc(startPoint.transform.position, Vector3.up, StartPointMarkerRadius);
            }
        }
    }
}

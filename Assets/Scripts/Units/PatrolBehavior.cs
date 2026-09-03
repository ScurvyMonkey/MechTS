using System.Collections.Generic;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// Loops a unit through an ordered set of waypoints forever, until a new order cancels
    /// it — <see cref="UnitManager.MoveSelectedTo"/>/<see cref="UnitManager.StopSelected"/>
    /// destroy this component before issuing any other order, since every order in this
    /// project (Move, Attack, Stop, Defend, Return to Base) already funnels through one of
    /// those two methods. Added dynamically only to units actually given a Patrol order
    /// (issue #26) — most units never have this component. Draws a connecting line between
    /// its recorded waypoints, looped back to the first (issue #32), via a `LineRenderer` on
    /// a dedicated child GameObject — a sibling component on this same GameObject wouldn't be
    /// destroyed when this component is (Unity doesn't cascade a `Destroy()` across siblings),
    /// so the line lives on its own child instead, destroyed alongside it in <see cref="OnDestroy"/>.
    /// </summary>
    public class PatrolBehavior : MonoBehaviour
    {
        private const float LineWidth = 0.15f;
        private static readonly Color LineColor = new Color(1f, 0.85f, 0.2f);

        private static Material _sharedLineMaterial;

        private readonly List<Vector3> _waypoints = new List<Vector3>();

        private UnitBase _unit;
        private int _currentIndex;
        private LineRenderer _lineRenderer;

        /// <summary>
        /// Caches the sibling <see cref="UnitBase"/> and builds the (initially empty) patrol
        /// path line on its own child GameObject.
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBase>();

            var lineHost = new GameObject("PatrolLine");
            lineHost.transform.SetParent(transform, false);

            _lineRenderer = lineHost.AddComponent<LineRenderer>();
            _lineRenderer.material = GetLineMaterial();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = true;
            _lineRenderer.startWidth = LineWidth;
            _lineRenderer.endWidth = LineWidth;
            _lineRenderer.startColor = LineColor;
            _lineRenderer.endColor = LineColor;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.positionCount = 0;
        }

        /// <summary>
        /// Adds a waypoint to this patrol's route, in the order given. If this is the
        /// first waypoint recorded, immediately starts moving toward it. Refreshes the
        /// patrol path line, which only becomes visible once a second waypoint is added.
        /// </summary>
        /// <param name="point">The world-space waypoint to add.</param>
        public void AddWaypoint(Vector3 point)
        {
            _waypoints.Add(point);
            if (_waypoints.Count == 1)
            {
                _unit.MoveTo(point);
            }

            RefreshLine();
        }

        /// <summary>
        /// Advances to the next waypoint, looping back to the first, once the unit arrives
        /// at its current one.
        /// </summary>
        private void Update()
        {
            if (_waypoints.Count == 0 || !_unit.HasArrived()) return;

            _currentIndex = (_currentIndex + 1) % _waypoints.Count;
            _unit.MoveTo(_waypoints[_currentIndex]);
        }

        /// <summary>
        /// Redraws the patrol path line from the current waypoint list — hidden entirely
        /// below two waypoints (nothing to connect yet), otherwise a closed loop
        /// (<see cref="LineRenderer.loop"/>) through every recorded point in order.
        /// </summary>
        private void RefreshLine()
        {
            if (_waypoints.Count < 2)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            _lineRenderer.positionCount = _waypoints.Count;
            _lineRenderer.SetPositions(_waypoints.ToArray());
        }

        /// <summary>
        /// Destroys this patrol's path-line child GameObject alongside itself — a sibling
        /// component isn't destroyed automatically when this one is, and the line hosts its
        /// own child specifically so it can be cleaned up here without leaving a stale
        /// visual behind once the patrol is cancelled.
        /// </summary>
        private void OnDestroy()
        {
            if (_lineRenderer != null) Destroy(_lineRenderer.gameObject);
        }

        /// <summary>
        /// Returns the shared unlit material used by every patrol path line, creating it once.
        /// </summary>
        private static Material GetLineMaterial()
        {
            if (_sharedLineMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                _sharedLineMaterial = new Material(shader) { name = "PatrolLineMaterial" };
            }
            return _sharedLineMaterial;
        }
    }
}

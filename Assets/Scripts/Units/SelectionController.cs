using System.Collections.Generic;
using MechTS.Core;
using MechTS.Economy;
using MechTS.UI;
using MechTS.Utilities;
using MechTS.Vision;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MechTS.Units
{
    /// <summary>
    /// Handles left-click and drag-box selection of the player's units using the new
    /// Input System's device APIs directly (no generated actions asset — see completion
    /// report for why). Draws a visible box overlay while dragging. A single click that
    /// isn't a unit but is a player-owned building or main building selects it instead,
    /// exposed via <see cref="SelectedBuilding"/>/<see cref="SelectedMainBuilding"/> for
    /// <see cref="MechTS.UI.SelectionPanel"/>'s detail card. A click that hits none of
    /// those but lands on a <see cref="Economy.ResourceNode"/> (own layer — nodes sit on
    /// the Default layer, not <see cref="_unitLayerMask"/>'s layer 9, and have no faction)
    /// selects it instead, exposed via <see cref="SelectedResourceNode"/>. Clicking an
    /// <b>enemy</b> unit/building/main building (issue #24) populates the separate
    /// <see cref="SelectedEnemyUnit"/>/<see cref="SelectedEnemyBuilding"/>/
    /// <see cref="SelectedEnemyMainBuilding"/> properties instead of the player-scoped ones
    /// above — deliberately never touching <see cref="UnitManager.SelectedUnits"/> or the
    /// player-scoped building fields, since every order-issuing system (<see cref="AttackCommand"/>,
    /// <see cref="MoveCommand"/>, <see cref="ControlGroupController"/>) reads only those, with
    /// no faction check of its own. Keeping enemy inspection on entirely separate fields makes
    /// "you can't command an enemy" structural rather than an enforced permission check.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        [SerializeField] private LayerMask _unitLayerMask;
        [SerializeField] private LayerMask _resourceNodeLayerMask;

        private UnitManager _unitManager;
        private GameManager _gameManager;
        private VisionManager _visionManager;
        private Camera _camera;
        private Vector2 _dragStart;
        private bool _isDragging;
        private RectTransform _dragBoxRect;
        private GameObject _buildingIndicator;

        /// <summary>The player-owned building currently selected via single click, if any.</summary>
        public BuildingInstance SelectedBuilding { get; private set; }

        /// <summary>The player's main building, if currently selected via single click.</summary>
        public MainBuilding SelectedMainBuilding { get; private set; }

        /// <summary>The resource node currently selected via single click, if any. Nodes have
        /// no owning faction (see <see cref="ResourceNode"/>) so either side's node is selectable.</summary>
        public ResourceNode SelectedResourceNode { get; private set; }

        /// <summary>The enemy unit currently inspected via single click, if any — read-only,
        /// never added to <see cref="UnitManager.SelectedUnits"/> (see class doc comment).</summary>
        public UnitBase SelectedEnemyUnit { get; private set; }

        /// <summary>The enemy building currently inspected via single click, if any.</summary>
        public BuildingInstance SelectedEnemyBuilding { get; private set; }

        /// <summary>The enemy main building currently inspected via single click, if any.</summary>
        public MainBuilding SelectedEnemyMainBuilding { get; private set; }

        /// <summary>Whether a drag-select box is currently in progress. Read by
        /// <see cref="MechTS.Core.CameraManager"/> to suppress edge-of-screen panning while
        /// dragging — otherwise panning near an edge would move the camera out from under
        /// an in-progress drag box (see issue #10's architecture review).</summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Caches the scene's <see cref="UnitManager"/>, <see cref="GameManager"/>, and main
        /// camera, and builds the (initially hidden) drag-select box overlay.
        /// </summary>
        private void Start()
        {
            _unitManager = FindFirstObjectByType<UnitManager>();
            _gameManager = FindFirstObjectByType<GameManager>();
            _visionManager = FindFirstObjectByType<VisionManager>();
            _camera = Camera.main;

            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null)
            {
                _dragBoxRect = UIFactory.CreateRect(uiManager.HUDCanvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var image = _dragBoxRect.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.15f);
                _dragBoxRect.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Tracks a left-mouse drag, updates the drag-box visual, and resolves the drag into
        /// a single click or a box-select on release. Ignored outside an active round (e.g.
        /// while the Mission End screen is shown).
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                _dragStart = mouse.position.ReadValue();
                _isDragging = true;
            }

            if (_isDragging)
            {
                UpdateDragBoxVisual(_dragStart, mouse.position.ReadValue());
            }

            if (mouse.leftButton.wasReleasedThisFrame && _isDragging)
            {
                _isDragging = false;
                if (_dragBoxRect != null) _dragBoxRect.gameObject.SetActive(false);

                Vector2 dragEnd = mouse.position.ReadValue();

                if (Vector2.Distance(_dragStart, dragEnd) < 4f)
                {
                    SelectSingleAt(dragEnd);
                }
                else
                {
                    SelectBox(_dragStart, dragEnd);
                }
            }
        }

        /// <summary>
        /// Positions and sizes the drag-box overlay to span the given screen-space corners.
        /// </summary>
        /// <param name="a">One corner of the drag rectangle, in screen space.</param>
        /// <param name="b">The opposite corner of the drag rectangle, in screen space.</param>
        private void UpdateDragBoxVisual(Vector2 a, Vector2 b)
        {
            if (_dragBoxRect == null) return;

            _dragBoxRect.gameObject.SetActive(true);

            var canvasRect = (RectTransform)_dragBoxRect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, a, null, out Vector2 localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, b, null, out Vector2 localB);

            _dragBoxRect.anchoredPosition = (localA + localB) * 0.5f;
            _dragBoxRect.sizeDelta = new Vector2(Mathf.Abs(localB.x - localA.x), Mathf.Abs(localB.y - localA.y));
        }

        /// <summary>
        /// Selects the single unit, building, main building, or resource node under the
        /// cursor, or clears the selection if there isn't one. Units, buildings, and the main
        /// building share <see cref="_unitLayerMask"/> (see completion report), so one raycast
        /// resolves all three, checked in that priority order; resource nodes sit on their own
        /// <see cref="_resourceNodeLayerMask"/> and are checked with a second raycast only if
        /// the first didn't resolve to anything. An enemy-owned hit resolves into the
        /// read-only <see cref="SelectedEnemyUnit"/>/<see cref="SelectedEnemyBuilding"/>/
        /// <see cref="SelectedEnemyMainBuilding"/> properties instead of the player-scoped ones.
        /// </summary>
        /// <param name="screenPosition">The screen-space position to raycast from.</param>
        private void SelectSingleAt(Vector2 screenPosition)
        {
            ClearBuildingSelection();
            _unitManager.ClearSelection();

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _unitLayerMask))
            {
                var unit = hit.collider.GetComponentInParent<UnitBase>();
                if (unit != null)
                {
                    if (unit.Faction == Faction.Player)
                    {
                        _unitManager.SetSelection(new[] { unit });
                    }
                    else if (IsVisibleToPlayer(unit.transform.position))
                    {
                        SelectedEnemyUnit = unit;
                        unit.SetSelected(true);
                    }
                    return;
                }

                var building = hit.collider.GetComponentInParent<BuildingInstance>();
                if (building != null)
                {
                    if (building.Faction == Faction.Player)
                    {
                        SelectedBuilding = building;
                        ShowBuildingIndicator(building.transform);
                    }
                    else if (IsVisibleToPlayer(building.transform.position))
                    {
                        SelectedEnemyBuilding = building;
                        ShowBuildingIndicator(building.transform);
                    }
                    return;
                }

                var mainBuilding = hit.collider.GetComponentInParent<MainBuilding>();
                if (mainBuilding != null)
                {
                    if (mainBuilding.Faction == Faction.Player)
                    {
                        SelectedMainBuilding = mainBuilding;
                        ShowBuildingIndicator(mainBuilding.transform);
                    }
                    else if (IsVisibleToPlayer(mainBuilding.transform.position))
                    {
                        SelectedEnemyMainBuilding = mainBuilding;
                        ShowBuildingIndicator(mainBuilding.transform);
                    }
                    return;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit nodeHit, 1000f, _resourceNodeLayerMask))
            {
                var node = nodeHit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    SelectedResourceNode = node;
                    ShowBuildingIndicator(node.transform);
                }
            }
        }

        /// <summary>
        /// Clears any currently selected building/main building/resource node/enemy
        /// inspection and hides its highlight — deselecting an enemy unit also toggles off
        /// its own ring via <see cref="UnitBase.SetSelected"/>, since it was never added to
        /// <see cref="UnitManager.SelectedUnits"/> for <see cref="UnitManager.ClearSelection"/>
        /// to handle.
        /// </summary>
        private void ClearBuildingSelection()
        {
            SelectedBuilding = null;
            SelectedMainBuilding = null;
            SelectedResourceNode = null;
            SelectedEnemyBuilding = null;
            SelectedEnemyMainBuilding = null;
            if (SelectedEnemyUnit != null)
            {
                SelectedEnemyUnit.SetSelected(false);
                SelectedEnemyUnit = null;
            }
            if (_buildingIndicator != null) _buildingIndicator.SetActive(false);
        }

        /// <summary>
        /// Shows the shared building-selection highlight ring on the given target, creating it
        /// the first time it's needed.
        /// </summary>
        /// <param name="target">The building/main building transform to highlight.</param>
        private void ShowBuildingIndicator(Transform target)
        {
            if (_buildingIndicator == null)
            {
                _buildingIndicator = SelectionIndicator.Create(transform);
            }
            SelectionIndicator.AttachTo(_buildingIndicator, target);
            _buildingIndicator.SetActive(true);
        }

        /// <summary>
        /// Whether a world position is currently visible to the Player faction (issue #37) —
        /// an enemy-owned hit that fails this check resolves as if nothing were there at all,
        /// not just "not selectable." Always true if no <see cref="VisionManager"/> exists.
        /// </summary>
        /// <param name="worldPosition">The world position to check.</param>
        private bool IsVisibleToPlayer(Vector3 worldPosition)
        {
            return _visionManager == null || _visionManager.IsVisible(Faction.Player, worldPosition);
        }

        /// <summary>
        /// Selects every owned unit whose screen-space position falls inside the drag rectangle.
        /// </summary>
        /// <param name="a">One corner of the drag rectangle, in screen space.</param>
        /// <param name="b">The opposite corner of the drag rectangle, in screen space.</param>
        private void SelectBox(Vector2 a, Vector2 b)
        {
            ClearBuildingSelection();

            Rect box = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

            var inBox = new List<UnitBase>();
            foreach (var unit in _unitManager.ActiveUnits)
            {
                if (unit.Faction != Faction.Player) continue;

                Vector3 screenPos = _camera.WorldToScreenPoint(unit.transform.position);
                if (box.Contains(screenPos))
                {
                    inBox.Add(unit);
                }
            }

            _unitManager.SetSelection(inBox);
        }
    }
}

using System.Collections.Generic;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Vision;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MechTS.Units
{
    /// <summary>
    /// Resolves every right-click into whichever order type currently applies, by trying each
    /// <see cref="IRightClickOrderResolver"/> in <see cref="_resolvers"/> in priority order and
    /// stopping at the first one that consumes the click (issue #60) — taking over right-click
    /// handling from <see cref="MoveCommand"/> so every order shares one click instead of
    /// multiple components racing on it. Priority order (highest first): pending building
    /// placement confirm (#46), rally point set/clear (#23), an armed Attack-Move/Patrol order
    /// (#26), Harvester redirect (#38), Sabotage (#3/#31), Repair (#42), then Attack/Move.
    /// </summary>
    public class AttackCommand : MonoBehaviour
    {
        [SerializeField] private LayerMask _targetableLayerMask;
        [SerializeField] private LayerMask _groundLayerMask;
        [SerializeField] private LayerMask _resourceNodeLayerMask;

        private GameManager _gameManager;
        private Camera _camera;
        private List<IRightClickOrderResolver> _resolvers;

        /// <summary>
        /// Caches the scene's managers and main camera, then builds the ordered list of
        /// <see cref="IRightClickOrderResolver"/>s this component dispatches right-clicks to.
        /// A resolver backed by an optional component (<see cref="BuildingPlacement"/>,
        /// <see cref="UnitOrderCommand"/>) is only added if that component actually exists in
        /// the scene, matching this component's previous null-guarded behavior.
        /// </summary>
        private void Start()
        {
            var unitManager = FindFirstObjectByType<UnitManager>();
            _gameManager = FindFirstObjectByType<GameManager>();
            var selectionController = FindFirstObjectByType<SelectionController>();
            var unitOrderCommand = FindFirstObjectByType<UnitOrderCommand>();
            var buildingPlacement = FindFirstObjectByType<BuildingPlacement>();
            var visionManager = FindFirstObjectByType<VisionManager>();
            _camera = Camera.main;

            _resolvers = new List<IRightClickOrderResolver>();
            if (buildingPlacement != null)
            {
                _resolvers.Add(new PendingPlacementOrderResolver(buildingPlacement, _groundLayerMask));
            }
            _resolvers.Add(new RallyPointOrderResolver(selectionController, _targetableLayerMask, _groundLayerMask));
            if (unitOrderCommand != null)
            {
                _resolvers.Add(new PendingUnitOrderResolver(unitOrderCommand, _groundLayerMask));
            }
            _resolvers.Add(new HarvestRedirectOrderResolver(unitManager, _resourceNodeLayerMask));
            _resolvers.Add(new SabotageOrderResolver(unitManager, _targetableLayerMask));
            _resolvers.Add(new RepairOrderResolver(unitManager, _targetableLayerMask));
            _resolvers.Add(new AttackOrMoveOrderResolver(unitManager, visionManager, _targetableLayerMask, _groundLayerMask));
        }

        /// <summary>
        /// On every right-click, builds the click's camera ray and tries each resolver in
        /// <see cref="_resolvers"/> in order, stopping at the first one that handles it. Ignored
        /// outside an active round (e.g. while the Mission End screen is shown) or while the
        /// cursor is over UI.
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());

            foreach (var resolver in _resolvers)
            {
                if (resolver.TryHandle(ray)) return;
            }
        }
    }
}

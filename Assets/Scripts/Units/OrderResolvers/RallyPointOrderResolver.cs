using MechTS.Economy;
using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// If a player-owned, production-capable building (or the player's Main Building, issue
    /// #41) is currently selected, resolves this right-click into a rally-point set (clicked
    /// ground) or clear (clicked the building itself) instead of a unit order (issue #23,
    /// extracted into its own resolver by issue #60).
    /// </summary>
    public class RallyPointOrderResolver : IRightClickOrderResolver
    {
        private readonly SelectionController _selectionController;
        private readonly LayerMask _targetableLayerMask;
        private readonly LayerMask _groundLayerMask;

        /// <summary>
        /// Wraps the given <see cref="SelectionController"/> and layer masks.
        /// </summary>
        /// <param name="selectionController">The scene's <see cref="SelectionController"/> component.</param>
        /// <param name="targetableLayerMask">The layer mask units/buildings/main buildings share.</param>
        /// <param name="groundLayerMask">The ground layer mask to raycast against.</param>
        public RallyPointOrderResolver(SelectionController selectionController, LayerMask targetableLayerMask, LayerMask groundLayerMask)
        {
            _selectionController = selectionController;
            _targetableLayerMask = targetableLayerMask;
            _groundLayerMask = groundLayerMask;
        }

        /// <summary>
        /// Buildings and units are never selected simultaneously (see
        /// <see cref="SelectionController"/>), so this never conflicts with any lower-priority
        /// resolver — those would just no-op on an empty <see cref="UnitManager.SelectedUnits"/>
        /// in this case anyway.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        public bool TryHandle(Ray ray)
        {
            var building = _selectionController != null ? _selectionController.SelectedBuilding : null;
            if (building != null && building.Faction == Faction.Player && building.CanProduceUnits)
            {
                if (Physics.Raycast(ray, out RaycastHit targetHit, 1000f, _targetableLayerMask)
                    && targetHit.collider.GetComponentInParent<BuildingInstance>() == building)
                {
                    building.ClearRallyPoint();
                    return true;
                }

                if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _groundLayerMask))
                {
                    building.SetRallyPoint(groundHit.point);
                    return true;
                }

                return false;
            }

            var mainBuilding = _selectionController != null ? _selectionController.SelectedMainBuilding : null;
            if (mainBuilding != null && mainBuilding.Faction == Faction.Player)
            {
                if (Physics.Raycast(ray, out RaycastHit mainTargetHit, 1000f, _targetableLayerMask)
                    && mainTargetHit.collider.GetComponentInParent<MainBuilding>() == mainBuilding)
                {
                    mainBuilding.ClearRallyPoint();
                    return true;
                }

                if (Physics.Raycast(ray, out RaycastHit mainGroundHit, 1000f, _groundLayerMask))
                {
                    mainBuilding.SetRallyPoint(mainGroundHit.point);
                    return true;
                }

                return false;
            }

            return false;
        }
    }
}
